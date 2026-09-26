#!/usr/bin/env python3
"""Writes the in-page game guide (dist/help.html) for the Forays web build,
in the shape of BOSS's make-help.py output (RVIP W6).

The Mac's Docs tree (~/Desktop/Games/Roguelikes/Docs, build-docs.py +
guides.py) is not available in the cloud run, so this file is
self-contained: the key list is parsed from the game's own help
(Forays/ForaysHelp/help.txt), the rest is written here. On the Mac, move the
GAMES/GUIDES parts into build-docs.py / guides.py and import them as BOSS does.

  python3 web/make-help.py            -> help fragment (for dist/help.html)
  python3 web/make-help.py --page     -> standalone page (docs/web/forays-docs.html)
"""
import html, os, re, sys

HERE = os.path.dirname(os.path.abspath(__file__))
HELP = os.path.join(HERE, '..', 'Forays', 'ForaysHelp', 'help.txt')
esc = html.escape
UPSTREAM = '3ed15593a1e41aefd3694946490df3646c2278de'


def kbd(k):
    parts = re.split(r'(\+)', k) if len(k) > 1 and '+' in k.strip('+') else [k]
    return ''.join('<span class="plus">+</span>' if p == '+' else f'<kbd>{esc(p)}</kbd>' for p in parts)


def commands():
    """(key, description) pairs from the command list in help.txt, two per line."""
    out, on = [], False
    for line in open(HELP, encoding='utf-8'):
        line = line.rstrip('\r\n')
        if line.startswith('Command list'):
            on = True
            continue
        if not on:
            continue
        if line.startswith('Inventory [i]') or line.startswith('(You can also'):
            break
        for m in re.finditer(r'(\S+(?: \S+)?) : (.+?)(?=\s{2,}\S+(?: \S+)? : |$)', line):
            key, desc = m.group(1).strip(), m.group(2).strip()
            if key.startswith('Alt+Enter'):
                continue
            out.append((key, desc))
    return out


KEY_HINTS = [
    ('?', 'In-game help: overview, skills, feats, spells, items, tips'),
    ('x', 'Explore automatically until something happens'),
    ('Enter', 'Menu of all commands'),
    ('i', 'Inventory with a cursor: letter = use, Shift+letter = drop, Enter = all actions'),
    ('>', 'Take the stairs down (from anywhere: walks to the stairs you have seen)'),
    ('q', 'Quit or save (the browser also saves by itself)'),
]

ESSENTIALS = [
    ('Moving', [('1-9 / arrows / hjklyubn', 'Walk, attack by walking into a monster'), ('5 / .', 'Wait a turn'),
                ('x', 'Explore'), ('X', 'Travel to a place'), ('>', 'Stairs down (walks there first)'), ('w', 'Walk in a direction')]),
    ('Fighting', [('e', 'Equipment: switch weapon and armor'), ('1-5', 'Switch weapon (top row)'), ('8 9 0', 'Switch armor (top row)'),
                  ('s', 'Shoot the bow'), ('z', 'Cast a spell'), ('r', 'Rest (once per level)')]),
    ('Items', [('i', 'Inventory'), ('a', 'Apply (use) an item'), ('f', 'Fling an item'), ('g', 'Pick up'), ('d', 'Drop'), ('\\', 'Known item types')]),
    ('Looking', [('Tab', 'Look around'), ('m', 'Dungeon map'), ('p', 'Previous messages'), ('c', 'Character sheet and feats'), ('t', 'Torch on/off')]),
]

ABOUT = '''<p><strong>Forays into Norrendrin</strong> (Derrick Creamer; version 0.8.4, 2015) is a
streamlined fantasy roguelike about tactical combat. There are no classes and no
experience points: you get stronger by finding the <em>shrines</em> on each
level (combat, defense, magic, spirit, stealth), which raise a skill and let you
pick feats. Every character carries the same five weapons (sword, mace,
dagger, staff, bow) and three armors and switches between them; weapons pick up
enchantments and armors get damaged on the way. Light matters: your torch
shows you the dungeon and shows you to the monsters. The descent is one way,
20 levels deep, and ends at the demons' pit.</p>'''

