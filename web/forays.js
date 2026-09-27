/*
 * Forays into Norrendrin in the browser. The game (C#, .NET browser-wasm) runs
 * in worker.js and hands over its finished 88x28 cell buffer (char, fg, bg per
 * cell, colours decided by the game); this page only blits cells and forwards
 * keys. Files the game writes (save, options, high scores) are mirrored to
 * IndexedDB ('/forays/files'). Structure follows the BOSS page (boss.js).
 */
const COLS = 88, ROWS = 28;
const FONT = '"DejaVu Sans Mono", Menlo, Consolas, "Liberation Mono", monospace';
const DB = '/forays/files';
const SLOT = 48, NSLOT = 64;
const $ = id => document.getElementById(id);
const dpr = Math.max(1, Math.min(3, window.devicePixelRatio || 1));

let worker, ring, db, running = false, ended = false;
let scr = null, cur = { row: 0, col: 0, vis: false }, info = {}, dirty = true;
let ctx, px = 16;

function status(msg, isError) {
	const s = $('status');
	s.textContent = msg; s.hidden = !msg; s.classList.toggle('error', !!isError);
}

/* ---------- cross-origin isolation (SharedArrayBuffer) ---------- */
async function isolate() {
	if (window.crossOriginIsolated) return true;
	if (!('serviceWorker' in navigator)) return false;
	if (sessionStorage.getItem('forays-coi')) { sessionStorage.removeItem('forays-coi'); return false; }
	sessionStorage.setItem('forays-coi', '1');
	await navigator.serviceWorker.register('coi-sw.js');
	await navigator.serviceWorker.ready;
	location.reload();
	return new Promise(() => {});
}

/* ---------- IndexedDB mirror of the game's files ---------- */
function openDB() {
	return new Promise((ok, bad) => {
		const r = indexedDB.open(DB, 1);
		r.onupgradeneeded = () => r.result.createObjectStore('files');
		r.onsuccess = () => ok(r.result);
		r.onerror = () => bad(r.error);
	});
}
function tx(mode, f) {
	return new Promise((ok, bad) => {
		const t = db.transaction('files', mode), s = t.objectStore('files'), out = f(s);
		t.oncomplete = () => ok(out && out.result);
		t.onerror = () => bad(t.error);
	});
}
async function allFiles() {
	const out = {};
	await new Promise((ok, bad) => {
		const t = db.transaction('files', 'readonly'), c = t.objectStore('files').openCursor();
		c.onsuccess = () => { const k = c.result; if (k) { out[k.key] = new Uint8Array(k.value); k.continue(); } };
		t.oncomplete = ok; t.onerror = () => bad(t.error);
	});
	return out;
}
const putFile = (name, data) => tx('readwrite', s => s.put(data, name));
const delFile = name => tx('readwrite', s => s.delete(name));
const getFile = name => tx('readonly', s => s.get(name));

