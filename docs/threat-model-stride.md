# Modélisation de menaces — STRIDE (Sprint 6)

> Complète l'analyse de risque structurée (Step 18.7, roadmap) : le risk storming évalue le risque d'architecture (disponibilité, dette, coût...), STRIDE évalue la menace orientée attaquant sur des flux de confiance précis. Les deux se recoupent par endroits (ex. certificat auto-signé) — normal, ce sont deux angles sur le même système, pas deux exercices redondants.

**Méthode** : chaque flux de confiance est passé au filtre des 6 catégories STRIDE (Spoofing, Tampering, Repudiation, Information disclosure, Denial of service, Elevation of privilege). Seules les catégories où une menace réelle et spécifique a été identifiée sont détaillées — lister "aucune menace" pour chaque case vide n'apporterait rien. Chaque menace retenue a une décision explicite : **Mitigée** (contrôle déjà en place), **Acceptée** (tracée en ADR, risque assumé), ou **À traiter** (aucun contrôle, pas encore une décision consciente).

Flux couverts, dans l'ordre où une requête les traverse réellement : navigateur → ALB/App Service, Blazor → API, API → base, CI → cloud, rotation automatique → base.

---

## 1. Navigateur → ALB (AWS) / App Service (Azure)

| Catégorie | Menace | État | Décision |
|---|---|---|---|
| **S**poofing | Certificat auto-signé sur l'ALB AWS : un attaquant en position MITM peut présenter son propre certificat sans qu'aucun signal fiable ne le distingue de celui légitime pour un utilisateur habitué à l'avertissement de confiance du navigateur. N'affecte pas Azure App Service (certificat Microsoft, chaîne de confiance valide). | Non mitigé côté AWS | **Acceptée** — ADR-0003. Track : fitness function candidate (détecter si le certificat servi redevient auto-signé après un futur remplacement par un certificat DNS-validé), pas encore construite. |
| **T**ampering | Modification du trafic en transit. | Mitigée | HTTPS forcé des deux côtés (`https_only = true` sur App Service, redirection HTTP→HTTPS sur l'ALB) — le chiffrement protège contre la modification passive même sans chaîne de confiance complète côté AWS. |
| **R**epudiation | Aucun log d'accès HTTP au niveau de l'ALB — impossible de prouver après coup qui a atteint le service au niveau infra (l'application logue ses propres requêtes, mais pas la couche ALB). | Non traité | **À traiter** — Checkov `CKV_AWS_91` (ALB access logging) écarté au Sprint 6 faute de bucket S3 dédié, jamais construit depuis (voir ArkCloudInfra README §9). Candidat naturel Sprint 10 (observabilité) plutôt qu'un ajout isolé. |
| **D**enial of service | Aucun rate limiting au niveau infra (ALB/App Service) — seule l'application (ASP.NET Core `RateLimiter`, scope essentiellement login) protège, et seulement une fois la requête déjà arrivée jusqu'au compute. | Non mitigé | **Acceptée pour l'instant** — voir ADR-0008. Un déni de service volumétrique en amont de l'application n'est filtré par rien. |

## 2. Blazor (frontend) → API

| Catégorie | Menace | État | Décision |
|---|---|---|---|
| **S**poofing | Un client falsifie son identité auprès de l'API. | Mitigée | JWT Bearer authentication, vérifié à chaque requête protégée. |
| **T**ampering | Falsification du token ou de la requête en transit. | Mitigée | JWT signé (HMAC via `Jwt:Key`), HTTPS de bout en bout côté Azure ; côté AWS dépend du même hop ALB que la section 1 (cert auto-signé — risque déjà tracé, pas dupliqué ici). |
| **I**nformation disclosure | CORS trop permissif exposerait l'API à des origines non autorisées. | Mitigée | Policy CORS nommée (`"BlazorFrontend"`) plutôt qu'un `AllowAnyOrigin`. |
| **E**levation of privilege | Un utilisateur authentifié accède à des ressources/actions au-delà de son rôle. | Mitigée | Policies d'autorisation par rôle (`Authorization/PolicyNames.cs`), vérifiées côté serveur — jamais une confiance côté client seul. |

## 3. API → base (PostgreSQL)

| Catégorie | Menace | État | Décision |
|---|---|---|---|
| **S**poofing | Un service se faisant passer pour l'API se connecte directement à la base. | Mitigée | Base accessible uniquement via le réseau privé (VNet integration côté Azure, private endpoint ; VPC + security groups côté AWS) — pas d'exposition publique à usurper. |
| **T**ampering | Interception/modification du trafic SQL en transit. | Mitigée | TLS forcé par défaut sur PostgreSQL Flexible Server (`require_secure_transport = ON`, `modules/azure/postgresql`) ; RDS avec `storage_encrypted` et connexion dans le VPC privé côté AWS. |
| **I**nformation disclosure | Identifiant client ou autre donnée personnelle qui fuiterait dans les logs applicatifs au niveau de cette frontière. | Recoupe RGPD | **À traiter** — objet direct du chantier RGPD (Step 16 septies, minimisation des logs), pas dupliqué ici en détail. |
| **E**levation of privilege | Le compte applicatif utilisé par l'API a plus de droits SQL que nécessaire (ex. `DROP TABLE`, droits admin). | Non vérifié | **À traiter** — aucune revue documentée des privilèges du rôle Postgres applicatif à ce jour, contrairement au travail déjà fait côté IAM cloud (Sprint 6). Candidat pour une prochaine revue, même méthode que l'audit IAM. |

## 4. CI (GitHub Actions) → cloud (Azure / AWS)

| Catégorie | Menace | État | Décision |
|---|---|---|---|
| **S**poofing | Un workflow GitHub Actions usurpé obtiendrait des identifiants cloud. | Mitigée | OIDC des deux côtés (`ARM_USE_OIDC`, `aws-actions/configure-aws-credentials` avec `role-to-assume`) — aucune clé statique long-lived à voler ; le trust policy est scopé au repo (`repo:epiph11/ArkCloudInfra`). |
| **E**levation of privilege | Le rôle CI (`arkcloudinfra-ci`) a plus de droits que ce que le pipeline exécute réellement. | Mitigée (Sprint 6) | Audit IAM Sprint 6 : `AdministratorAccess` retiré, remplacé par des policies scopées compte+région (voir README ArkCloudInfra §11). Régression trouvée et corrigée dans la foulée (permissions insuffisantes après resserrement) — traçable dans l'historique de ce même README. |
| **E**levation of privilege | `GHCR_PAT`, un secret personnel humain, vit dans le chemin de déploiement — sa compromission équivaut à une élévation de privilège vers le pipeline de déploiement. | Non mitigé structurellement | **Acceptée** — ADR-0007. Scope minimal (`read:packages` seul), rotation surveillée par rappel automatique, mais reste un secret humain unique dans un chemin critique. |
| **R**epudiation | Une modification d'infra appliquée sans traçabilité de qui/pourquoi. | Mitigée | Chaque `apply` passe par une PR + `terraform plan` publié dans le résumé du run ; `apply` lui-même gated par l'Environment GitHub "production" (approbation manuelle possible). |

## 5. Rotation automatique (Runbook/Lambda) → base

| Catégorie | Menace | État | Décision |
|---|---|---|---|
| **E**levation of privilege | L'identité qui exécute la rotation (Automation Runbook Azure, Lambda AWS) a plus de droits sur la base que "changer le mot de passe". | Mitigée (Sprint 6) | Rôles personnalisés dédiés (`azurerm_role_definition`, audit IAM Sprint 6) — `.../flexibleServers/{read,write}` côté Azure, IAM role scopé côté AWS — pas de rôle `Contributor`/administrateur générique. |
| **D**enial of service | Une rotation ratée ou mal appliquée rend la base inaccessible à l'application (mot de passe changé côté Postgres mais pas propagé partout). | Partiellement mitigée | Alarme CloudWatch sur les erreurs de la Lambda de rotation (`aws_cloudwatch_metric_alarm.rotation_errors`) côté AWS. Pas d'équivalent documenté côté Azure Automation Runbook à ce jour. |
| **T**ampering | La restaurabilité d'une sauvegarde après une rotation automatique n'a jamais été vérifiée — une base restaurée depuis un backup pré-rotation porterait l'ancien mot de passe. | Non vérifié | **À traiter** — recoupe directement le chantier "drill de restauration" (Step 16 septies), qui doit explicitement couvrir ce cas plutôt que le découvrir en situation réelle. |

---

## Résumé — ce qui reste réellement ouvert

Des menaces listées ci-dessus, celles sans mitigation ni décision d'acceptation tracée (statut **À traiter**) :
- Absence de logs d'accès ALB (repudiation, flux 1)
- Revue des privilèges SQL du compte applicatif (élévation, flux 3)
- Restaurabilité après rotation automatique, jamais testée (tampering, flux 5)

Celles explicitement **acceptées** (tracées en ADR, pas ignorées) :
- Certificat auto-signé ALB (ADR-0003)
- `Jwt:Key` non rotatable sans déconnexion de masse (ADR-0004)
- `GHCR_PAT`, secret humain dans le chemin de déploiement (ADR-0007)
- Absence de rate limiting au niveau infra (ADR-0008)

Les trois premières ("à traiter") sont les candidates naturelles pour le prochain passage sur ce document — pas un exercice ponctuel, à rejouer à chaque changement structurant (Sprint 8, Sprint 9) comme prévu au Step 18.7.
