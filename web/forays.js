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
let cv, ctx, px = 16, cw = 9, ch = 19, zoom = 0;

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
const hex = v => '#' + (v & 0xffffff).toString(16).padStart(6, '0');
function fit() {
	const b = $('full');
	let best = 8;
	for (let p = 8; p <= 48; p++) {
		ctx.font = p + 'px ' + FONT;
		if (Math.ceil(ctx.measureText('M').width) * COLS <= b.clientWidth && Math.ceil(p * 1.2) * ROWS <= b.clientHeight) best = p;
	}
	return best;
}
function layout() {
	px = Math.max(8, fit() + zoom);
	ctx.font = px + 'px ' + FONT;
	cw = Math.ceil(ctx.measureText('M').width); ch = Math.ceil(px * 1.2);
	const w = cw * COLS, h = ch * ROWS;
	cv.width = w * dpr; cv.height = h * dpr; cv.style.width = w + 'px'; cv.style.height = h + 'px';
	const b = $('full');
	cv.style.left = Math.max(0, (b.clientWidth - w) / 2) + 'px'; cv.style.top = Math.max(0, (b.clientHeight - h) / 2) + 'px';
	dirty = true;
}
function draw() {
	requestAnimationFrame(draw);
	if (!dirty || !scr) return;
	dirty = false;
	ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
	ctx.font = px + 'px ' + FONT; ctx.textBaseline = 'middle'; ctx.textAlign = 'center';
	for (let r = 0; r < ROWS; r++) for (let c = 0; c < COLS; c++) {
		const i = (r * COLS + c) * 3, x = c * cw, y = r * ch;
		ctx.fillStyle = hex(scr[i + 2]); ctx.fillRect(x, y, cw, ch);
		const code = scr[i];
		if (code > 32) { ctx.fillStyle = hex(scr[i + 1]); ctx.fillText(String.fromCharCode(code), x + cw / 2, y + ch / 2 + 1); }
	}
	if (cur.vis) { ctx.fillStyle = '#c0c0c0'; ctx.fillRect(cur.col * cw, cur.row * ch + ch - 3, cw, 2); }
}

/* ---------- worker ---------- */
function onMessage(e) {
	const m = e.data;
	switch (m.t) {
	case 'screen':
		scr = m.cells; cur = { row: m.row, col: m.col, vis: m.vis };
		try { info = m.info ? JSON.parse(m.info) : {}; } catch (_) { info = {}; }
		dirty = true;
		if (!running) { running = true; status(''); $('game').hidden = false; layout(); }
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

/* ---------- top bar ---------- */
function bar() {
	$('btn-zoom-in').onclick = () => { zoom++; layout(); };
	$('btn-zoom-out').onclick = () => { zoom--; layout(); };
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
	get info() { return info; },
	get running() { return running; },
	get ended() { return ended; },
};

async function main() {
	cv = $('full').querySelector('canvas'); ctx = cv.getContext('2d');
	bar();
	if (!await isolate()) { status('This browser cannot run the game here (no cross-origin isolation / SharedArrayBuffer).', true); return; }
	db = await openDB();
	const files = await allFiles();
	ring = new Int32Array(new SharedArrayBuffer(4 * (2 + NSLOT * SLOT)));
	worker = new Worker('worker.js', { type: 'module' });
	worker.onmessage = onMessage;
	worker.onerror = e => status('The game crashed: ' + e.message + ' — reload the page.', true);
	worker.postMessage({ t: 'init', ring: ring.buffer, files });
	window.addEventListener('keydown', onKey);
	window.addEventListener('resize', layout);
	window.addEventListener('beforeunload', e => { if (running) { e.preventDefault(); e.returnValue = ''; } });
	requestAnimationFrame(draw);
}
main();
