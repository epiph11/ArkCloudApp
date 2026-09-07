# ADR-0010 : Bootstrap/rotation du rôle `arkcloud_app` sur Azure — procédure manuelle via Kudu, plutôt qu'une automatisation dédiée

**Statut** : Acceptée (implémentation reportée en backlog — voir addendum du 07/09/2026)
**Date** : 2026-08 (Sprint 6)
**Sprint** : 6

## Contexte

STRIDE flux 3 (élévation de privilège, `docs/threat-model-stride.md`) demande un rôle Postgres applicatif à moindre privilège (`arkcloud_app`, DML seul) pour remplacer `arkcloudadmin` côté connexion de l'API. Côté AWS, ce chantier est terminé : le rôle est créé et sa rotation automatisée en fusionnant la logique dans la Lambda de rotation déjà existante (`modules/aws/secret-rotation`, mode `target_role = "app"`), qui tourne déjà dans le VPC et n'a donc aucun nouvel accès réseau à construire.

Côté Azure, ce raccourci n'existe pas : l'Automation Runbook qui rotate déjà `arkcloudadmin` n'a **aucun accès réseau** au VNet privé où vit Postgres — il ne fait qu'appeler l'API de gestion Azure (control plane), qui ne nécessite aucune connectivité réseau. Étendre ce Runbook pour y exécuter du vrai SQL (`ALTER ROLE`) demande une connectivité que l'Automation Account n'a pas nativement. Confirmé via la documentation officielle : *"In the current implementation of Private Link, Automation account cloud jobs cannot access Azure resources that are secured using private endpoint [...] To workaround this, use a Hybrid Runbook Worker instead"* (learn.microsoft.com/.../how-to/private-link-security).

Un Hybrid Runbook Worker est une machine virtuelle qui tourne en continu spécifiquement pour donner à l'Automation Account un accès réseau — un coût récurrent (~15-70 $/mois selon le SKU) pour une tâche qui ne sert qu'une fois tous les 90 jours.

## Options envisagées

