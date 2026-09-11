#!/bin/sh
set -e

# Sprint 6 -- ADR-0010 (rotation arkcloud_app via Kudu). Demarre sshd en arriere-plan pour que la
# console Kudu fonctionne sur ce conteneur Linux custom, puis lance dotnet au premier plan avec
# `exec` -- le process principal (PID 1 du point de vue d'App Service) reste `dotnet
# ArkCloud.API.dll`, comme avant cet ajout, pas sshd. Coherent avec la facon dont App Service suit
# le cycle de vie du conteneur (healthcheck HTTP sur 8080, pas un signal lie a sshd).
/usr/sbin/sshd

exec dotnet ArkCloud.API.dll
