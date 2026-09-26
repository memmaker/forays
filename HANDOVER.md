# Forays into Norrendrin 0.8.4: handover

## Cloud experiment (read this first)

This repo runs the RVIP import in a Claude Code **cloud** session. Everything
the procedure normally takes from sibling folders on the maintainer's Mac is
bundled under `rvip/`:

- `rvip/RVIP.md` — the procedure (snapshot; the canonical copy lives on the
  Mac). **Write lessons into `rvip/LESSONS.md`** (new file, one bullet per
  lesson naming the RVIP section it belongs to); never edit `rvip/RVIP.md`.
- `rvip/web/rvip-wm.js`, `rvip/web/rvip-sound.js` — shared page code every
  game loads (window manager, sound). Use, don't fork.
- `rvip/templates/boss/` — a text-mode (80x25 cell grid) web port that does
  NOT use Emscripten: `web/boss.js` (cell grid drawing, key queue, WASI shim,
  IndexedDB mirror of the save folder), `web/index.html`, `web/test.mjs`
  (headless node test), `bcrt.pas` (the game-side screen buffer), `HANDOVER.md`.
  Use it as the model for the page: the game keeps an 80x25 (or larger)
  cell buffer with colours and pane info, the page only blits cells and
  sends keys.
- `rvip/templates/hack-HANDOVER.md` — another text game's handover, for the
  windows/panes layout ideas (map, messages, status, inventory panes).

**The game.** Forays into Norrendrin 0.8.4 by Derrick Creamer, C#
(github.com/Forays/ForaysIntoNorrendrin, upstream history is in this repo,
remote `upstream`; our commits go on top on `main`). `ConsoleForays.sln` /
`Forays/ConsoleForays.csproj` is the pure console build (the other build,
`Forays.csproj`, adds an OpenTK/OpenGL window with a tileset). **Port the
console build.** Case O (see RVIP.md Part O: no C# example exists yet; you
write the `### O-Forays` lessons).

**Route to the browser (decide, record in the handover):** the only real
options are the .NET WebAssembly runtime (dotnet 8/9 `wasm-browser` /
`Microsoft.NET.Sdk.WebAssembly` "browser-wasm" with `[JSImport]/[JSExport]`
interop, workload `wasm-tools` + `wasm-experimental`) or NativeAOT-LLVM for
wasm. Prefer the .NET wasm runtime. Replace the game's console I/O layer
(`Console.*`, its screen/colour helpers, `Console.ReadKey`) with one web
backend class that keeps the cell buffer, exposes it to JS, and takes keys
from a queue fed by JS (async: the game loop must await keys, or run the game
on a worker thread if the runtime allows blocking; document which). Saves:
the game's save file → browser IndexedDB (mirror as BOSS does, or the
runtime's own FS with a sync). RVIP's "the game decides, JS only blits" rule
holds: explore, menus, colours, panes are decided in C#.

**Differences from a local run**
- No browser pane and no ruzzoli.de deploy key. Stages 1–6 are in scope.
  Tests: a headless node run where the runtime allows, plus Playwright/
  Chromium (`npx playwright install chromium`) driving the page: screenshots
  into `web/shots/`, read the cell grid via a JS hook. Stage 5's "page live"
  becomes "page serves from `web/dist` with `python3 -m http.server` and
  passes the Playwright check". Write `web/deploy.sh` like BOSS's; never run it.
- Toolchain: install the .NET SDK (8 or 9) and the wasm workloads in the
  cloud VM; record every command in `web/toolchain.sh` so the Mac can
  reproduce it. If the .NET wasm route cannot be installed at all, write
  `web/toolchain.sh` with what you tried and the errors, commit, push, stop.
- Commit after every stage (`RVIP: stage N <topic>`) and **push to `origin`**
  (github.com/memmaker/forays, private). Every commit message ends with
  `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`. Never force-push.
- The Docs page (stage 6) is not here: write `docs/web/forays-docs.html` in
  the shape of BOSS's `make-help.py` output and note it for the Mac side.
- Tiles: the console build is text; the graphical build ships a tileset
  under `Forays/` (check). One tile set per game, ≥95% coverage of every
  drawable thing or text mode; record the count and the decision.
- ASan has no C# equivalent: run the native console build (dotnet) with a
  scripted key feed for a few thousand random keys per seed, several seeds,
  save → load, and treat any unhandled exception as a bug to fix in a
  separate `port:` commit.

