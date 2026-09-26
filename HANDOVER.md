# Forays into Norrendrin 0.8.4: handover

## Cloud experiment (read this first)

The RVIP import (stages 1–6) ran in a Claude Code **cloud** session. What
the procedure normally takes from sibling folders on the maintainer's Mac
(the RVIP.md snapshot, the shared page code `rvip-wm.js` / `rvip-sound.js`,
the BOSS and Hack templates) was bundled in the private `memmaker/forays-cloud`
repo; its lessons were merged into the Mac's RVIP.md and this public history
leaves the bundle out. The build takes `rvip-wm.js` / `rvip-sound.js` from
`~/Games/rvip-tools/web/`, as the other games do.

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
  `rvip-sound.js` (copied by build.sh from `~/Games/rvip-tools/web/`).
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

### Stage 2 — explore + stairs (done)

- **Explore key `x`** is upstream's own (`Actor.FindAutoexplorePath`, the
  `case 'x'` in `Actor.InputHuman`, continued by the `path` walker at the top
  of `InputHuman`). It already meets RVIP step 2: path over known map,
  one step per turn, stops on any message (`MessageBuffer.Add` →
  `Interrupt`), on keys (`Input.KeyIsAvailable` in the walker) and before
  dangerous steps (`NextStepIsDangerous`: known traps, fire, etc.); walks
  through closed doors (opening them). Documented in the in-game help
  (`ForaysHelp/help.txt`) and the bottom command bar.
- **Stairs:** Forays has only stairs down (one-way). `>` on the stairs takes
  them as before; elsewhere it walks to the stairs you have seen (upstream
  path code, no more "Travel to the stairs? (y/n)" prompt) and takes them on
  arrival. A disturbance (`Actor.Interrupt`, except the arrival message)
  cancels; `>` again resumes. `<` prints "There are no up staircases in
  Forays: the only way is down." Code: `Forays/Rvip.cs` (`Rvip.CommandKey`,
  the main-loop hook = the command read in `Actor.InputHuman`,
  `Rvip.stairs_walk`), `Actor.cs` `case '>'` and `Interrupt()`. Help text
  updated.