/* ---------- keys ---------- */
function sendKey(code, key, mods) {
	const str = code + '\t' + key + '\t' + mods;
	const w = Atomics.load(ring, 0);
	if (w - Atomics.load(ring, 1) >= NSLOT) return;          /* full: drop */
	const s = 2 + (w % NSLOT) * SLOT, n = Math.min(str.length, SLOT - 1);
	ring[s] = n;
	for (let i = 0; i < n; i++) ring[s + 1 + i] = str.charCodeAt(i);
	Atomics.store(ring, 0, w + 1);
	Atomics.notify(ring, 0);
}
const PASS = new Set(['F5', 'F11', 'F12']);
function onKey(e) {
	if (!running || !$('help').hidden) return;
	if (e.target && (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA')) return;
	if (['Shift', 'Control', 'Alt', 'Meta', 'CapsLock', 'NumLock', 'Dead'].includes(e.key)) return;
	if (e.metaKey || PASS.has(e.code)) return;
	e.preventDefault();
	sendKey(e.code, e.key, (e.shiftKey ? 's' : '') + (e.ctrlKey ? 'c' : '') + (e.altKey ? 'a' : ''));
}

/* ---------- drawing ---------- */
/* The game (Rvip.Info) names the panes of its 88x28 screen: map (with the
 * command bar under it), side (character column), msg (message rows -> the
 * prompt line). Multi-window: each pane in its window, the whole screen over
 * them while the game shows a whole-screen view (info.full). One window: the
 * whole screen in the map window. */
const hex = v => '#' + (v & 0xffffff).toString(16).padStart(6, '0');
let wm = null, rects = {}, L = { px: 0, font: 13, wm: null }, auto = true, saveT = 0;
const one = () => !rects.side && !rects.inv && !rects.msg && !rects.vis && !rects.equip;
/* fonts: the top-bar choice (L.face) for the text panes, the map's own (L.mapFace, on its title bar) for the map */
let face = FONT;
function faceOf(n) { return n ? '"' + n + '", ' + FONT : FONT; }
function metrics(p) { ctx.font = p + 'px ' + face; return [Math.ceil(ctx.measureText('M').width), Math.ceil(p * 1.2)]; }
function fitPx(w, h, C, R) {
	let best = 8;
	for (let p = 8; p <= 48; p++) { const [a, b] = metrics(p); if (a * C <= w && b * R <= h) best = p; }
	return best;
}
function size(c, w, h, p) {
	if (c.width !== Math.round(w * dpr) || c.height !== Math.round(h * dpr)) { c.width = Math.round(w * dpr); c.height = Math.round(h * dpr); }
	c.style.width = w + 'px'; c.style.height = h + 'px';
	const g = c.getContext('2d');
	g.setTransform(dpr, 0, 0, dpr, 0, 0); g.font = p + 'px ' + face; g.textBaseline = 'middle'; g.textAlign = 'center';
	return g;
}
/* cells r0..r0+R, c0..c0+C of the screen into canvas c at font size p */
function blit(c, p, r0, c0, R, C) {
	const [w, h] = metrics(p), g = size(c, C * w, R * h, p);
	for (let r = 0; r < R; r++) for (let k = 0; k < C; k++) {
		const i = ((r0 + r) * COLS + c0 + k) * 3, x = k * w, y = r * h;
		g.fillStyle = hex(scr[i + 2]); g.fillRect(x, y, w, h);
		const code = scr[i];
		if (code > 32) { g.fillStyle = hex(scr[i + 1]); g.fillText(String.fromCharCode(code), x + w / 2, y + h / 2 + 1); }
	}
	const cr = cur.row - r0, cc = cur.col - c0;
	if (cur.vis && cr >= 0 && cr < R && cc >= 0 && cc < C) { g.fillStyle = '#c0c0c0'; g.fillRect(cc * w, cr * h + h - 3, w, 2); }
	return [w, h];
}
function paneOf(name) { return (info.panes && info.panes[name]) || { map: [3, 21, 25, 67], side: [0, 0, 28, 21], msg: [0, 21, 3, 67] }[name]; }
function draw() {
	requestAnimationFrame(draw);
	if (!dirty || !scr || !wm) return;
	dirty = false;
	face = faceOf(L.mapFace);
	const full = $('full'), fcv = full.querySelector('canvas'), mcv = $('map').querySelector('canvas');
	if (one() || info.full) {
		/* whole screen: in the map window (one window) or over all windows */
		const box = one() ? $('map') : full, c = one() ? mcv : fcv;
		full.hidden = one();
		const p = auto || info.full ? fitPx(box.clientWidth, box.clientHeight, COLS, ROWS) : px;
		const [w, h] = blit(c, p, 0, 0, ROWS, COLS);
		if (one() && info.hero && !info.full) {
			const m = paneOf('map');
			RvipWM.center(c, (m[1] + info.hero[1] + 0.5) * w, (m[0] + info.hero[0] + 0.5) * h, COLS * w, ROWS * h);
		} else RvipWM.center(c, 0, 0, COLS * w, ROWS * h);
		if (one()) return;
	}
	full.hidden = !info.full;
	const m = paneOf('map'), s = paneOf('side');
	const [w, h] = blit(mcv, px, m[0], m[1], m[2], m[3]);
	if (info.hero) RvipWM.center(mcv, (info.hero[1] + 0.5) * w, (info.hero[0] + 0.5) * h, m[3] * w, m[2] * h);
	else RvipWM.center(mcv, 0, 0, m[3] * w, m[2] * h);
	if (rects.side) {
		face = faceOf(L.face);
		const sb = $('side'), sp = Math.min(px, fitPx(sb.clientWidth, sb.clientHeight, s[3], s[2]));
		const scv = sb.querySelector('canvas'), [sw, sh] = blit(scv, sp, s[0], s[1], s[2], s[3]);
		scv.style.marginTop = '0px'; scv.style.marginLeft = Math.max(0, (sb.clientWidth - s[3] * sw) / 2) + 'px';
	}
}
/* lists the game sends: inventory [letter, glyph, name, colour], equipment [slot, name, colour] */
function list(el, rows, fmt) {
	const k = JSON.stringify(rows);
	if (el._k === k) return;
	el._k = k; el.textContent = '';
	if (!rows.length) { const d = document.createElement('div'); d.className = 'wm-vh'; d.textContent = 'empty'; el.appendChild(d); }
	rows.forEach(r => el.appendChild(fmt(r)));
}
function invRow(r) {
	const d = document.createElement('div'), b = document.createElement('b');
	b.textContent = r[1]; b.style.color = r[3]; d.style.color = r[3];
	d.appendChild(document.createTextNode(r[0] + ') ')); d.appendChild(b); d.appendChild(document.createTextNode(' ' + r[2]));
	return d;
}
function eqRow(r) {
	const d = document.createElement('div');
	if (r[0]) { const h = document.createElement('span'); h.className = 'wm-vh'; h.textContent = r[0] + ': '; d.appendChild(h); }
	else d.style.paddingLeft = '1.5em';
	const n = document.createElement('span'); n.textContent = r[1]; n.style.color = r[2]; d.appendChild(n);
	return d;
}
function update(i) {
	RvipWM.prompt.text(i.full ? '' : i.prompt || '');
	RvipWM.prompt.wait(i.atCmd);
	const log = $('log');
	if (i.logReset) log.textContent = '';
	if (i.log) i.log.forEach((l, n) => RvipWM.log(log, l, n === 0 && i.logReplace));
	const mb = $('msgb'); mb.scrollTop = mb.scrollHeight;   /* newest message in view on every flush */
	log.scrollTop = log.scrollHeight;
	if (i.inv) list($('inv'), i.inv, invRow);
	if (i.equip) list($('equip'), i.equip, eqRow);
	if (i.vis !== undefined) RvipWM.visible($('vis'), i.vis);
}
function fonts() { ['log', 'inv', 'equip', 'vis'].forEach(id => { $(id).style.fontSize = L.font + 'px'; $(id).style.fontFamily = L.face ? faceOf(L.face) : ''; }); dirty = true; }
function saveLayout() { clearTimeout(saveT); saveT = setTimeout(() => putFile('web-layout.json', new TextEncoder().encode(JSON.stringify(L))), 300); }
function autoPx() {
	face = faceOf(L.mapFace);
	const b = $('map'), m = paneOf('map');
	return one() ? fitPx(b.clientWidth, b.clientHeight, COLS, ROWS) : fitPx(b.clientWidth, b.clientHeight, m[3], m[2]);
}
function layout() { if (auto) px = autoPx(); dirty = true; }
async function makeWM() {
	try { const d = await getFile('web-layout.json'); if (d) { const s = JSON.parse(new TextDecoder().decode(d)); L = { px: s.px | 0, font: s.font || 13, wm: s.wm, sound: !!s.sound, face: s.face || '', mapFace: s.mapFace || '' }; } } catch (_) { }
	if (L.px >= 8 && L.px <= 48) { px = L.px; auto = false; }
	fonts(); loadFace(L.face); loadFace(L.mapFace);
	$('chk-sound').checked = !!L.sound;
	wm = RvipWM({
		area: $('game'), menu: $('btn-layout'),
		wins: [{ id: 'map', title: 'Map' }, { id: 'side', title: 'Character' }, { id: 'msg', title: 'Messages' },
			{ id: 'inv', title: 'Inventory' }, { id: 'equip', title: 'Equipment' }, { id: 'vis', title: 'Visible' }],
		multi: { d: 'h', r: 0.74, a: { d: 'h', r: 0.2, a: 'side', b: 'map' }, b: { d: 'v', r: 0.4, a: 'msg', b: { d: 'v', r: 0.55, a: 'inv', b: 'vis' } } },
		single: 'map',
		state: L.wm,
		save: st => { L.wm = st; saveLayout(); },
		layout: r => { rects = r; layout(); renderMapSel(); },
		font: (id, d) => { if (id === 'map' || id === 'side') { zoom(d); return; } L.font = Math.max(8, Math.min(28, L.font + d)); fonts(); saveLayout(); },
		onReset: () => { auto = true; L.px = 0; L.font = 13; L.wm = wm.state(); /* sound and font choices kept */ fonts(); layout(); saveLayout(); renderMapSel(); }
	});
	wm.apply();
	renderMapSel();
}
/* map font select on the Map title bar (the WM rebuilds title bars: re-insert) */
const mapSel = document.createElement('select');
mapSel.title = 'Map font'; mapSel.innerHTML = '<option value="">Default font</option>';
mapSel.addEventListener('pointerdown', e => e.stopPropagation());   /* not a window drag */
mapSel.addEventListener('mousedown', e => e.stopPropagation());
function renderMapSel() {
	const bs = document.querySelector('#t-map .wm-btns');
	if (bs && mapSel.parentNode !== bs) bs.insertBefore(mapSel, bs.firstChild);
	mapSel.value = L.mapFace || '';
}
/* a face from the index page's fonts/ (build.sh lists them in fonts.json) */
function loadFace(n) {
	if (!n) { fonts(); return; }
	const ff = new FontFace(n, 'url(../fonts/' + n + '.woff)');
	ff.load().then(() => { document.fonts.add(ff); fonts(); layout(); }).catch(() => status('Could not load the font ' + n + '.', true));
}
function zoom(d) {
	auto = false;
	px = Math.max(8, Math.min(48, px + d));
	L.px = px; saveLayout(); dirty = true;
}

/* ---------- worker ---------- */
function onMessage(e) {
	const m = e.data;
	switch (m.t) {
	case 'screen':
		scr = m.cells; cur = { row: m.row, col: m.col, vis: m.vis };
		if (m.info) { try { info = JSON.parse(m.info); update(info); } catch (err) { console.error('info', err); } }
		dirty = true;
		if (!running) { running = true; status(''); $('game').hidden = false; wm.apply(); layout(); }
		break;
	case 'sound': if (L.sound) RVIPSound.play([m.name], 0.6); break;
	case 'beacon': // RVIP 12: the game builds the report, the page only sends it
		if (window.RvipWM && RvipWM.report) RvipWM.report(m.q); else fetch('/roguelikes/beacon?' + m.q, { keepalive: true, mode: 'no-cors' }).catch(function () {});
		break;
	case 'store': putFile(m.name, m.data); break;
	case 'delete': delFile(m.name); break;
	case 'quit': case 'exit': gameOver(); break;
	case 'crash': status('The game crashed: ' + m.msg.split('\n')[0] + ' — reload the page.', true); console.error(m.msg); break;
	}
}
function gameOver() {
	if (ended) return;
	ended = true; running = false;
	setTimeout(() => { $('overlay').hidden = false; }, 300);
}
/* autosave (RVIP W5): the game saves only while it waits for a command */
function requestSave() { if (running && !ended && info.atCmd) sendKey('RvipSave', '', ''); }
setInterval(requestSave, 120000);
document.addEventListener('visibilitychange', () => { if (document.hidden) requestSave(); });

/* ---------- top bar ---------- */
function bar() {
	RvipWM.dropdown($('btn-file'), $('menu-file'));
	RvipWM.dropdown($('btn-audio'), $('menu-audio'));
	fetch('fonts.json').then(r => r.json()).then(list => {
		[[$('sel-font'), 'face'], [mapSel, 'mapFace']].forEach(([sel, k]) => {
			list.forEach(n => { const o = document.createElement('option'); o.value = n; o.textContent = n.replace(/^Web(Plus|437)_/, '').replace(/_/g, ' '); sel.appendChild(o); });
			sel.value = L[k] || '';
		});
	}).catch(() => { });
	[[$('sel-font'), 'face'], [mapSel, 'mapFace']].forEach(([sel, k]) => {
		sel.onchange = function () { L[k] = this.value; saveLayout(); loadFace(this.value); layout(); this.blur(); };
	});
	$('btn-restart').onclick = () => location.reload();
	$('btn-new').onclick = async () => {
		if (!confirm('Delete the saved game in this browser?')) return;
		await delFile('forays.sav'); location.reload();
	};
	$('btn-export').onclick = async () => {
		const d = await getFile('forays.sav');
		if (!d) { alert('No saved game yet. Save first (q, then "Save your progress").'); return; }
		const a = document.createElement('a');
		a.href = URL.createObjectURL(new Blob([d])); a.download = 'forays.sav'; a.click();
	};
	$('btn-import').onclick = () => $('import-file').click();
	$('import-file').onchange = async (e) => {
		const f = e.target.files[0]; if (!f) return;
		await putFile('forays.sav', new Uint8Array(await f.arrayBuffer())); location.reload();
	};
	$('btn-help').onclick = openHelp;
	$('chk-sound').onchange = function () { L.sound = this.checked; saveLayout(); this.blur(); };
	$('help-close').onclick = () => { $('help').hidden = true; };
	document.querySelectorAll('#bar button').forEach(b => b.addEventListener('mousedown', e => e.preventDefault()));
}
async function openHelp() {
	$('help').hidden = false;
	const body = $('help-body');
	if (!body.dataset.loaded) {
		try { body.innerHTML = await (await fetch('help.html')).text(); body.dataset.loaded = 1; }
		catch (_) { body.textContent = 'Help is not available.'; }
	}
	body.focus();
}
document.addEventListener('keydown', e => { if (e.key === 'Escape' && !$('help').hidden) { $('help').hidden = true; e.preventDefault(); e.stopPropagation(); } }, true);

/* hooks for tests (web/test.mjs) */
window.forays = {
	text() { if (!scr) return ''; let s = ''; for (let r = 0; r < ROWS; r++) { for (let c = 0; c < COLS; c++) { const k = scr[(r * COLS + c) * 3]; s += k < 32 ? ' ' : String.fromCharCode(k); } s += '\n'; } return s; },
	key: sendKey,
	save: requestSave,
	get layout() { return { rects, px, auto, full: info.full }; },
	get info() { return info; },
	get running() { return running; },
	get ended() { return ended; },
};

async function main() {
	ctx = document.createElement('canvas').getContext('2d');
	bar();
	if (!await isolate()) { status('This browser cannot run the game here (no cross-origin isolation / SharedArrayBuffer).', true); return; }
	db = await openDB();
	await makeWM();
	const files = await allFiles();
	const seed = new URLSearchParams(location.search).get('seed');
	if (seed) files.seed = new TextEncoder().encode(seed);
	ring = new Int32Array(new SharedArrayBuffer(4 * (2 + NSLOT * SLOT)));
	worker = new Worker('worker.js', { type: 'module' });
	worker.onmessage = onMessage;
	worker.onerror = e => status('The game crashed: ' + e.message + ' — reload the page.', true);
	worker.postMessage({ t: 'init', ring: ring.buffer, files });
	window.addEventListener('keydown', onKey);
	window.addEventListener('resize', () => { wm.apply(); layout(); });
	window.addEventListener('beforeunload', e => { if (running) { e.preventDefault(); e.returnValue = ''; } });
	requestAnimationFrame(draw);
}
main();
