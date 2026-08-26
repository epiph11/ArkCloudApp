# ADR-0007 : `GHCR_PAT` — secret humain dans le chemin de déploiement, risque accepté

**Statut** : Acceptée
**Date** : 2026-08 (Sprint 6, issue de l'analyse STRIDE)
**Sprint** : 6

## Contexte

Contrairement à tous les autres accès cloud de ce projet (Azure OIDC, AWS OIDC), l'authentification à GHCR pour qu'Azure App Service puisse tirer les images Docker repose sur `GHCR_PAT`, un Personal Access Token GitHub classique appartenant à un compte humain. Sa compromission équivaut à une élévation de privilège directe vers le pipeline de déploiement — quiconque le détient peut faire tourner n'importe quelle image sur les App Services.

Voir aussi `docs/threat-model-stride.md` (flux CI → cloud, Elevation of privilege) et ADR-0004 (rotation sélective des secrets).

## Options envisagées

1. **Éliminer le PAT** — passer par Azure Container Registry avec identité managée (même logique que AWS ECR + rôle IAM, qui n'a pas ce problème). Supprime le risque structurellement, mais infra nouvelle (ACR, coût) et double push d'image.
2. **Réduire la portée du risque sans l'éliminer** — scope minimal du PAT, rotation surveillée, rappel automatique d'échéance.
3. **Ne rien faire** — laisser le risque non tracé, non assumé consciemment.

## Décision

Option 2 pour l'instant : le risque est accepté, pas éliminé, avec les mitigations suivantes déjà en place :
- Scope du PAT limité à `read:packages` seul — pas d'accès en écriture, pas d'accès à d'autres ressources GitHub.
- Rappel d'échéance automatisé (`.github/workflows/secret-expiry-check.yml`), qui a déjà fonctionné en pratique (alerte réelle déclenchée à 10 jours de l'expiration, Sprint 6).
- Rotation manuelle documentée en 5 étapes (README ArkCloudInfra §10), pas laissée à l'improvisation.

L'option 1 (éliminer via ACR) reste en **backlog**, non tranchée — voir roadmap §Continuité.

## Conséquences

**Positives**
- Le risque est nommé et tracé plutôt que découvert en marge d'un incident.
- Le scope minimal (`read:packages`) limite ce qu'une compromission permettrait réellement, même si le risque d'élévation de privilège vers le déploiement reste réel.

**Négatives / compromis assumés**
- Le risque structurel demeure : tant que l'option 1 n'est pas mise en œuvre, un PAT humain reste dans un chemin de déploiement automatisé — la seule vraie protection est la rotation et la surveillance, pas l'élimination de la surface d'attaque.
- La rotation reste partiellement manuelle (voir ADR-0004) — un humain doit agir dans la fenêtre d'alerte, sans quoi le PAT expire et casse le déploiement (disponibilité), ou pire, reste actif au-delà de sa date prévue sans qu'on le sache si l'alerte est manquée.

**Ce que ça bloque ou impose pour la suite**
- Revoir cette décision si l'incident redouté se matérialise (compromission détectée) ou si le coût d'un ACR devient justifiable à l'échelle du projet — pas de critère de déclenchement formel posé à ce jour.
