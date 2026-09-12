# ADR-0011 : Authentification passwordless pour `arkcloud_app` (IAM DB auth AWS / Entra ID Azure) — proposition technique

**Statut** : **Acceptée pour le scope AWS** (implémentée le 08/09/2026, Sprint 6) — scope Azure : **Terraform + code applicatif écrits le 12/09/2026** (authentification Entra ID activée sur `psql-arkcloud-dev`, administrateur AAD désigné, chemin `Database:AuthMode=AzureAd` dans `InfrastructureServiceRegistration.cs`), **bootstrap SQL et bascule applicative réelle pas encore exécutés** — voir `ArkCloudInfra/docs/runbooks/bootstrap-arkcloud-app-azure-entra-id.md` pour les étapes restantes. Écrit ≠ vérifié : ne pas considérer ce scope clos tant que ce runbook n'a pas été suivi de bout en bout comme l'a été le pendant AWS.
**Date** : 2026-09 (Sprint 6, exploration backlog → décision explicite de l'utilisateur d'implémenter dans la foulée, plutôt que d'attendre Sprint 7/8)
**Sprint** : **6** pour le scope AWS (révisé — voir Décision), candidat Sprint 7/8 toujours valable pour le scope Azure si repris plus tard

## Contexte

Task #89 du backlog, ouverte pendant la discussion sur la rotation `arkcloud_app` (Sprint 6) : au lieu de faire tourner un mot de passe applicatif tous les 90 jours (mécanisme déjà en place, voir ADR-0010 côté Azure et `modules/aws/secret-rotation` côté AWS), peut-on éliminer le mot de passe lui-même ? AWS et Azure proposent tous les deux un mécanisme d'authentification basé sur l'identité plutôt que sur un secret statique : **IAM database authentication** côté AWS RDS, **Microsoft Entra ID authentication** côté Azure PostgreSQL Flexible Server. Cette ADR documente une proposition technique concrète pour les deux clouds — ni codée ni décidée, un point de départ vérifié pour un futur sprint applicatif.

## État réel du terrain, vérifié le 07/09/2026 (pas supposé)

**AWS** :
- `iam_database_authentication_enabled = true` est déjà actif sur `psql-arkcloud-dev` (`modules/aws/rds/main.tf` ligne 83) — activé de longue date, jamais exploité.
- Le rôle IAM `ecs-task-{name_prefix}` (`modules/aws/ecs/main.tf`) existe déjà et est **vide** — c'est le rôle que l'application (pas l'agent ECS) assume réellement, celui qui devrait porter la permission `rds-db:connect`. Zéro nouvelle ressource IAM à créer, juste une policy à y attacher.
- Piège connu et déjà documenté dans ce projet (recherché en session précédente, `aws/amazon-ecs-agent#1604`) : c'est bien le **task role**, jamais l'execution role, qui doit porter cette permission — l'execution role sert uniquement à ce que l'agent ECS lui-même fasse (pull d'image, écriture de logs, lecture de secrets au démarrage du conteneur).

**Azure** :
- Rien en place. Pas d'administrateur Entra ID configuré sur `psql-arkcloud-dev`, pas de bloc `authentication { active_directory_auth_enabled = true }` sur la ressource `azurerm_postgresql_flexible_server`.
- Les deux App Services (`app-arkcloud-api-dev`, `app-arkcloud-web-dev`) ont déjà une identité managée système (`identity { type = "SystemAssigned" }`, `modules/azure/app-service/main.tf`) — déjà utilisée pour Key Vault (`DefaultAzureCredential` dans `Program.cs`). C'est la même identité qui porterait l'auth Postgres, pas une nouvelle à créer.

**Application (`ArkCloud.Infrastructure`)** :
- `InfrastructureServiceRegistration.cs` lit une connection string statique une seule fois au démarrage : `options.UseNpgsql(configuration.GetConnectionString("DefaultConnection"))`. Un mot de passe IAM (validité 15 min) ou un token Entra ID (validité de quelques heures) rendent ce modèle caduc tel quel — il faut un mécanisme de rafraîchissement, pas juste changer la valeur de config.

## Design technique proposé

### AWS — IAM DB auth via Npgsql `UsePeriodicPasswordProvider`

Côté SQL, une fois par bootstrap (comme le bootstrap actuel de `arkcloud_app`) :
```sql
GRANT rds_iam TO arkcloud_app;
```
`rds_iam` est le rôle Postgres géré par AWS qui active l'authentification par token pour un rôle donné — coexiste avec l'authentification par mot de passe existante, permettant une transition progressive plutôt qu'un cutover brutal.

Côté Terraform (`modules/aws/ecs/main.tf`), attacher au rôle `task` déjà existant :
```hcl
resource "aws_iam_role_policy" "task_rds_connect" {
  name = "rds-iam-connect"
  role = aws_iam_role.task.id
  policy = jsonencode({
    Version = "2012-10-17"
    Statement = [{
      Effect   = "Allow"
      Action   = "rds-db:connect"
      Resource = "arn:aws:rds-db:${var.region}:${var.account_id}:dbuser:${var.rds_resource_id}/arkcloud_app"
    }]
  })
}
```

Côté C# (`InfrastructureServiceRegistration.cs`), remplacer l'enregistrement statique par un `NpgsqlDataSource` construit avec un provider de mot de passe périodique :
```csharp
var dataSourceBuilder = new NpgsqlDataSourceBuilder(hostConnectionStringSansMotDePasse);
dataSourceBuilder.UsePeriodicPasswordProvider(
    passwordProvider: (_, _) => ValueTask.FromResult(
        RDSAuthTokenGenerator.GenerateAuthToken(host, 5432, "arkcloud_app")),
    successRefreshInterval: TimeSpan.FromMinutes(10),   // token valide 15 min, marge de sécurité
    failureRefreshInterval: TimeSpan.FromSeconds(5));
var dataSource = dataSourceBuilder.Build();
services.AddDbContext<ArkCloudDbContext>(options => options.UseNpgsql(dataSource));
```
Nécessite le package `AWSSDK.RDS` (génération de token, ne fait aucun appel réseau — c'est une signature SigV4 locale) et que le task role soit résolu via le SDK AWS standard (déjà le cas dans un conteneur ECS Fargate, credentials injectées automatiquement).

### Azure — Entra ID auth via Npgsql `UsePeriodicPasswordProvider` + `DefaultAzureCredential`

Côté Terraform (`modules/azure/postgresql/main.tf` ou équivalent) :
```hcl
resource "azurerm_postgresql_flexible_server" "this" {
  # ... config existante ...
  authentication {
    active_directory_auth_enabled = true
    password_auth_enabled         = true   # coexistence pendant la transition, comme côté AWS
    tenant_id                     = var.tenant_id
  }
}

resource "azurerm_postgresql_flexible_server_active_directory_administrator" "api" {
  server_name         = azurerm_postgresql_flexible_server.this.name
  resource_group_name = var.resource_group_name
  tenant_id           = var.tenant_id
  object_id           = module.app_service_api.principal_id  # identité managée déjà existante
  principal_name      = "app-arkcloud-api-dev"
  principal_type      = "ServicePrincipal"
}
```

Côté C#, même mécanisme Npgsql, source de token différente :
```csharp
var credential = new DefaultAzureCredential();
dataSourceBuilder.UsePeriodicPasswordProvider(
    passwordProvider: async (_, ct) =>
    {
        var token = await credential.GetTokenAsync(
            new TokenRequestContext(["https://ossrdbms-aad.database.windows.net/.default"]), ct);
        return token.Token;
    },
    successRefreshInterval: TimeSpan.FromMinutes(45),  // token valide plusieurs heures, marge large
    failureRefreshInterval: TimeSpan.FromSeconds(5));
```
Nécessite le package `Azure.Identity` (déjà présent — utilisé pour Key Vault) et `Npgsql` récent (le provider périodique n'existe pas dans les toutes premières versions 8.x).

### Point commun aux deux clouds

Les deux providers de mot de passe périodique se substituent à une connection string à mot de passe fixe — même mécanisme Npgsql (`NpgsqlDataSourceBuilder.UsePeriodicPasswordProvider`), seule la source du token change. Un seul point d'abstraction possible dans `InfrastructureServiceRegistration.cs` (une interface `IDbTokenProvider` avec une implémentation AWS et une Azure, sélectionnée par config), cohérent avec le pattern déjà utilisé ailleurs dans ce projet pour les différences AWS/Azure.

## Options non retenues dans cette proposition

- **Cutover brutal (désactiver le mot de passe dès l'activation IAM/Entra)** : rejeté par défaut — `password_auth_enabled` reste `true` en parallèle le temps de valider en conditions réelles, cohérent avec la prudence déjà appliquée pour la bascule `arkcloud_app` elle-même (Sprint 6).
- **Migrer aussi le rôle admin (`arkcloudadmin`/`arkcloud` master user)** : hors périmètre — l'auth passwordless a plus de sens pour le rôle applicatif (connexion permanente depuis un service identifiable) que pour un rôle d'administration ponctuelle, où un secret classique avec accès Break Glass reste plus simple à raisonner.

## Effort estimé

- AWS : petit (policy Terraform + ~20 lignes C# + un package NuGet) — la quasi-totalité du terrain est déjà prêt (IAM DB auth actif, rôle vide en attente).
- Azure : moyen (bloc auth sur le serveur + ressource AD admin + même changement C#, mais rien n'est encore posé côté infra).
- Commun aux deux : tests d'intégration (`ArkCloudApiFactory.cs` utilise probablement une connection string classique — à vérifier), et une vraie validation en conditions réelles avant de couper `password_auth_enabled`.

## Décision

**Révisée le 08/09/2026** : l'utilisateur a explicitement demandé d'implémenter le scope AWS pendant le Sprint 6 plutôt que d'attendre un sprint applicatif — le thème (suppression d'une classe de secret statique) colle mieux à "sécurité cloud avancée" qu'à Angular (Sprint 7) ou microservices (Sprint 8), qui n'ont aucun lien naturel avec ce sujet. Le scope Azure (Entra ID) reste non implémenté : effort moyen, rien n'est posé côté infra Azure, pas de terrain aussi favorable que côté AWS (IAM DB auth déjà actif, rôle applicatif déjà vide et prêt).

**Implémenté pour AWS** — design conforme à la proposition technique ci-dessus, sans déviation :
- Terraform (`modules/aws/ecs/main.tf`) : policy `rds-db:connect` sur le task role, scopée à `arkcloud_app` uniquement via le nouvel output `resource_id` de `modules/aws/rds`.
- SQL : `GRANT rds_iam TO arkcloud_app` ajouté à `_set_secret_app_role` dans `modules/aws/secret-rotation/lambda/rotate.py` — réutilise le mécanisme de bootstrap idempotent déjà en place (Sprint 6, task #74/75) plutôt qu'un script séparé, cohérent avec le commentaire déjà présent dans ce fichier sur le "cheap self-heal".
- C# (`InfrastructureServiceRegistration.cs`) : nouveau chemin `Database:AuthMode=AwsIam` avec `NpgsqlDataSourceBuilder.UsePeriodicPasswordProvider` + `RDSAuthTokenGenerator.GenerateAuthToken` (signature vérifiée contre le code source réel d'`aws-sdk-net`, pas supposée). Le chemin par défaut (`ConnectionStrings:DefaultConnection` passé directement à `UseNpgsql(string)`) reste **strictement inchangé** — pas de construction de `NpgsqlDataSource` en dehors du cas AWS IAM, pour ne rien risquer côté Azure/tests d'intégration/dev local.
- `environments/dev/main.tf` (`aws_ecs_service_api`) : `Database__AuthMode=AwsIam` + `Database__Host/Port/Name/Username` ajoutés à l'environnement du service. Le secret `ConnectionStrings__DefaultConnection` reste injecté (coexistence assumée), même si l'app ne le lit plus tant que `Database__AuthMode` est positionné — un rollback vers l'auth par mot de passe redevient un simple retrait de variable d'environnement.

**Pas fait dans ce lot, à vérifier avant un `apply` réel** : le package NuGet `AWSSDK.RDS` a été ajouté au `.csproj` mais pas restauré/compilé dans cette session (pas d'accès `dotnet build` avec les vraies dépendances ici) ; le zip du Lambda de rotation (`modules/aws/secret-rotation/lambda/build/rotate.zip`) n'a pas été reconstruit avec le nouveau `GRANT rds_iam` — nécessite de relancer `build.sh` puis de redéployer, comme pour toute modification de `rotate.py`.

## Conséquences

**Positives** — plus de mot de passe applicatif à faire tourner ni à exposer dans Secrets Manager pour `arkcloud_app` côté AWS ; supprime une classe entière de risque (secret statique volé/leaké, l'incident réel de ce projet — tfstate exposé, task #87) plutôt que de la mitiger par rotation périodique. Désamorce en partie la procédure Kudu jamais implémentée (#83, backlog) : le seul rôle qui en avait besoin côté rotation applicative n'a plus de mot de passe à faire tourner une fois validé en conditions réelles.

**Négatives / compromis** — dépendance plus forte à la disponibilité d'IAM STS pour toute nouvelle connexion DB (si IAM STS est indisponible, l'appli ne peut plus se reconnecter, alors qu'un mot de passe statique continuerait de fonctionner) ; complexité de test légèrement supérieure en théorie, mais neutralisée en pratique ici puisque `ArkCloudApiFactory.cs` remplace entièrement l'enregistrement du `DbContext` par un container Testcontainers avant que le chemin AWS IAM ne s'exécute ; asymétrie transitoire pendant que `password_auth_enabled` reste `true` en parallèle côté AWS, et que le scope Azure n'a toujours aucune implémentation.

**Ce qui reste ouvert** — validation en conditions réelles (apply + déploiement Lambda + test de connexion) non faite dans cette session, à faire par l'utilisateur ; scope Azure (Entra ID) toujours à l'état de proposition, sans date.
