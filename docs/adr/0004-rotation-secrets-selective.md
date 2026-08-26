# ADR-0004 : Rotation automatique pour les mots de passe Postgres, manuelle par décision explicite pour `Jwt:Key` et `GHCR_PAT`

**Statut** : Acceptée
**Date** : 2026-08 (Sprint 6)
**Sprint** : 6

## Contexte

Quatre secrets vivent dans ce système : `POSTGRES_ADMIN_PASSWORD` (Azure et AWS), `Jwt:Key`, `GHCR_PAT`. Les automatiser tous n'a pas le même coût ni le même risque selon le secret.

## Options envisagées

1. **Tout automatiser** — cohérent, mais `Jwt:Key` et `GHCR_PAT` ont des contraintes structurelles qui rendent une automatisation naïve dangereuse ou impossible (voir Décision).
2. **Tout manuel** — sûr mais revient à espérer qu'un humain se souvienne des échéances, ce qui a déjà produit l'incident ayant motivé le garde-fou cost-guard (mots de passe exposés dans le chat, voir Journal des décisions du roadmap).
3. **Automatiser sélectivement**, selon ce que chaque secret permet réellement.

## Décision

- **`POSTGRES_ADMIN_PASSWORD`** (Azure + AWS) : rotation automatique tous les 90 jours (Azure Automation Runbook + schedule ; AWS Secrets Manager + Lambda custom). Le mot de passe est un secret que ce système génère et applique lui-même — rien n'empêche de l'automatiser complètement.
- **`Jwt:Key`** : manuel. Une seule clé de signature, sans `kid` (key ID) permettant une rotation à chaud avec chevauchement de clés — la rotation invalide tous les tokens actifs et déconnecte tous les utilisateurs. Automatiser reviendrait à programmer une déconnexion de masse récurrente sans supervision humaine.
- **`GHCR_PAT`** : manuel, par contrainte externe — GitHub n'expose aucune API pour créer un PAT (classique ou fine-grained), seule la génération via github.com/settings/tokens est possible, donc au moins une étape reste humaine quoi qu'on fasse (voir backlog, roadmap §Continuité).

Les deux secrets manuels sont couverts par un rappel d'échéance automatisé (`.github/workflows/secret-expiry-check.yml` + `.github/secrets-inventory.json`) : le système n'automatise pas la rotation elle-même, mais automatise le fait de ne jamais oublier qu'elle est due.

## Conséquences

**Positives**
- Le secret le plus critique en fréquence de compromission potentielle (mot de passe DB, accessible à quiconque a une session applicative compromise) est celui avec la rotation la plus courte et la plus fiable.
- Aucune automatisation dangereuse construite juste pour cocher une case "tout est automatisé".

**Négatives / compromis assumés**
- `Jwt:Key` reste un point de défaillance non rotatable sans impact utilisateur visible — un risque explicitement tracé dans l'analyse STRIDE plutôt que découvert plus tard.
- `GHCR_PAT` garde une procédure manuelle en 5 étapes (voir README ArkCloudInfra §10) — le rappel automatique réduit le risque d'oubli, pas l'effort de rotation elle-même.

**Ce que ça bloque ou impose pour la suite**
- `Jwt:Key` : une vraie automatisation nécessiterait un support multi-clés (`kid`) — chantier non planifié, coût réel en complexité pour un bénéfice qui ne se matérialise qu'au moment d'une rotation.
- `GHCR_PAT` : deux pistes backlog non tranchées (automatiser les étapes 2-5, ou éliminer le PAT via Azure Container Registry + identité managée) — voir roadmap §Continuité.
