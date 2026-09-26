**RVIP port** of Forays into Norrendrin 0.8.4, upstream
[Forays/ForaysIntoNorrendrin @ `3ed1559`](https://github.com/Forays/ForaysIntoNorrendrin/tree/3ed15593a1e41aefd3694946490df3646c2278de)
(Derrick Creamer; 0.8.4 released 2015-11-06, plus the author's later
clean-ups). Play: https://ruzzoli.de/roguelikes/forays/
Our changes: https://github.com/memmaker/forays/compare/3ed1559...master

Forays into Norrendrin is an original roguelike (no ancestor game) by Derrick
Creamer, 2011–2015, written in C#: tactical combat, no classes and no
experience points; shrines raise skills and give feats, every character
carries the same five weapons and three armors. The game's own readme is the
upstream README (http://forays.github.io/); the in-game help is in
`Forays/ForaysHelp/`.

What this port adds:
- **.NET WebAssembly port** of the console build: `web/wasm/ForaysWeb.csproj`
  (`Microsoft.NET.Sdk.WebAssembly`, browser-wasm, Mono interpreter, trimmed).
  `Forays/Term.cs` replaces `System.Console`; the runtime runs in a Web
  Worker (`web/worker.js`) and waits for keys with `Atomics.wait` on a
  SharedArrayBuffer, so the game loop is unchanged. `web/coi-sw.js` adds the
  COOP/COEP headers this needs. Saves, options and high scores live in the
  browser's IndexedDB; autosave every two minutes and on tab switch.
- **Windows**: Map, Character, Messages, Inventory, Visible, Equipment
  (shared `rvip-wm.js`), text mode in the game's own colours.
- **Stairs** `>` walks to the stairs you have seen and takes them; explore
  `x` is the game's own.
- **Enter menu** with every command; **item cursor** in every item prompt
  (`Forays/Rvip.cs`).
- **Sound** (off by default): short effects synthesized at build time
  (`web/make-sounds.py`).
- Fixes (`port:` commits): save/load (broken upstream), a file-name crash in
  the character dump, and a Mono wasm array bug in `PosArray`.

Controls: numpad / arrows / `hjklyubn` move; `x` explore, `>` stairs,
Enter command menu, `i` inventory, `e` equipment, `Tab` look, `r` rest,
`?` help, `q` quit or save. The Help button opens the game guide.

Build: `sh web/build.sh` → `web/dist` (needs the .NET 10 SDK, see
`web/toolchain.sh`, and python3; the guide reads
`~/Desktop/Games/Roguelikes/Docs`, the page code `~/Games/rvip-tools/web/`).
Deploy: `sh web/deploy.sh`. Notes: `HANDOVER.md`.

Credits: Forays into Norrendrin © 2011–2015 Derrick Creamer, MIT licence
(`LICENSE.txt`). Web port: memmaker.
