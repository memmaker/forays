#!/bin/sh
# Build Forays into Norrendrin for the browser into web/dist:
# .NET 10 SDK, browser-wasm (web/wasm/ForaysWeb.csproj), no workload needed.
# Toolchain: web/toolchain.sh. Test: web/test.mjs. Deploy: web/deploy.sh.
set -e
command -v dotnet >/dev/null || { export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"; }
cd "$(dirname "$0")/.."
OUT=web/dist
PUB=web/wasm/bin/Release/net10.0/publish/wwwroot
rm -rf "$OUT" "$(dirname "$PUB")"
dotnet publish web/wasm/ForaysWeb.csproj -c Release -nologo -v q | grep -v "^$" || true
[ -f "$PUB/_framework/dotnet.js" ] || { echo "publish failed"; exit 1; }
rm -rf "$OUT" && mkdir -p "$OUT"
cp -r "$PUB/_framework" "$OUT/_framework"
cp web/index.html web/forays.js web/worker.js web/coi-sw.js "$OUT/"
python3 web/make-help.py > "$OUT/help.html"
python3 web/make-sounds.py "$OUT"
# text fonts: the index page's fonts/ (served at ../fonts/ next to the games)
FONTS=${FONTS:-$HOME/Games/roguelikes-index/fonts}
(ls "$FONTS" 2>/dev/null | grep '\.woff$' | sed 's/\.woff$//') | python3 -c 'import json,sys; print(json.dumps(sys.stdin.read().split()))' > "$OUT/fonts.json"
du -sh "$OUT"
