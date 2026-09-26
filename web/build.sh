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
cp web/index.html web/forays.js web/worker.js web/coi-sw.js rvip/web/rvip-wm.js rvip/web/rvip-sound.js "$OUT/"
python3 web/make-help.py > "$OUT/help.html"
python3 web/make-help.py --page > docs/web/forays-docs.html
python3 web/make-sounds.py "$OUT"
du -sh "$OUT"
