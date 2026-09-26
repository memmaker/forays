#!/bin/sh
# RVIP ASan substitute: random-key runs, each followed by save -> load.
# usage: sh web/native/run-seeds.sh [first-seed] [count] [keys]
set -e
HERE=$(cd "$(dirname "$0")" && pwd)
GAME="$HERE/../../Forays"
DLL="$HERE/bin/Release/net10.0/ForaysNative.dll"
[ -f "$DLL" ] || dotnet build -c Release "$HERE" >/dev/null
first=${1:-1}; count=${2:-10}; keys=${3:-3000}
fail=0
s=$first
while [ $s -lt $((first+count)) ]; do
  d=$(mktemp -d)
  cp -r "$GAME/ForaysHelp" "$GAME/options.txt" "$GAME/highscore.txt" "$d/"
  (cd "$d" && timeout 600 dotnet "$DLL" $s $keys save) || fail=1
  if [ -f "$d/forays.sav" ]; then
    (cd "$d" && timeout 600 dotnet "$DLL" $((s+100000)) $keys save) || fail=1
  fi
  rm -rf "$d"
  s=$((s+1))
done
exit $fail