- **Tests used:** native scripted feed — `web/native` Headless reads
  `SCRIPT` (keys; `~` Enter, `` ` `` Escape), `GOD=1` (test-only
  invulnerability), `TRACE=1` (per key: tile, walk flag, top rows),
  `DUMP=1` (final screen). Seed 6: `x`×200 then `>` walks to the stairs and
  asks the game's own questions ("Really take the stairs without resting",
  shrine warning), then "You walk down the stairs". Browser:
  `node web/test.mjs --seed 6 …` gives the same level (page URL `?seed=N`
  seeds `R`), explore runs there.
- **"Known grid" test:** the explorer and the walk use the game's own
  `tile.seen` / `GetPath(…, known only)`; unseen stairs → "You don't see any
  stairs here."
- Open: `[more]` stops exist ("You walk down the stairs. [more]") → RVIP 3d
  auto_more, do in stage 3.

### Stage 3 — Enter menu + inventory (done)

- **Enter menu:** `Rvip.CommandMenu()` in `Forays/Rvip.cs`, opened from
  `Rvip.CommandKey` (Enter at the command prompt; Enter did nothing there
  before). Five groups as in the help's command list (Look/rest/fight, Items,
  Moving incl. explore `x` and stairs `>`, Information, Game), key next to
  each. Arrows / numpad 8 2 9 3, Enter / 5 / 6 / Space choose, a command's own
  key runs it, Escape / 0 / . close. Box sized to content (longest entry +
  border + one space), scrolls when taller than the screen (it is: 32 lines on
  a 28-row screen, footer "v more"). The chosen command is returned as the
  command key (direct, no key queue). No mouse yet (the page sends none).
- **Inventory:** the cursor lives in upstream's `Actor.GetItemSelection` /
  `SelectItem`, so every item prompt ("Apply which item?", fling, drop…) has
  it: `Rvip.DrawCursor` (dark-blue row), arrows / numpad 8 2 move,
  Enter / 5 / 6 choose. In the `i` list (`Rvip.inv_mode`): letter = main
  action (apply — Forays items are all consumables), Shift+letter drops,
  Ctrl+letter examines, Enter / Space = the item box with every action and its
  key ([a]pply [f]ling [d]rop, upstream's `UI.ItemDescriptionBox`), numpad
  `+` `-` `*`, 0 / . / 4 close, any other key closes and runs as a command
  (`Term.Push`, new: a pushed key is read before the backend's). After an
  action from the list it reopens unless a monster is in view
  (`Rvip.reopen_inventory`). `ItemSelection.action` carries the choice.
- **Equipment `e`:** upstream's own screen, not changed (weapons/armour
  swap with letters); no cursor added.
- **auto_more (3d):** `Rvip.auto_more = true` → `MessageBuffer.DisplayLines`
  skips the `[more]` key wait; messages stay in the log (`p`, stage 5 log
  window).
- Help: `ForaysHelp/help.txt` mentions the Enter menu and the inventory keys.
- Tests: native `SCRIPT=" aT~ ia"` (bandages applied, list reopened),
  `" aT~ i~"` (item box), `" aT~ ~"` (menu); browser screenshots
  `web/shots/s3-inv.png`, `s3-menu.png`; random keys 10×3000 + loads clean.

### Stage 4 — tiles (done: text mode)

- **Count:** 0 tiles. Forays has no tileset anywhere in its history
  (`git log --all -- '*.png'`: only `ForaysImages/font*.png` + `logo.png`).
  The graphical build (`Forays.csproj`) draws the same characters from bitmap
  font sheets (`font8x16.png` = 1152×16 = 128 glyphs of 8 px + 1 px gap).
  The game only ever draws ASCII (no char > 127 in the source), so a text
  cell grid covers **100 %** of drawable things.
- **Decision: text mode**, no tiles/text switch (like BOSS/ZAPM). No fallback
  set: RVIP says ask before a foreign set, and none is bundled; nobody to ask
  in the cloud run. Colours are the game's GL palette (`Colors.ConvertColor`).
- Possible later polish (not done): draw the game's own `font8x16.png`
  glyphs (integer scale, nearest-neighbour, tinted per colour) instead of the
  browser's monospace font — the look of the OpenGL build.

### Stage 5 — web page (done, not deployed)

- **Windows** (`web/index.html`, `web/forays.js`, shared `rvip-wm.js`):
  Map, Character (the game's left column), Messages (log), Inventory,
  Visible — on by default; Equipment via the Windows drop-down. One/multi
  window switch, drag/resize/rename/A−/A+, layout + zoom + font saved in
  IndexedDB (`/forays/files`, key `web-layout.json`), survives reload
  (tested). One window = the whole 88×28 screen in the map window.
- **Game-side (W0):** `Rvip.Info()` (Forays/Rvip.cs) builds the page state
  as JSON on every key wait (`Term.Present(true)`): `panes` (the game's
  screen regions: map+command bar `[3,21,25,67]`, side `[0,0,28,21]`,
  message rows `[0,21,3,67]`), `full` (whole-screen view: set by
  `Screen.Blank()` and `UI.DisplayCharacterInfo`, cleared by
  `UI.DisplayStats()`; the page then shows the whole screen over the
  windows), `prompt` (live message rows, not the dark-grey old ones →
  `RvipWM.prompt.text`), `atCmd` (`Term.AtCommandPrompt`, set around the
  command read in `Rvip.CommandKey` → `RvipWM.prompt.wait`), `hero` (map
  cell → `RvipWM.center`), `log` new lines / `logReplace` (the game's own
  "(xN)" folding) → `RvipWM.log`, `inv` / `equip` with the items' own
  colours (`Colors.ResolveColor(item.color)`, weapons by
  `EnchantmentColor()`), `vis` (monsters `CanSee`, items on seen tiles) →
  `RvipWM.visible`. Info errors are caught (never crash the game).
- **Camera:** map pane centred on the player when zoomed (tested: zoom +5,
  canvas margin follows the hero), clamped at the edges.
- **Persistence (W5):** save/options/high scores/keys/name mirrored to IDB;
  **autosave**: the page sends a `RvipSave` pseudo-key every 2 min and when
  the tab is hidden; the game saves only at the command prompt with no keys
  pending (`Rvip.Autosave`: same state as the `q` save, also writes
  options), then goes on. A finished run (death/abandon) deletes
  `forays.sav` (Main.cs, after the game loop). Tested: autosave → reload →
  "Resume saved game" → same game. Export / Import / New game buttons.
  Quit → "Play again" overlay.
- **Hosting:** needs SharedArrayBuffer → `coi-sw.js` service worker adds
  COOP/COEP (scope = the game folder), so plain static hosting works
  (`sh web/serve.sh` = `python3 -m http.server` on `web/dist`; all tests use
  that). `web/deploy.sh` written like BOSS's, with the dirty/unpushed guard,
  **never run** (no deploy key in the cloud). Target
  `https://ruzzoli.de/roguelikes/forays/`. Optional nginx COOP/COEP lines
  are in its comment.
- Tests: `node web/test.mjs` with `click:<css>`, `eval:<js>`, `reload`,
  `--keep` (persistent profile); screenshots `s5-multi`, `s5-help` (whole
  screen over windows), `s5-single`, `s5-zoom`.
- Not done: mouse (the game has MouseUI for its GL build; the page sends no
  mouse events), link preview (5b, needs the index card), beacon (stage 9).

### Stage 6 — docs + sound (done; Docs tree merge is for the Mac)

- **Help button guide:** `web/make-help.py` → `dist/help.html` (build.sh),
  same sections as BOSS's: about, keys to remember (help `?`, explore `x`,
  Enter menu, inventory, stairs `>`, save `q`), essentials, complete key
  list (parsed from the game's `ForaysHelp/help.txt`), saving (web), tips,
  new player's guide, playing in the browser, About this version (upstream
  Forays/ForaysIntoNorrendrin @ `3ed1559`, memmaker/forays compare view).
  Weapon/armor claims checked against the game's own `Weapon.Description()`.