SAVING = '''<ul>
<li><strong>Saving is automatic.</strong> The game is stored in this browser (IndexedDB) while it waits for your command, every two minutes and when you switch tabs. Reloading the page and choosing <em>Resume saved game</em> continues from there.</li>
<li><kbd>q</kbd> offers the game's own choices: save and return to the menu, save and quit, abandon the character.</li>
<li>When your character dies or is abandoned, the save is deleted: death is final. High scores, options and your name stay.</li>
<li><em>Export save</em> downloads <code>forays.sav</code>; <em>Import save</em> loads one; <em>New game</em> deletes the save in this browser.</li>
<li>Private/incognito windows and "clear site data" delete the stored game. Export first if it matters.</li>
</ul>'''

TIPS = '''<ul>
<li>Rest (<kbd>r</kbd>) once per level to heal fully and repair your equipment — use it after the big fight, not before.</li>
<li>Find every shrine before you go down: the game warns you when one is left. Skills are the only way to grow.</li>
<li>Switch weapons for the job: sword crits take half a foe's maximum health, the mace ignores armor and knocks back, the dagger always hits in darkness, the staff swaps places and trips, the bow hits from afar.</li>
<li>Put your torch away (<kbd>t</kbd>) to sneak past sleeping monsters; take it out to fight things that hide in the dark.</li>
<li>Unknown potions and scrolls are identified by using them; try them when you are healthy and alone.</li>
<li>Terrain is a weapon: doors, corridors, fire, oil, webs and chasms change fights. Look around with <kbd>Tab</kbd>.</li>
</ul>'''

GUIDE = [
    ('Your first minutes', '''<ol>
<li>Start a game from the menu with <kbd>a</kbd>, type a name (or Enter for a random one).</li>
<li>Read the tip boxes: they explain each thing the first time it happens.</li>
<li>Press <kbd>x</kbd> to explore. It stops when something shows up; press it again to go on.</li>
<li>Walk into monsters to attack. When hurt, finish the level's fights and rest with <kbd>r</kbd>.</li>
<li>Step on a shrine (<code>_</code>) to raise a skill, then press <kbd>&gt;</kbd> to walk to the stairs and go down.</li>
</ol>'''),
    ('Health and danger', '''<p>Health does not come back by itself: resting once per level, bandages and
potions are all you have. Watch the status column: bleeding, poison, burning and
stuns show there. Retreating into a corridor so only one monster reaches you is
often the best move.</p>'''),
    ('Equipment', '''<p>You never find new weapons, only better versions of the ones you carry:
enchantments (e.g. <em>of echoes</em>) and damage (weak point, dulled, stuck…) show on the
equipment screen <kbd>e</kbd>; resting repairs it. Full plate protects most but
clanks with every step.</p>'''),
]

WEB = '''<ul>
<li><strong>Windows:</strong> Map, Character, Messages, Inventory and Visible (monsters and items in view) are separate windows; Equipment can be added from <em>Windows ▾</em>. Drag title bars to rearrange, drag the gaps to resize, hover a title for rename and text size. <em>One window</em> shows the original 88×28 screen.</li>
<li>The newest message or question is shown in a box at the top left of the map; the full history is in Messages.</li>
<li><strong>Keys:</strong> the numeric keypad, arrow keys or <kbd>h j k l y u b n</kbd> move you. Browsers keep some shortcuts (<kbd>Ctrl+W</kbd>, <kbd>Ctrl+T</kbd>, <kbd>F5</kbd>…) for themselves.</li>
<li>No <code>[more]</code> stops: messages go on to the Messages window.</li>
<li>Sound effects are off by default; switch them on with <em>Sound</em> in the top bar.</li>
<li>The game runs in a Web Worker; the first visit installs a small service worker so it can (it reloads once).</li>
<li>If the game ever crashes, a message appears at the top; reload the page to continue from the last autosave.</li>
</ul>'''