1. **Hybrid Runbook Worker** — solution "propre" côté Azure (garde l'automatisation complète, cohérente avec le mécanisme AWS), mais introduit une VM payante à faire tourner en continu pour une tâche trimestrielle. Rejetée sur ce seul critère coût/bénéfice.
2. **Débloquer un accès réseau interactif** (ex. Kudu SSH sur un App Service existant, ECS `execute-command` côté AWS) **pour un usage récurrent/automatisé** — écarté après une analyse explicite des compromis sécurité : ouvrir un canal d'accès interactif permanent est un changement de posture, pas juste une commodité, même si le coût est nul.
3. **Azure Functions (Flex Consumption)** — essayé concrètement (voir `modules/azure/functions-experiment`, maintenant démonté). Techniquement fonctionnel une fois trois limitations réelles contournées : `zip_deploy_file` ne fonctionne pas pour Flex Consumption côté provider Terraform (bug ouvert, [hashicorp/terraform-provider-azurerm#29630](https://github.com/hashicorp/terraform-provider-azurerm/issues/29630) — déploiement réel via `az functionapp deployment source config-zip --build-remote true` à la place), `az webapp log tail`/`log config` ne fonctionnent pas sur ce type de plan (Application Insights branché à la place). Le rôle a été créé et sa rotation vérifiée en conditions réelles (`role_created: true` puis `false` sur deux appels successifs, mot de passe confirmé dans Key Vault). Écarté malgré tout : garder ça en production ajoute un type de ressource entièrement nouveau au projet (jamais utilisé ailleurs), avec plusieurs contournements de bugs à maintenir, pour un besoin qui survient une fois par trimestre.
4. **Procédure manuelle via Kudu**, sur `app-arkcloud-api-dev` (l'App Service qui héberge déjà l'API) — cet App Service a déjà l'intégration VNet vers `snet-api`, donc déjà l'accès réseau à Postgres via la règle existante `AllowPostgresFromApi` (`nsg-database`). Zéro nouvelle ressource : pas de VM, pas de nouveau sous-réseau, pas de nouveau type de service.

## Décision

Option 4. La console Kudu (SSH) de `app-arkcloud-api-dev`, déclenchée manuellement par un humain, sert à exécuter le script SQL de bootstrap/rotation de `arkcloud_app` — même schéma opérationnel que `GHCR_PAT` et `Jwt:Key` (ADR-0004) : rotation manuelle, mais couverte par le rappel d'échéance automatisé existant (`.github/secrets-inventory.json` + `secret-expiry-check.yml`), pour ne jamais dépendre de la mémoire d'un humain plutôt que d'automatiser l'exécution elle-même.

Ce choix n'est pas motivé par le coût : Functions aurait tout aussi bien pu tourner gratuitement en automatique (~30 Go-secondes consommées sur un essai complet, largement sous le quota gratuit mensuel). La vraie raison est la complexité de maintenance : Kudu ne demande aucune brique nouvelle à comprendre, déployer ou faire évoluer, alors que Functions en introduit plusieurs (nouveau provider de ressources Azure, pipeline de déploiement distinct, contournements de bugs propres à Flex Consumption) pour un bénéfice qui ne se matérialise qu'une fois tous les 90 jours.

## Conséquences

**Positives**
- Zéro nouvelle infrastructure permanente côté Azure pour ce besoin — réutilise un App Service déjà existant, déjà payé, déjà dans le bon sous-réseau.
- Aucun nouveau canal d'accès interactif créé spécifiquement pour cette tâche (contrairement à l'Option 2) — Kudu sur `app-arkcloud-api-dev` est un accès qui existe déjà pour l'exploitation courante de l'App Service, pas une porte ouverte en plus.
- L'essai Azure Functions n'a pas été du temps perdu : il prouve que le rôle `arkcloud_app` et sa logique de bootstrap/rotation fonctionnent réellement sur Postgres Azure (même code SQL que côté AWS, vérifié en conditions réelles), et documente pour la suite du projet un premier contact concret avec Azure Functions Flex Consumption — utile si un futur chantier (Sprint 8+) a un vrai besoin serverless.

**Négatives / compromis assumés**
- Rotation manuelle, donc dépendante d'un rappel plutôt que d'un déclenchement automatique — même compromis déjà assumé pour `Jwt:Key`/`GHCR_PAT` (ADR-0004), pas une nouveauté dans la façon dont ce projet gère ses secrets.
- Asymétrie durable entre AWS (rotation `arkcloud_app` automatique, fusionnée dans la Lambda existante) et Azure (manuelle via Kudu) — actée consciemment ici plutôt que découverte plus tard, mais reste une incohérence opérationnelle entre les deux clouds à garder en tête.

**Ce que ça bloque ou impose pour la suite**
- Écrire la procédure Kudu elle-même (script SQL + étapes SSH documentées) — pas encore fait au moment de cette ADR.
- Ajouter `ArkCloudAppRole--Password (Azure)` à `.github/secrets-inventory.json` avec une échéance de 90 jours, sur le même modèle que les entrées existantes.
- Si un jour la fréquence de rotation devait descendre sous 90 jours, ou si Azure Functions Flex Consumption mûrit (les trois limitations rencontrées ici sont d'assez récentes fonctionnalités, susceptibles d'être corrigées), cette décision mériterait d'être réexaminée plutôt que reconduite par défaut.

## Addendum (07/09/2026) — deux corrections, décision inchangée

**Fait erroné corrigé** : cette ADR affirmait `modules/azure/functions-experiment` "maintenant démonté" (Option 3). Faux — vérifié dans `environments/dev/main.tf` (le bloc `module "functions_experiment"` y est toujours) et dans un `terraform plan`/`apply` réel du même jour (la ressource `module.functions_experiment.azurerm_function_app_flex_consumption.this` y apparaît toujours, `Refreshing state`). L'étape de démontage documentée dans `modules/azure/functions-experiment/README.md` ("Démonter (une fois l'essai concluant)") n'a jamais été exécutée. Le Function App tourne donc toujours en production aujourd'hui, avec `arkcloud_app` opérationnel dessus — c'est, de fait, le mécanisme qui a servi à la bascule réelle vers `arkcloud_app` (tâche #84), pas Kudu.

**Prémisse de l'Option 4 à vérifier avant implémentation** : la Décision ci-dessus suppose que la console Kudu (SSH) fonctionne "telle quelle" sur `app-arkcloud-api-dev` parce que l'App Service a déjà l'intégration VNet. C'est vrai pour l'accès réseau, mais insuffisant : `app-arkcloud-api-dev` tourne une **image Docker custom** (pas un stack Azure managé), et le Dockerfile de `ArkCloud.API` n'installe ni ne configure aujourd'hui de serveur SSH. Pour un conteneur Linux custom, Kudu SSH exige que l'image elle-même embarque `sshd` (port 2222, démarré en parallèle du process principal — pattern documenté par Microsoft pour les conteneurs custom), ce qui n'a jamais été posé dans ce projet. La phrase "Kudu... est un accès qui existe déjà" (section Conséquences positives) n'a donc jamais été vérifiée en pratique et est probablement fausse telle quelle.

**Décision** : la Option 4 (Kudu) reste le choix retenu — discuté avec l'utilisateur, qui souhaite explicitement tester cette voie plutôt que consacrer davantage de ressources Azure Functions au sujet. Son implémentation (ajout de `sshd` au Dockerfile, rebuild, vérification réelle du SSH) est repostée en fond de backlog (tâche #83), pas abandonnée. **En attendant**, le Function App reste en place et sert de mécanisme de facto pour toute rotation réelle de `arkcloud_app` côté Azure qui serait nécessaire avant que Kudu soit implémenté — ne pas le démonter tant que Kudu n'est pas prouvé fonctionnel en remplacement.
