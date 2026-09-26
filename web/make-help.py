#!/usr/bin/env python3
"""Writes the in-page game guide (dist/help.html) for the Forays web build.

The game content comes from the desktop key guides in
~/Desktop/Games/Roguelikes/Docs (build-docs.py + guides.py, entry
forays.html), so both guides stay in sync; only the saving and "playing in
the browser" parts are written here, because they differ on the web."""
import html, importlib.util, os, sys

DOCS = os.path.expanduser('~/Desktop/Games/Roguelikes/Docs')
PAGE = 'forays.html'
UPSTREAM = '3ed15593a1e41aefd3694946490df3646c2278de'

sys.path.insert(0, DOCS)
spec = importlib.util.spec_from_file_location('build_docs', os.path.join(DOCS, 'build-docs.py'))
docs = importlib.util.module_from_spec(spec)
spec.loader.exec_module(docs)
from guides import GUIDES   # noqa: E402

game = next(g for g in docs.GAMES if g['file'] == PAGE)
guide = dict(GUIDES[PAGE])
info = dict(game['info'])
kbd = docs.kbd
esc = html.escape

KEY_HINTS = [
    ('?', 'In-game help: overview, skills, feats, spells, items, tips'),
    ('x', 'Explore automatically until something happens'),
    ('Enter', 'Menu of all commands'),
    ('i', 'Inventory with a cursor: letter = use, Shift+letter = drop, Enter = all actions'),
    ('>', 'Take the stairs down (from anywhere: walks to the stairs you have seen)'),
    ('q', 'Quit or save (the browser also saves by itself)'),
]


SAVING = '''<ul>
<li><strong>Saving is automatic.</strong> The game is stored in this browser (IndexedDB) while it waits for your command, every two minutes and when you switch tabs. Reloading the page and choosing <em>Resume saved game</em> continues from there.</li>
<li><kbd>q</kbd> offers the game's own choices: save and return to the menu, save and quit, abandon the character.</li>
<li>When your character dies or is abandoned, the save is deleted: death is final. High scores, options and your name stay.</li>
<li><em>Export save</em> downloads <code>forays.sav</code>; <em>Import save</em> loads one; <em>New game</em> deletes the save in this browser.</li>
<li>Private/incognito windows and "clear site data" delete the stored game. Export first if it matters.</li>
</ul>'''


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


toc = [('about', 'About the game'), ('keys', 'Keyboard controls'), ('saving', 'Saving your game'), ('tips', 'Tips'),
       ('guide', "New player's guide"), ('web', 'Playing in the browser'), ('credits', 'Credits'), ('version', 'About this version')]
parts = ['<p>' + esc(game['tagline']) + '</p><ul class="toc">' +
         ''.join(f'<li><a href="#h-{a}">{esc(t)}</a></li>' for a, t in toc) + '</ul>']
parts.append(section('about', 'About the game', guide.pop('What makes Forays into Norrendrin special')))
all_keys = game['all']() if callable(game['all']) else game['all']
ess = ''.join(f'<div class="box"><h3>{esc(cat)}</h3>{dl(items)}</div>' for cat, items in game['essentials'])
full = ''.join(f'<div>{kbd(k)}<span>{esc(d)}</span></div>' for k, d in all_keys)
parts.append(section('keys', 'Keyboard controls',
                     '<div class="box key"><h3>The keys to remember</h3>' + dl(KEY_HINTS) + '</div>'
                     '<h3>Essential keys</h3><div class="grid">' + ess + '</div>'
                     '<details><summary>Complete key list (' + str(len(all_keys)) + ' commands)</summary>'
                     '<div class="all">' + full + '</div></details>'))
parts.append(section('saving', 'Saving your game', SAVING))
parts.append(section('tips', 'Tips', info['Tips']))
parts.append(section('guide', "New player's guide", ''.join(f'<h3>{esc(t)}</h3>{b}' for t, b in guide.items())))
parts.append(section('web', 'Playing in the browser', WEB))
parts.append(section('credits', 'Credits', info['Credits']))
parts.append(VERSION)
print('\n'.join(parts))