- **Docs page:** (cloud) `docs/web/forays-docs.html` = `make-help.py --page`; replaced on the Mac by the Docs entry `forays.html` (stage 7)
  (standalone, same content). The Mac's `~/Desktop/Games/Roguelikes/Docs`
  (build-docs.py GAMES entry + guides.py) is not in the cloud: **Mac side**
  moves the ESSENTIALS/KEY_HINTS/TIPS/GUIDE blocks into build-docs.py /
  guides.py and switches make-help.py to import them like BOSS's.
- **Sound (6b):** Forays has no sounds; `web/make-sounds.py` synthesizes 7
  short WAVs into `dist/sound/` at build time (no third-party assets). The
  game names them (`Rvip.Sound` → `ITermBackend.Sound` → worker `sound` →
  `RVIPSound.play`): `hit` (player damages a monster), `hurt`, `death`,
  `pickup`, `stairs`, `spell`, `levelup` (hooks in `Actor.TakeDamage`,
  death, "You walk down the stairs", the 4 pick-ups, `CastSpell`,
  `IncreaseSkill`). Top-bar **Sound** toggle, **off by default**, saved in
  the layout file (IndexedDB). Tested: toggled on → `hurt` played.
  **No music** (none exists; not synthesized) — open.

### Stage 7 — publish (done, Mac)

- **Mac check** (browser pane, own tab, local server): new game, windows,
  explore, `>` walk (cancelled by a monster), `<` message, Enter menu,
  inventory cursor, autosave → reload → "Resume saved game", Help. All fine;
  no game fix needed. The pane runs **no service worker on local http**
  (`coi-sw.js` registration fails), so local tests use a server that sends
  COOP/COEP itself; on https://ruzzoli.de the service worker works.
  Named keys in scripted tests need `code` as well as `key` (`Term.FromBrowser`).
- **Toolchain (Mac):** .NET SDK 10.0.401 via `dotnet-install.sh --channel 10.0
  --install-dir ~/.dotnet` (no sudo, no workload); `build.sh` adds `~/.dotnet`
  to PATH. Recorded in `web/toolchain.sh`.
- **Docs:** entry `forays.html` in `~/Desktop/Games/Roguelikes/Docs`
  (`build-docs.py` GAMES + `parse_forays` reading `ForaysHelp/help.txt`,
  `guides.py` GUIDES + SAVING); `web/make-help.py` imports it like
  FrogComposband's → `dist/help.html`.
- **Repos:** the cloud repo (with the `rvip/` bundle) is now private
  **memmaker/forays-cloud** (`~/Games/forays-cloud`). This folder is the
  public **memmaker/forays** (remote `memmaker`, branch `master`, remote
  `upstream`): same history minus `rvip/` (`git filter-repo`), `build.sh` takes
  `rvip-wm.js` / `rvip-sound.js` from `~/Games/rvip-tools/web/`.
