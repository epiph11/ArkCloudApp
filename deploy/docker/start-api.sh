#!/bin/sh
set -e

# Sprint 6 -- ADR-0010 (rotation arkcloud_app via Kudu). Demarre sshd en arriere-plan pour que la
# console Kudu fonctionne sur ce conteneur Linux custom, puis lance dotnet au premier plan avec
# `exec` -- le process principal (PID 1 du point de vue d'App Service) reste `dotnet
# ArkCloud.API.dll`, comme avant cet ajout, pas sshd. Coherent avec la facon dont App Service suit
# le cycle de vie du conteneur (healthcheck HTTP sur 8080, pas un signal lie a sshd).
# Cles hote SSH regenerees a chaque demarrage (jamais baties dans l'image -- voir le commentaire
# dans Dockerfile.api : Trivy a signale les cles gravees au build comme secret HIGH severity, en
# plus d'etre identiques sur toute instance derivee de la meme image). `-A` ne recree que les
# types de cles manquants -- no-op si /etc/ssh/ en a deja (persistance non attendue ici, le
# filesystem du conteneur redemarre a neuf a chaque restart App Service, mais rend la commande
# idempotente si ce jour ce n'est plus le cas).
ssh-keygen -A

/usr/sbin/sshd

exec dotnet ArkCloud.API.dll
