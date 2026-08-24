# Sprint 4 — CI/CD & fondation Azure

> **Spécification technique, racontée par ses décisions.**
> Ce document ne liste pas seulement ce qui a été construit, mais *pourquoi* chaque choix a été fait, ce qui a cassé en chemin, et ce qui a été délibérément laissé de côté. Les bugs y figurent volontairement : ils sont la partie la plus instructive du sprint, et plusieurs n'étaient détectables qu'à l'exécution réelle.

**Périmètre** : Steps 1 à 9 du roadmap, versant Azure.
**Statut** : clôturé le 28/07/2026.
**Livrable** : une plateforme Azure complète, provisionnée par Terraform, alimentée par un pipeline CI/CD qui déploie réellement, avec monitoring et logs d'audit vérifiés en conditions réelles.

---

## 1. La décision fondatrice : deux repos, pas un monorepo

La documentation d'origine plaçait Terraform dans `ArkCloud/deploy/terraform/`. Décision prise en sens inverse : un repo séparé, **`ArkCloudInfra`**.

Le raisonnement n'est pas esthétique. Trois arguments concrets :

- **Rayon d'impact** — une erreur dans un pipeline applicatif ne doit pas pouvoir déclencher un `terraform apply` sur l'infrastructure.
- **Gouvernance différenciée** — les changements d'infra méritent des règles de revue et des approbations distinctes de celles du code applicatif. Deux repos rendent cette asymétrie naturelle plutôt que conventionnelle.
- **Permissions** — l'identité qui déploie l'infra n'a aucune raison d'avoir accès au code applicatif, et réciproquement.

Le prix à payer, assumé : la coordination entre les deux repos devient un vrai problème d'ingénierie. C'est exactement ce qui a produit le mécanisme de déclenchement cross-repo décrit en §6, et le bug de tags décrit en §8.

---

## 2. Le problème de l'œuf et la poule : le remote state

Terraform gère l'infrastructure, mais son propre state doit vivre quelque part — et ce quelque part ne peut pas être géré par le Terraform qu'il stocke.

**Solution retenue** : un bootstrap manuel one-shot en Azure CLI créant un resource group `rg-terraform-state` dédié, distinct de `rg-arkcloud-dev`. Un storage account avec versioning activé et verrouillage par lease.

**Pourquoi un resource group séparé** — décision qui a payé plus tard : quand le garde-fou budgétaire est arrivé (Sprint 6), il a pu être scopé à `rg-arkcloud-dev` uniquement. Si le state avait vécu dans le même groupe, son coût aurait compté dans le budget applicatif, et un `terraform destroy` malheureux aurait pu emporter le state avec l'infrastructure qu'il décrit.

---

## 3. Modules Terraform — les arbitrages structurants

Sept modules, chacun portant une décision explicite.

### `network` — quatre sous-réseaux, dont un vide

`vnet-arkcloud-dev` découpé en quatre : `snet-api`, `snet-web`, `snet-database`, `snet-private-endpoint`.

Le quatrième est **délibérément vide**. Il est réservé aux private endpoints (Key Vault, storage) prévus au durcissement Sprint 6. Le provisionner tout de suite coûte zéro et évite d'avoir à redécouper le plan d'adressage plus tard — redécoupage qui, sur un VNet en service, est nettement plus douloureux qu'une planification anticipée.

`snet-database` porte une **délégation** à `Microsoft.DBforPostgreSQL/flexibleServers` : c'est ce qui permet à PostgreSQL Flexible Server d'être injecté dans le VNet plutôt que d'être exposé publiquement.

`nsg-api` n'a aucune règle entrante personnalisée — l'intégration VNet d'App Service est sortante uniquement, rien n'écoute en entrée sur ce sous-réseau. Le NSG existe quand même, pour que le durcissement futur ait un point d'accroche.

### `key-vault` — RBAC, et aucun secret dans le code

Deux décisions liées.