VERSION = (f'<h2 id="h-version">About this version</h2><ul>'
           f'<li>Based on <strong>Forays into Norrendrin 0.8.4</strong> by Derrick Creamer (MIT licence), '
           f'upstream source <a href="https://github.com/Forays/ForaysIntoNorrendrin/tree/{UPSTREAM}">Forays/ForaysIntoNorrendrin @ {UPSTREAM[:7]}</a> '
           f'(0.8.4 plus the author\'s later clean-ups).</li>'
           f'<li>Our changes (web backend replacing the console, stairs walking, command menu, inventory cursor, '
           f'window layout, autosave, save/load fixes): <a href="https://github.com/memmaker/forays">memmaker/forays</a>, '
           f'<a href="https://github.com/memmaker/forays/compare/{UPSTREAM[:7]}...master">compare view</a>.</li>'
           f'<li>Built with .NET (browser-wasm); text mode, the game\'s own colours.</li></ul>')


def dl(items):
    return '<dl>' + ''.join(f'<dt>{kbd(k)}</dt><dd>{esc(d)}</dd>' for k, d in items) + '</dl>'


def section(anchor, title, body):
    return f'<h2 id="h-{anchor}">{esc(title)}</h2>{body}'


def fragment():
    toc = [('about', 'About the game'), ('keys', 'Keyboard controls'), ('saving', 'Saving your game'),
           ('tips', 'Tips'), ('guide', "New player's guide"), ('web', 'Playing in the browser'), ('version', 'About this version')]
    parts = ['<p>Tactical fantasy roguelike: no classes, no experience — shrines, feats and five weapons you always carry.</p>'
             '<ul class="toc">' + ''.join(f'<li><a href="#h-{a}">{esc(t)}</a></li>' for a, t in toc) + '</ul>']
    parts.append(section('about', 'About the game', ABOUT))
    all_keys = commands() + [('Enter', 'Menu of all commands'), ('<', 'There are no up stairs (says so)')]
    ess = ''.join(f'<div class="box"><h3>{esc(cat)}</h3>{dl(items)}</div>' for cat, items in ESSENTIALS)
    full = ''.join(f'<div>{kbd(k)}<span>{esc(d)}</span></div>' for k, d in all_keys)
    parts.append(section('keys', 'Keyboard controls',
                         '<div class="box key"><h3>The keys to remember</h3>' + dl(KEY_HINTS) + '</div>'
                         '<h3>Essential keys</h3><div class="grid">' + ess + '</div>'
                         '<details><summary>Complete key list (' + str(len(all_keys)) + ' commands)</summary>'
                         '<div class="all">' + full + '</div></details>'))
    parts.append(section('saving', 'Saving your game', SAVING))
    parts.append(section('tips', 'Tips', TIPS))
    parts.append(section('guide', "New player's guide", ''.join(f'<h3>{esc(t)}</h3>{b}' for t, b in GUIDE)))
    parts.append(section('web', 'Playing in the browser', WEB))
    parts.append(VERSION)
    return '\n'.join(parts)


def page():
    """Standalone docs page (docs/web/forays-docs.html) with the page's help styles."""
    idx = open(os.path.join(HERE, 'index.html'), encoding='utf-8').read()
    css = re.search(r'/\* Help: the game guide \*/(.*?)</style>', idx, re.S).group(1)
    css = css.replace('#help-body', 'main')
    return ('<!DOCTYPE html>\n<html lang="en"><head><meta charset="utf-8">'
            '<meta name="viewport" content="width=device-width, initial-scale=1">'
            '<title>Forays into Norrendrin — guide</title><style>'
            ':root{--bg:#0b0b0d;--panel:#16161a;--line:#2b2b33;--text:#d8d8de;--dim:#8a8a96;--accent:#d9b24c}'
            'body{margin:0;background:var(--bg);color:var(--text);font:14px/1.6 system-ui,-apple-system,"Segoe UI",sans-serif}'
            'main{max-width:980px;margin:0 auto;padding:8px 24px 40px}'
            'kbd{display:inline-block;min-width:1.7em;padding:0 6px;text-align:center;font:600 12px/1.6 ui-monospace,Menlo,monospace;'
            'background:#22222a;border:1px solid #3a3a46;border-bottom-width:2px;border-radius:4px}'
            'a{color:var(--accent)}h1{color:var(--accent);font-size:22px}'
            + css + '</style></head><body><main><h1>Forays into Norrendrin</h1>'
            '<p><a href="https://ruzzoli.de/roguelikes/forays/">Play in the browser</a></p>\n'
            + fragment() + '\n</main></body></html>\n')


if __name__ == '__main__':
    print(page() if '--page' in sys.argv else fragment())