- **Live:** https://ruzzoli.de/roguelikes/forays/ (`sh web/build.sh && sh
  web/deploy.sh`, guard as Zangband's). Card on https://ruzzoli.de/roguelikes/
  (`forays.png`: 48×10 cells of the web grid drawn with the game's
  `ForaysImages/font8x16.png`), tree: standalone original after Brogue
  (`li.insp`, 2011 · Derrick Creamer; year from `LICENSE.txt` 2011–2015 — the
  web check was not possible, RogueBasin unverified). og block in
  `web/index.html` (by hand, image `roguelikes/forays.png`).

Next: stage 8 (shrine). Templates: `~/Games/roguelikes-index/shrine/frogcomposband.html`
and its commit `e1775e7`. Material:
- Manual/help: the game's own `Forays/ForaysHelp/help.txt` (overview,
  command list), `advanced_help.txt`, `feat_help.txt`, `spell_help.txt`,
  `item_help.txt` (also under `?` in the game); the web guide
  (`dist/help.html`, Docs `forays.html`); upstream README → http://forays.github.io/.
- Licence: MIT, `LICENSE.txt` (© 2011–2015 Derrick Creamer).
- Changelog: no file; version history only in upstream commit messages
  (e.g. `2015-11-06 Version 0.8.4. Logo added. Monster balance changes.`,
  `git log 3ed1559`), plus forays.github.io.
- Walkthrough: none known; the in-game tips and the Docs new-player guide are
  what exists. Report as missing unless the web has one.
- Links to add: Info button on the card, ✦ in the tree entry, game-title link
  on the shrine page to `../forays/`; shrine page gets its own og block
  (image `roguelikes/forays.png`).

### Stage 8 — shrine (done)

- **Shrine:** https://ruzzoli.de/roguelikes/shrine/forays.html
  (`~/Games/roguelikes-index/shrine/forays.html` + `shrine/forays/`:
  `manual.html` = the five upstream `ForaysHelp/*.txt` + README at `3ed1559`,
  `changelog.txt` = RogueBasin dates + GitHub release notes 0.8.3/0.8.4 +
  upstream commit log, `license.txt` = `LICENSE.txt`). Hand-written og block.
  Links: card Info button, tree ✦, game-title link (already in
  `web/index.html` since stage 7) all live. Phone width (375 px) checked.
- **Lineage (verified):** RogueBasin: development began October 2011, first
  release 0.5.0 on 28 January 2012, 0.8.4 on 6 November 2015 (GitHub release
  2015-11-07 UTC), development stopped 2016; influences ADOM, DoomRL, Angband,
  Brogue. Card and tree year corrected 2011 → 2012. Credits from
  forays.github.io (Derrick S. Creamer; site by Tommy Ettinger/notostraca;
  logo L.C. Smith/Soundlust).
- **Missing:** no changelog file in the game (built from releases + git log);
  no walkthrough or strategy guide found (page gives rules of thumb from the
  help and links RogueBasin, forays.github.io). The manual exists (help files).
- **Cheats:** the `~` debug menu is behind `if(false)` in `Actor.cs`:
  unreachable in every build.

Next: stage 9 (graveyard + leaderboard).

### Stage 9 — graveyard + leaderboard (done)

- **Beacon hook (game decides):** `Rvip.Beacon(depth, turn)` (Forays/Rvip.cs), called in
  `Main.cs` right after the game loop ends (`if(!Global.SAVING)`, next to `recentcause`),
  so death, win and "abandon character" all pass one place; save & quit sends nothing.
  `ev` = `win` if `Global.BOSS_KILLED` (both win paths: last demon/circle in Map.cs,
  boss "ripe old age" in Actor.cs), `quit` if `KILLED_BY == "gave up"`, else `death`.
  Path to the page: `ITermBackend.Beacon` → `WebBackend` JSImport `beacon` → worker
  `postMessage({t:'beacon'})` → `forays.js` → `RvipWM.report(q)` (fetch fallback).
- **Fields:** `g=forays`, `ev`, `name` (`Actor.player_name`, the game asks for it),
  `killer` (death only: `Rvip.killer` = `dmg.source` set in `Actor.TakeDamage`'s death
  branch → monster `Name.Singular`; no monster source → `KILLED_BY` minus "killed by "
  and a/an/the, e.g. "slamming into the wall"), `depth` (`M.Depth`), `score` = depth
  (the game's high score list ranks by depth, W for a win), `turns` = `Q.turn / 100`.
  **Missing:** `lvl` (Forays has no character level, only skills).
- "Quit game immediately" (`q` → d) sends nothing: it exits without ending the run,
  and the web autosave keeps the character resumable.
- **Killer art:** `forays()` in `roguelikes-index/killers/make.py`: Actor.cs `Define()`
  glyph from `ForaysImages/font8x16.png` (as the card), GL palette colour, 2x in 32 px;
  80 PNGs live under `/roguelikes/killers/forays/`.
- **Fix:** `web/coi-sw.js` rebuilt every response with its body; a 204 (the beacon's
  answer) then throws → "Failed to fetch", report stayed in the outbox forever. Null-body
  statuses now pass `null`.
- **Tests:** native `SCRIPT=" aT~ qcy" TRACE=1` → `BEACON …ev=quit`; random seeds →
  `ev=death&killer=goblin|lone%20wolf|…`; a temporary patch (case 2 sets BOSS_KILLED,
  reverted) → `ev=win`. Live (browser pane): quit → `ev=quit&name=t&depth=1&score=1&turns=0&id=…&at=…`,
  random keys → `ev=death&name=die&killer=goblin&depth=1&score=1&turns=30&id=…`, outbox
  emptied after the SW fix (204). Test IDB `/forays/files` deleted from a plain page.
