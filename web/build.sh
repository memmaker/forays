#!/bin/sh
# Build Forays into Norrendrin for the browser into web/dist:
# .NET 10 SDK, browser-wasm (web/wasm/ForaysWeb.csproj), no workload needed.
# Toolchain: web/toolchain.sh. Test: web/test.mjs. Deploy: web/deploy.sh.
set -e
cd "$(dirname "$0")/.."
OUT=web/dist
PUB=web/wasm/bin/Release/net10.0/publish/wwwroot
rm -rf "$OUT" "$PUB/.." && mkdir -p "$OUT"
dotnet publish web/wasm/ForaysWeb.csproj -c Release -nologo -v q | grep -v "^$" || true
[ -f "$PUB/_framework/dotnet.js" ] || { echo "publish failed"; exit 1; }
cp -r "$PUB/_framework" "$OUT/"
cp web/index.html web/forays.js web/worker.js web/coi-sw.js rvip/web/rvip-wm.js rvip/web/rvip-sound.js "$OUT/"
[ -f web/make-help.py ] && python3 web/make-help.py > "$OUT/help.html" || true
du -sh "$OUT"