**RBAC plutôt que les access policies héritées** : les autorisations se gèrent comme sur n'importe quelle autre ressource Azure, sans syntaxe spécifique à Key Vault à maintenir en parallèle.

**Aucun secret créé par Terraform.** Le module crée le coffre, jamais son contenu. Les valeurs sont posées hors bande (`az keyvault secret set`). La raison est le state lui-même : tout secret créé via Terraform y atterrit en clair. Le state est certes versionné et verrouillé, mais le meilleur moyen de ne pas exposer un secret dans un state reste de ne jamais l'y mettre.

Convention de nommage imposée par le fournisseur de configuration .NET : `--` remplace `:`. Un secret nommé `Jwt--Key` devient la clé `Jwt:Key` côté application. C'est pour ça que les noms contiennent des doubles tirets et non des points.

### `app-service` — deux plans, pas un partagé

`ArkCloud.API` et `ArkCloud.Blazor` tournent chacun sur leur propre App Service Plan Basic B1. Le coût est doublé — et cette décision a été réexaminée au Sprint 6 lors de l'investigation budgétaire, puis maintenue : le tier Basic B1 est déjà le moins cher qui supporte l'intégration VNet régionale dont l'API a besoin pour joindre PostgreSQL en privé. Les tiers Free (F1) et Shared (D1) ne la supportent pas — vérifié en direct, `az appservice plan update --sku F1` échoue tant que l'app y est rattachée.

Blazor reçoit `Api__BaseUrl` pointant sur le hostname réel de l'API, injecté par Terraform. Ni l'un ni l'autre ne code en dur l'adresse de l'autre.

---

## 4. Premier `plan`, premier `apply` : trois bugs que la relecture ne pouvait pas trouver

Instructif, parce que les trois se manifestent à des étages différents de la chaîne de validation.

| Bug | Détecté par | Nature |
|---|---|---|
| `enable_rbac_authorization` renommé `rbac_authorization_enabled` | Avertissement au `plan` | Évolution du schéma du provider — l'ancien nom marche encore en v4, disparaîtra en v5. Corrigé par anticipation. |
| `health_check_path` exige désormais `health_check_eviction_time_in_min` | Erreur bloquante au `plan` | Contrainte de schéma introduite par une version récente du provider. Nouvelle variable ajoutée, défaut à 2 (minimum autorisé par Azure). |
| `public_network_access_enabled` (vrai par défaut) en conflit avec `delegated_subnet_id` | Erreur bloquante à l'**`apply`**, pas au `plan` | La règle est validée par l'API Azure, pas par le schéma Terraform. Aucune analyse statique ne pouvait l'attraper. |

Le troisième est le plus intéressant : il illustre qu'un `plan` vert ne garantit rien. La seule preuve qu'une configuration est valide est un `apply` réussi contre le vrai fournisseur.

---

## 5. Authentification CI : OIDC, et le format de sujet immuable

Le pipeline s'authentifie à Azure par **OIDC** — un jeton émis par GitHub Actions à chaque run, jamais un secret client stocké quelque part. Cela suppose une App Registration Azure AD dont les federated credentials font confiance à un sujet précis (`repo:<org>/ArkCloudInfra:ref:refs/heads/main`).

**Bug rencontré** : le déploiement cross-repo, validé de bout en bout le 16/07, s'est ensuite bloqué sur un changement de format des sujets OIDC devenus immuables. Corrigé, mais l'épisode a mis en évidence une fragilité réelle : la fédération d'identité dépend d'un contrat de nommage entre GitHub et Azure qui peut évoluer sans que le code change.

---

## 6. Déclenchement cross-repo — la conséquence directe de la séparation des repos

Le prix de la décision §1. Le mécanisme retenu : `repository_dispatch` de `ArkCloud` vers `ArkCloudInfra`, avec un PAT dédié `INFRA_DISPATCH_TOKEN`.

**Deux variables de tag séparées, pas une seule** (`api_image_tag` et `web_image_tag`) : quand seule l'API est reconstruite, il ne faut pas que Terraform redéploie aussi Blazor sur un tag qu'il n'a pas produit. Deux variables permettent au dispatch de ne cibler que ce qui a réellement changé.

