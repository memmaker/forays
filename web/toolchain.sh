#!/bin/sh
# Toolchain for the Forays web port, as installed in the RVIP cloud run
# (Ubuntu 24.04, 2026-09-26). Reproduce on the Mac with the macOS lines.
#
# What worked: .NET SDK 10 (10.0.112) from Ubuntu's archive; the web build is
# Microsoft.NET.Sdk.WebAssembly, RuntimeIdentifier browser-wasm (Mono
# interpreter). NO workload is needed (no wasm-tools / wasm-experimental: no
# native relink, no AOT); the runtime pack comes from nuget.org at restore.
# .NET 8 (8.0.131) builds and runs too (TargetFramework net8.0), same bugs.
#
# What failed in the cloud VM:
#   curl https://dot.net/v1/dotnet-install.sh  -> builds.dotnet.microsoft.com
#   is blocked by the egress proxy (403); apt's dotnet-sdk packages worked.
set -e
if [ "$(uname)" = Darwin ]; then
	# Mac run (2026-09-26): the official script into ~/.dotnet (no sudo; the
	# brew cask needs an admin password). Got SDK 10.0.401, no workload needed.
	# web/build.sh adds ~/.dotnet to PATH when dotnet is not found.
	curl -sSLO https://dot.net/v1/dotnet-install.sh
	bash dotnet-install.sh --channel 10.0 --install-dir "$HOME/.dotnet"
	export PATH="$HOME/.dotnet:$PATH" DOTNET_ROOT="$HOME/.dotnet"
	brew install node python3
else
	apt-get update
	apt-get install -y dotnet-sdk-10.0      # (dotnet-sdk-8.0 also works; set net8.0)
fi
dotnet --list-sdks
# NuGet packages (restored by the builds): OpenTK.Next 1.1.1616.8959 (the game
# references it; only its managed types are used in the web build),
# System.Drawing.Common 10.0.0 / 8.0.10, Microsoft.NETCore.App.Runtime.Mono.browser-wasm.
dotnet restore web/wasm/ForaysWeb.csproj
dotnet restore web/native/ForaysNative.csproj
# Headless browser tests: Playwright 1.56.1 (matches Chromium build 1194 that
# the cloud image ships under /opt/pw-browsers; on the Mac run
# `npx playwright install chromium` and set CHROME to its path, or unset it).
(cd web && npm install)
