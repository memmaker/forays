#!/bin/sh
# Upload web/dist to https://ruzzoli.de/roguelikes/forays/ (RVIP W9).
# Written in the cloud run, NEVER run there: run it on the Mac after
# `sh web/build.sh` from a clean, pushed tree.
# The game needs cross-origin isolation (SharedArrayBuffer): web/coi-sw.js
# adds COOP/COEP from a service worker, so no nginx change is needed. (Faster
# first load: add to the nginx location for /roguelikes/forays/
#   add_header Cross-Origin-Opener-Policy same-origin;
#   add_header Cross-Origin-Embedder-Policy require-corp;  )
set -e
cd "$(dirname "$0")"
# guard (RVIP step 9): refuse a dirty or unpushed tree
if [ -n "$(git status --porcelain)" ]; then echo "deploy: working tree is dirty" >&2; exit 1; fi
git fetch -q origin
if [ -n "$(git log --oneline @{u}.. 2>/dev/null)" ] || ! git rev-parse -q --verify @{u} >/dev/null; then
	echo "deploy: commits not pushed (or no upstream)" >&2; exit 1
fi
[ -f dist/index.html ] && [ -d dist/_framework ] || { echo "deploy: run web/build.sh first" >&2; exit 1; }
ssh ruzzoli.de 'sudo mkdir -p /var/www/ruzzoli.de/roguelikes/forays && sudo chown -R felix:www-data /var/www/ruzzoli.de/roguelikes/forays'
rsync -rtz --delete dist/ ruzzoli.de:/var/www/ruzzoli.de/roguelikes/forays/