**Limitation connue, non résolue** : deux chemins d'apply coexistent — le `terraform apply` complet (CI classique) et l'apply `-target` scopé (dispatch cross-repo). Si le tag réellement désiré diverge un jour du défaut, ils peuvent se marcher dessus. Aucun correctif structurel n'a été apporté ; c'est documenté comme un point de vigilance plutôt que masqué.

---

## 7. Monitoring : trois bugs en cascade, et une leçon

Le monitoring était « câblé » depuis le début. Il ne **fonctionnait** pas. La vérification réelle (26-28/07) a révélé trois bugs indépendants, empilés les uns sur les autres.

### Bug 1 — La variable était posée, mais personne ne la lisait

`APPLICATIONINSIGHTS_CONNECTION_STRING` était correctement injectée par Terraform depuis l'origine. Mais ni `ArkCloud.API` ni `ArkCloud.Blazor` n'avaient le SDK Application Insights référencé en code. `az monitor app-insights query` retournait zéro ligne, indéfiniment.

**Cause profonde** : un conteneur Docker personnalisé n'a pas l'auto-instrumentation « codeless » d'Azure, réservée aux stacks runtime managées. Le SDK doit être référencé explicitement. Corrigé par `AddApplicationInsightsTelemetry()` dans les deux applications.

**La leçon** : de l'infrastructure correctement configurée pointant vers une application qui ne l'utilise pas produit exactement le même résultat qu'une infrastructure absente — sauf que le code Terraform, lui, a l'air juste.

### Bug 2 — Aucune image réelle n'était tirée

`docker_registry_username`/`password` étaient vides et le package GHCR était privé. Résultat : `ImagePullUnauthorizedFailure` en boucle depuis le 16/07 — les App Services tournaient sur l'image placeholder par défaut d'Azure.

Corrigé par un PAT GitHub dédié, scope `read:packages` seul. **Détail non évident** : un token *fine-grained* ne fonctionne pas pour l'authentification GHCR container ; il faut un token *classic*.

**Piège rencontré** : le secret doit être posé dans **les deux** workflows qui font un `terraform apply` (`terraform-ci.yml` **et** `deploy-on-image.yml`). L'avoir ajouté au premier seulement a fait planter le second au dispatch suivant avec « No value for required variable ».

### Bug 3 — Un tag par défaut qui n'a jamais existé

`api_image_tag`/`web_image_tag` avaient pour défaut `"latest"` — un tag jamais publié sur GHCR (seuls `dev` et des SHA de commit le sont). À chaque `terraform apply` complet, Terraform ramenait silencieusement le tag déployé à `latest`, annulant le déploiement réel effectué par le dispatch précédent.

Corrigé en alignant les défauts sur `"dev"`.

**Vérification finale, par requête réelle** : lignes présentes pour les deux `cloud_RoleName`, avec `resultCode`, `duration` et géolocalisation client. Le monitoring est prouvé, pas déclaré.

---

## 8. Logs d'audit — et le piège du pipeline legacy

La checklist de clôture (Step 17) a révélé qu'aucun `azurerm_monitor_diagnostic_setting` n'existait : Log Analytics ne recevait que la télémétrie applicative, rien au niveau plateforme.

Quatre diagnostic settings ajoutés (Key Vault, PostgreSQL, les deux App Services), avec `category_group = "allLogs"` plutôt qu'une liste de catégories nommées — pour ne pas dépendre d'une énumération figée que le provider peut faire évoluer.

**Puis le vrai bug** : Key Vault et PostgreSQL remontaient en quelques minutes. Les deux App Services n'ont produit **aucune ligne**, même après 30 minutes et du trafic HTTP généré exprès.

**Cause** : sans `log_analytics_destination_type` explicite, le diagnostic setting utilise le mode legacy « Azure Diagnostics » (table partagée `AzureDiagnostics`) — documenté par Microsoft comme le pipeline le moins fiable **spécifiquement pour `Microsoft.Web/sites`**.

