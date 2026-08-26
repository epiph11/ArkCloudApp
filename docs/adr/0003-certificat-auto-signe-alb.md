# ADR-0003 : Certificat auto-signé sur l'ALB AWS, plutôt que HTTP en clair ou attendre un domaine réel

**Statut** : Acceptée (temporaire par construction)
**Date** : 2026-08 (Sprint 6)
**Sprint** : 6

## Contexte

L'ALB AWS n'avait que HTTP jusqu'au Sprint 6 (Checkov `CKV_AWS_2`/`CKV2_AWS_20`/`CKV_AWS_103`). Un certificat ACM public validé par DNS ou email requiert un domaine réel — ce projet n'en a pas encore (pas de Route 53 ni de domaine externe possédé).

## Options envisagées

1. **Rester en HTTP** — pas de chiffrement en transit sur le hop navigateur→ALB, seul le trafic ALB→cible interne (VPC) est déjà privé.
2. **Attendre un domaine réel** pour un certificat ACM DNS-validé — chiffrement + chaîne de confiance complète, mais bloque le durcissement TLS indéfiniment sans date de domaine prévue.
3. **Certificat auto-signé**, importé dans ACM (`tls_private_key` + `tls_self_signed_cert`, flux d'import sans validation de domaine) — chiffrement réel immédiatement, sans chaîne de confiance.

## Décision

Certificat auto-signé (option 3), avec redirection HTTP→HTTPS. `ArkCloud.Blazor` configure ses `HttpClient` pour faire confiance explicitement à ce host précis (`Api__TrustSelfSignedCert`) plutôt que de désactiver la validation TLS globalement.

## Conséquences

**Positives**
- Trafic navigateur→ALB réellement chiffré, immédiatement, sans attendre un domaine.
- Les trois findings Checkov concernés sont résolus par une vraie amélioration de sécurité, pas par un `skip_check`.
- Le module ALB (`modules/aws/alb`) est écrit pour que remplacer le certificat par un vrai ACM DNS-validé ne change pas la forme des ressources — juste swap la source du certificat.

**Négatives / compromis assumés**
- Aucune chaîne de confiance : un navigateur affiche un avertissement de sécurité. Acceptable pour du trafic dev (health checks, tests directs), pas pour de vrais utilisateurs finaux.
- Un attaquant en position de MITM actif peut présenter son propre certificat auto-signé sans que rien ne le distingue visuellement de celui légitime pour un utilisateur qui a pris l'habitude de cliquer "continuer quand même" — c'est la menace concrète documentée dans l'analyse STRIDE (`docs/threat-model-stride.md`, flux navigateur→ALB, Spoofing).
- `Api__TrustSelfSignedCert` est une porte ouverte volontairement étroite (un host précis) plutôt qu'une désactivation globale de la validation TLS — mais reste une dérogation à documenter à chaque revue de sécurité tant qu'elle existe.

**Ce que ça bloque ou impose pour la suite**
- À remplacer par un certificat DNS-validé dès qu'un domaine existe pour ce projet — pas de sprint numéroté pour l'instant, dépend de l'acquisition du domaine.
- Ce risque a explicitement une fitness function candidate mais pas encore construite : une vérification automatisée que le certificat servi n'est plus auto-signé, qui ferait échouer un futur pipeline si quelqu'un ré-introduisait cet état par erreur après le remplacement.
