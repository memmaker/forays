/*
 * Forays into Norrendrin: the .NET runtime (web/wasm, browser-wasm) runs in
 * this module worker, so the game loop can block. Keys arrive through a
 * SharedArrayBuffer ring written by forays.js; waitKey() sleeps in
 * Atomics.wait until one is there. Screen and files go to the page by
 * postMessage. JS decides nothing here: it only moves bytes.
 */
import { dotnet } from './_framework/dotnet.js';

const SLOT = 48, NSLOT = 64;          /* ring: [0] written, [1] read, then NSLOT slots of SLOT ints */
let ring, nap, files = {};

function takeKey() {
	const r = Atomics.load(ring, 1), s = 2 + (r % NSLOT) * SLOT, n = ring[s];
	let str = '';
	for (let i = 0; i < n; i++) str += String.fromCharCode(ring[s + 1 + i]);
	Atomics.store(ring, 1, r + 1);
	return str;
}
const imports = {
	present(view, row, col, vis, info) {
		const cells = view.slice();
		postMessage({ t: 'screen', cells, row, col, vis, info }, [cells.buffer]);
	},
	keyAvailable() { return Atomics.load(ring, 0) !== Atomics.load(ring, 1); },
	waitKey(ms) {
		for (;;) {
			const w = Atomics.load(ring, 0);
			if (w !== Atomics.load(ring, 1)) return takeKey();
			postMessage({ t: 'wait' });
			Atomics.wait(ring, 0, w, ms < 0 ? Infinity : ms);
			if (ms >= 0 && Atomics.load(ring, 0) === Atomics.load(ring, 1)) return '';
		}
	},
	sleep(ms) { Atomics.wait(nap, 0, 0, ms); },
	storeFile(name, view) { postMessage({ t: 'store', name, data: view.slice() }); },
	deleteFile(name) { postMessage({ t: 'delete', name }); },
	initialFiles() { return Object.keys(files).join('\n'); },
	initialFile(name) { return files[name]; },
	quit() { postMessage({ t: 'quit' }); },
	sound(name) { postMessage({ t: 'sound', name }); },
	beacon(q) { postMessage({ t: 'beacon', q }); },
};

onmessage = async (e) => {
	if (e.data.t !== 'init') return;
	ring = new Int32Array(e.data.ring);
	nap = new Int32Array(new SharedArrayBuffer(4));
	files = e.data.files || {};
	try {
		const rt = await dotnet.withConfig({ disableIntegrityCheck: true }).create();
		rt.setModuleImports('forays', imports);
		postMessage({ t: 'started' });
		await rt.runMain(rt.getConfig().mainAssemblyName, []);
		postMessage({ t: 'exit' });
	} catch (err) {
		postMessage({ t: 'crash', msg: String(err && (err.stack || err.message) || err) });
	}
};