Corrigé par `log_analytics_destination_type = "Dedicated"` sur les deux App Services uniquement (tables dédiées `AppServiceHTTPLogs`, `AppServiceConsoleLogs`, etc.). Key Vault et PostgreSQL laissés en mode legacy puisqu'ils fonctionnaient. Changer cet attribut force un remplacement du diagnostic setting, pas un update — attendu, pas une erreur.

**Vérification finale par requête réelle** : 7 lignes `AuditEvent` (Key Vault), 1068 `PostgreSQLLogs` + 62 sessions + 15 table stats (PostgreSQL), 72 `AppServiceConsoleLogs` + 5 `AppServiceHTTPLogs` (API), 6 `AppServiceHTTPLogs` (Blazor).

---

## 9. Checkov : corriger, ou écarter en argumentant

Le premier run réel a remonté **28 findings**. Aucun faux positif — un vrai désaccord entre un jeu de règles « prod durcie par défaut » et des choix conscients pour un environnement **dev**.

La discipline adoptée, maintenue tout au long du projet : **corriger ce qui est gratuit et sans compromis, écarter le reste avec un raisonnement écrit** — jamais désactiver le scanner, jamais laisser un skip non justifié.

**Corrigés** (applicables à tout environnement) : FTP de déploiement désactivé, HTTP/2 activé, certificats client en mode optionnel, messages d'erreur détaillés, et un NSG manquant sur `snet-private-endpoint` — un vrai oubli, pas un choix.

**Écartés avec justification** : redondance de zone, instances minimales, SKU « production », backup géo-redondant — tous exigent de sortir du tier dev vers un tier réellement plus cher, sans bénéfice sur un environnement jetable. Les répertoires `environments/staging` et `prod` existent déjà précisément pour appliquer ces standards quand un vrai environnement à durcir existera.

Cas particulier instructif : **`CKV_AZURE_13`** (Easy Auth) écarté non pour des raisons de coût, mais parce que l'activer ferait doublon avec le système JWT propre à l'application — deux couches d'authentification qui se marcheraient dessus. Un finding peut être techniquement valide et architecturalement faux.

---

## 10. Ce qui a été laissé ouvert, en connaissance de cause

Un sprint honnête documente ses trous.

| Gap | Décision | Suite |
|---|---|---|
| NSG flow logs | Nécessitent une vraie brique d'infra (Network Watcher + storage), pas un réglage | Sprint 6 — et se révélera plus complexe que prévu (Azure a retiré les NSG flow logs) |
| Private endpoints | `snet-private-endpoint` réservé mais vide | Sprint 6 |
| `GET /health` renvoie 404 | Aucun health check ASP.NET Core implémenté, alors que Terraform pointe dessus. Le probe Azure tape toutes les ~30s et reçoit 404 | Repo applicatif, corrigé au Sprint 5 |
| Rotation des secrets | Aucune automatisation — procédure manuelle documentée avec échéances | Sprint 6 (automatisée pour de vrai) |
| Migration JFrog Artifactory | Reportée sans date | Chantier séparé |
| Conflit apply complet vs `-target` | Aucun correctif structurel | Point de vigilance assumé |

---

## Bilan

Sprint 4 livre une plateforme Azure réellement fonctionnelle : pipeline Terraform vert de bout en bout, déploiement continu opérationnel, monitoring applicatif **vérifié par requête réelle**, logs d'audit routés et confirmés.

La leçon transversale du sprint tient en une phrase : **le code correct et le système qui fonctionne sont deux choses différentes**. Les trois bugs de monitoring, le conflit `public_network_access_enabled`, le tag `latest` inexistant — aucun n'était visible en relisant le Terraform. Tous ont demandé une vérification en conditions réelles. C'est cette discipline, plus que l'infrastructure elle-même, qui constitue le vrai acquis du sprint.