## RVIP progress

### Stage 1 — build (done)

- Case **O** (C#, own console layer; nearest: BOSS = screen buffer + key queue).
  Branch `master`, remote `origin` (memmaker/forays). Upstream = history up to
  `3ed1559` (0.8.4 + upstream's post-release refactors).
- **Route:** .NET **browser-wasm** (Mono interpreter), SDK 10.0.112 (`apt
  install dotnet-sdk-10.0`; 8.0 works too), no workload needed.
  `web/toolchain.sh` has every command (dot.net install script is blocked in the
  cloud, apt works). Build: `sh web/build.sh` → `web/dist` (6.4 MB, trimmed,
  `TrimMode=full`). Project: `web/wasm/ForaysWeb.csproj` compiles
  `Forays/*.cs` + `web/wasm/WebBackend.cs`, defines `CONSOLE;WEB`, embeds
  `ForaysHelp/*.txt`, `options.txt`, `highscore.txt`. OpenTK is still
  referenced (NuGet `OpenTK.Next`, managed types only; GL code never runs).
- **Blocking input:** the runtime runs in a **module Web Worker**
  (`web/worker.js`); keys go through a SharedArrayBuffer ring written by the
  page, `waitKey` blocks in `Atomics.wait`. So the game loop stays synchronous
  (no async rewrite). Needs cross-origin isolation: `web/coi-sw.js` (service
  worker adding COOP/COEP, scope = game folder) makes plain static hosting work
  (tested with `python3 -m http.server`); nginx could send the headers instead.
- **Frontend file:** `Forays/Term.cs` replaces `System.Console`
  (`Console.` → `Term.` in Screen/Input/Main, `Thread.Sleep` → `Term.Sleep`,
  `Global.Quit` → `Term.Quit`). The game's own `Screen.memory` (88×28,
  Forays colours) is the cell buffer; `Term.Present()` converts colours with
  the game's `Colors.ConvertColor` (GL palette) and hands `(char, fg, bg)` per
  cell to the backend at every key wait / sleep. Keys: browser `code`/`key`
  → `ConsoleKeyInfo` in `Term.FromBrowser` (C#; printable chars through the
  game's own `Input.GetChar` table, so any keyboard layout works).
  `Term.AtCommandPrompt` exists for the W4 prompt line (not set yet).
- **Files:** runtime MEMFS; `WebBackend.SyncFiles()` mirrors `forays.sav`,
  `options.txt`, `highscore.txt`, `keys.txt`, `name.txt` to IndexedDB
  database `/forays/files` (page `web/forays.js`) on every key wait; the page
  hands them back at start. Save → reload → "Resume saved game" tested.
- **Page:** `web/index.html` (BOSS template bar), `web/forays.js` (one canvas,
  whole screen, stage 5 adds the windows), loads `rvip-wm.js` and
  `rvip-sound.js` from `rvip/web/` (copied by build.sh).
- **Tests:** `node web/test.mjs [--shot name] keys…` (Playwright 1.56.1 +
  the image's Chromium, fresh profile, prints the screen via
  `window.forays.text()`, `random:N:seed` = random keys in the browser,
  `reload`). Screenshots in `web/shots/` (ignored by git).
- **ASan substitute:** `web/native/` = same sources + `Headless.cs` backend with
  random keys; `sh web/native/run-seeds.sh first count keys` runs seeds, each
  followed by save → load in a new process. 12×3000 + 30×5000 keys + loads,
  clean after the fixes. Fixes (separate `port:` commits): save/load broken
  upstream (reader expected an unwritten block; names, item flavours and
  "tried" never saved → crash on first redraw after load), character-dump file
  name with `/` crashed, and the wasm-only `ArrayTypeMismatchException` in
  `PosArray` (generic 2-D array store under the Mono interpreter, net8 and
  net10) → PosArray keeps a 1-D array.
- **Tiles:** 0 sprites. The graphical build (`Forays.csproj`, OpenTK) only
  renders the same glyphs from font sheets (`ForaysImages/font*.png`, 11
  fonts + logo), there is no tileset → **text mode** (no tiles/text switch).
- Quirks: the game flushes pending keys before tips/"press any key" screens
  (`Input.FlushInput`), so scripted tests need waits after screens that pop
  tips. Upstream already has **explore on `x`** and travel commands (stage 2:
  check `<`/`>`).

Next: stage 2 (explore + stairs).
