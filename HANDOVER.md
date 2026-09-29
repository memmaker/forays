# Forays into Norrendrin 0.8.4: handover

All RVIP stages 1-9 done. Live: https://ruzzoli.de/roguelikes/forays/ · repo
https://github.com/memmaker/forays (remote `memmaker`, branch `master`; remote
`upstream` = Forays/ForaysIntoNorrendrin, history up to `3ed1559`) · shrine
https://ruzzoli.de/roguelikes/shrine/forays.html. Stages 1-6 were first done
in a cloud session (the old private `memmaker/forays-cloud` repo is deleted).

## The game

- Forays into Norrendrin 0.8.4 by Derrick Creamer, C#, MIT (`LICENSE.txt`).
  First release 0.5.0 Jan 2012, 0.8.4 Nov 2015. Case O (C#, own console
  layer; RVIP.md 5.3 / 5.5). We port the console build (`ConsoleForays.sln` /
  `Forays/ConsoleForays.csproj`); `Forays.csproj` (OpenTK) only draws the same
  ASCII from font sheets.
- Tiles: none exist → text mode (100 % of drawable things are ASCII), no
  Tiles switch. Colours = the game's GL palette (`Colors.ConvertColor`).
- The `~` debug menu is behind `if(false)` (no cheats).

## Build, test, deploy

- `sh web/build.sh` → `web/dist` (.NET browser-wasm, Mono interpreter, no
  workload; SDK 10 in `~/.dotnet`, commands in `web/toolchain.sh`).
  `web/wasm/ForaysWeb.csproj` compiles `Forays/*.cs` + `WebBackend.cs`, defines
  `CONSOLE;WEB`, `TrimMode=full`. Loads the shared `../rvip-*.js`.
  `web/make-help.py` (imports the Docs entry `forays.html` from
  `~/Desktop/Games/Roguelikes/Docs`) → `dist/help.html`; `web/make-sounds.py`
  synthesizes 7 WAVs (no music exists).
- `sh web/deploy.sh` (dirty/unpushed guard).
- Blocking input: the runtime runs in a module Web Worker (`web/worker.js`),
  keys through a SharedArrayBuffer ring (`Atomics.wait`), so the game loop
  stays synchronous. Needs cross-origin isolation: `web/coi-sw.js` service
  worker adds COOP/COEP (null-body statuses like the beacon's 204 must pass
  `null`). The browser pane runs no service worker on local http: test with a
  server that sends COOP/COEP itself (`web/serve.sh` is a plain server).
- Tests: `node web/test.mjs [--shot name] [--seed N] keys…` (Playwright;
  `random:N:seed`, `reload`, `click:<css>`, `eval:<js>`, `--keep`; named keys
  need `code` as well as `key`). Native substitute for ASan: `web/native/`
  (`Headless.cs`, env `SCRIPT` (`~` Enter, `` ` `` Esc), `GOD=1`, `TRACE=1`,
  `DUMP=1`); `sh web/native/run-seeds.sh first count keys` (each seed then
  save → load). The game flushes keys before tip screens: scripted tests need
  waits.

## Port map

- `Forays/Term.cs` replaces `System.Console` (cell buffer = the game's
  `Screen.memory` 88×28; keys via `Term.FromBrowser` → `ConsoleKeyInfo`).
- `Forays/Rvip.cs`: `CommandKey` (main-loop hook in `Actor.InputHuman`),
  Enter menu `CommandMenu`, inventory cursor in `GetItemSelection`
  (`DrawCursor`, `inv_mode`, `reopen_inventory`), auto_more, `>` walks to the
  seen stairs and stops (`>` again descends; `<` explains there are no up
  stairs), `Info()` JSON per key wait (panes, full screen, prompt, hero, log,
  inv/equip, visible), `Autosave` (page sends `RvipSave` every 2 min / on tab
  hide, saved only at the prompt), `Sound`, `Beacon` (from `Main.cs` after
  the game loop; win = `BOSS_KILLED`, quit = "gave up"; no `lvl`, score = depth).
- Explore `x` is upstream's own autoexplore.
- Files: MEMFS mirrored to IndexedDB `/forays/files` (`forays.sav`,
  `options.txt`, `highscore.txt`, `keys.txt`, `name.txt`, `web-layout.json`).
- Upstream fixes (`port:` commits): save/load (unwritten block, names/flavours
  not saved), dump file name with `/`, wasm `ArrayTypeMismatchException` in
  `PosArray` (now 1-D).

## Open

- No mouse (the page sends none; the GL build's MouseUI unused).
- Equipment `e` screen has no cursor (upstream screen unchanged).
- Possible polish: draw the game's own `font8x16.png` glyphs instead of the
  browser font (the OpenGL build's look).
