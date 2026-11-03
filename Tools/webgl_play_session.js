/**
 * Plays the REAL WebGL build in a real browser for a set number of complete runs, and records
 * frame sequences for two GIFs: one early game, one late game with blockers.
 *
 * Why it reads game state instead of mashing keys:
 *   An earlier balance sweep in this project produced a confident, completely wrong answer because
 *   the bot could read every tell instantly and perfectly. The mirror-image mistake is a browser
 *   driver that reads nothing - it dies at three passes, and the "ten full runs" it reports
 *   describe a harness flailing, not a game being played. So the build publishes its state to the
 *   page (WebBridge.cs / ShiftItBridge.jslib) and this driver consumes it, deciding as a player
 *   does:
 *
 *     - throw while the fuse ring's tip is inside the green band, for a PERFECT
 *     - never throw at a shielded, sleeping or absent robot
 *     - never throw a green DUD: hold it and let it fizzle
 *     - when both sides are blocked, wait for one to open
 *     - respect MIRRORED turns, where left Shift throws right
 *
 *   Input still goes in as genuine browser key events with the correct Shift location, so the thing
 *   under test is the real input path in the real build.
 *
 * Usage:
 *   node Tools/webgl_play_session.js [--runs 10] [--build <dir>] [--out <dir>] [--browser edge]
 */

const { spawn } = require('child_process');
const fs = require('fs');
const http = require('http');
const path = require('path');

const BROWSERS = {
  edge: 'C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe',
  chrome: 'C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe',
};

function arg(name, dflt) {
  const i = process.argv.indexOf('--' + name);
  return i >= 0 && process.argv[i + 1] ? process.argv[i + 1] : dflt;
}

const BUILD = arg('build', 'D:\\KT\\Builds\\ShiftIt\\WebGL');
const OUT = arg('out', 'D:\\KT\\ShiftIt\\Review\\webgl_session');
const WHICH = arg('browser', 'edge');
const PORT = parseInt(arg('port', '9344'), 10);
const RUNS = parseInt(arg('runs', '10'), 10);
const SKILL = arg('skill', 'average');
// Serve the build the way a plain static host does: no Content-Encoding header at all. Unity's
// gzip build then has to unpack itself in JavaScript via the decompression fallback. This is the
// exact condition on itch.io, and the difference between a page that loads and a blank screen.
const NO_ENCODING = process.argv.indexOf('--no-encoding') >= 0;

/**
 * The same read model the in-editor bot uses (SimBot.cs).
 *
 * The first version of this driver read the exact fuse fraction and band bounds off the bridge and
 * threw perfectly every time, with no reaction time and no misreads. It reached a median of 130
 * passes and a best of 290 against a game tuned for 15-35. That number says nothing about the
 * game; it describes a bot nobody has to play against. This project already lost a day to exactly
 * that mistake in Stage 1, so the driver now has to LOOK before it knows, and can still slip.
 */
const SKILLS = {
  perfect: { read: 0.0,  misread: 0.0,  jitter: 0.0 },
  skilled: { read: 0.30, misread: 0.02, jitter: 0.030 },
  average: { read: 0.52, misread: 0.06, jitter: 0.055 },
};
const P = SKILLS[SKILL] || SKILLS.average;

// A small deterministic RNG, so a session can be repeated exactly when chasing something.
let _seed = parseInt(arg('seed', '12345'), 10) >>> 0;
function rnd() {
  _seed = (_seed * 1664525 + 1013904223) >>> 0;
  return _seed / 4294967296;
}
function gauss(sd) {
  const u = Math.max(1e-9, rnd()), v = rnd();
  return Math.sqrt(-2 * Math.log(u)) * Math.cos(2 * Math.PI * v) * sd;
}

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// Mirrors the Phase enum in SimTypes.cs.
const PHASE = { TITLE: 0, PLAYER: 1, OUTGOING: 2, RECEIVER: 3, INCOMING: 4, FAILING: 5, OVER: 6 };
// Mirrors BombKind.
const BOMB = { NORMAL: 0, RED: 1, DUD: 2 };

function serve(root) {
  const types = {
    '.html': 'text/html', '.js': 'application/javascript', '.wasm': 'application/wasm',
    '.data': 'application/octet-stream', '.json': 'application/json', '.css': 'text/css',
    '.png': 'image/png', '.ico': 'image/x-icon', '.ogg': 'audio/ogg',
  };
  const server = http.createServer((req, res) => {
    const rel = decodeURIComponent(req.url.split('?')[0]).replace(/^\/+/, '') || 'index.html';
    const file = path.join(root, rel);
    if (!file.startsWith(root) || !fs.existsSync(file) || fs.statSync(file).isDirectory()) {
      res.writeHead(404); res.end('no'); return;
    }
    // Unity names its compressed payload "<real ext>.unityweb", so the real type is the
    // extension UNDERNEATH that suffix. Serve the wrong type and the loader rejects the wasm.
    let typed = file;
    let gz = false;
    if (typed.endsWith('.unityweb') || typed.endsWith('.gz')) {
      typed = typed.slice(0, typed.lastIndexOf('.'));
      // Only claim gzip if the bytes really are gzip: a build can be made uncompressed and keep
      // the same file names, and a wrong Content-Encoding fails with a blank screen.
      const fd = fs.openSync(file, 'r');
      const magic = Buffer.alloc(2);
      fs.readSync(fd, magic, 0, 2, 0);
      fs.closeSync(fd);
      gz = magic[0] === 0x1f && magic[1] === 0x8b;
    }
    const h = { 'Content-Type': types[path.extname(typed).toLowerCase()] || 'application/octet-stream' };
    if (gz && !NO_ENCODING) h['Content-Encoding'] = 'gzip';
    res.writeHead(200, h);
    fs.createReadStream(file).pipe(res);
  });
  return new Promise((resolve) => {
    server.listen(0, '127.0.0.1', () => resolve({ server, port: server.address().port }));
  });
}

function getJson(url) {
  return new Promise((resolve, reject) => {
    http.get(url, (res) => {
      let b = '';
      res.on('data', (d) => (b += d));
      res.on('end', () => { try { resolve(JSON.parse(b)); } catch (e) { reject(e); } });
    }).on('error', reject);
  });
}

class Cdp {
  constructor(ws) { this.ws = ws; this.id = 0; this.pending = new Map(); }

  send(method, params = {}) {
    const id = ++this.id;
    this.ws.send(JSON.stringify({ id, method, params }));
    return new Promise((resolve, reject) => {
      this.pending.set(id, { resolve, reject });
      setTimeout(() => {
        if (this.pending.has(id)) { this.pending.delete(id); reject(new Error(method + ' timed out')); }
      }, 60000);
    });
  }

  async evaluate(expr) {
    const r = await this.send('Runtime.evaluate', { expression: expr, returnByValue: true });
    return r.result ? r.result.value : undefined;
  }

  async key(type, code, location) {
    await this.send('Input.dispatchKeyEvent', {
      type, key: 'Shift', code, location,
      windowsVirtualKeyCode: 16, nativeVirtualKeyCode: 16,
      modifiers: type === 'keyDown' ? 8 : 0,
    });
  }

  /** A single tap. The game edge-detects, so one tap is exactly one throw. */
  async tap(side, holdMs = 70) {
    const code = side === 'L' ? 'ShiftLeft' : 'ShiftRight';
    const loc = side === 'L' ? 1 : 2;
    await this.key('keyDown', code, loc);
    await sleep(holdMs);
    await this.key('keyUp', code, loc);
  }

  async shotTo(file) {
    const r = await this.send('Page.captureScreenshot', { format: 'png' });
    fs.writeFileSync(file, Buffer.from(r.data, 'base64'));
  }

  /** The live state line published by WebBridge, parsed into names. */
  async state() {
    const s = await this.evaluate('window.shiftItState || ""');
    if (!s) return null;
    const p = s.split('|');
    if (p.length < 18) return null;
    const n = (i) => parseFloat(p[i]);
    return {
      phase: n(0) | 0, passes: n(1) | 0, score: n(2) | 0, best: n(3) | 0, zone: n(4) | 0,
      fuse: n(5), fuseMax: n(6), bandLo: n(7), bandHi: n(8), bomb: n(9) | 0,
      slots: [n(10) | 0, n(11) | 0],
      mirrored: n(12) === 1, bothBlocked: n(13) === 1, openSlot: n(14) | 0,
      opened: n(15) === 1, canSwap: n(16) === 1, locked: n(17) === 1,
    };
  }
}

/**
 * Records the fuse length the RUNNING BUILD reports at each pass count, and the first pass at
 * which it reports a mirrored turn.
 *
 * This exists because the balance numbers live in a JSON TextAsset that Unity LZ4-packs into
 * WebGL.data, so grepping the shipped file for them does not work - and "the source says 0.09"
 * is not evidence about the build. Reading the curve back out of the running game is.
 */
const fuseTrace = new Map();        // normal bombs
const hotTrace = new Map();         // HOT bombs, which carry half the fuse
let firstMirroredPass = null;
let firstHotPass = null;
const BOMB_HOT = 1;                 // BombKind.Hot

function traceFuse(st) {
  if (!st || st.fuseMax <= 0) return;
  // A HOT bomb is given half the fuse (ShiftSim: FuseMax *= 0.5f), so the two kinds must be
  // recorded apart. Mixed together they print as one curve with wild dropouts, which reads as a
  // broken tool rather than as the mechanic it is.
  const hot = st.bomb === BOMB_HOT;
  const map = hot ? hotTrace : fuseTrace;
  if (!map.has(st.passes)) map.set(st.passes, st.fuseMax);
  if (hot && firstHotPass === null) firstHotPass = st.passes;
  if (st.mirrored && firstMirroredPass === null) firstMirroredPass = st.passes;
}

function reportFuseTrace() {
  const rows = [...fuseTrace.entries()].sort((a, b) => a[0] - b[0]);
  if (!rows.length) return;
  const at = (p) => { const r = rows.find((x) => x[0] === p); return r ? r[1] : null; };
  console.log('');
  console.log('FUSE CURVE as reported by the running build:');
  let line = '  ';
  for (const [p, f] of rows) {
    if (p > 40) break;
    line += 'p' + p + '=' + f.toFixed(2) + '  ';
    if (line.length > 96) { console.log(line); line = '  '; }
  }
  if (line.trim()) console.log(line);
  // Two independent points fix the slope; the floor is read where the curve stops falling.
  const a = at(5), b = at(20);
  if (a !== null && b !== null) {
    const perPass = (a - b) / 15;
    console.log('  slope between p5 and p20: ' + perPass.toFixed(4) + ' s per pass');
  }
  const floor = Math.min(...rows.map((r) => r[1]));
  console.log('  normal-bomb floor: ' + floor.toFixed(3) + ' s');
  if (hotTrace.size) {
    const hotFloor = Math.min(...[...hotTrace.values()]);
    console.log('  HOT-bomb floor:    ' + hotFloor.toFixed(3) +
                ' s  <- the tightest demand the game ever makes');
  }
  console.log('  first HOT bomb at pass:      ' +
              (firstHotPass === null ? '(none reached)' : firstHotPass));
  console.log('  first MIRRORED turn at pass: ' +
              (firstMirroredPass === null ? '(none reached)' : firstMirroredPass));
}

/**
 * Per-turn state for the read model: how long this catch has been in the player's hands, and
 * whether the tells were actually taken in.
 */
function newTurnState() {
  return { t0: Date.now(), target: null, read: null };
}

/** Has the player had long enough to take in this turn's tells, and did they? */
function didRead(turn) {
  if (turn.read !== null) return turn.read;
  const elapsed = (Date.now() - turn.t0) / 1000;
  const frac = P.read <= 0 ? 1 : Math.min(1, Math.max(0, elapsed / P.read));
  if (rnd() >= frac) return null;          // still looking; no decision yet
  turn.read = rnd() >= P.misread;          // decided, correctly or not
  return turn.read;
}

/**
 * Decide what to press, from the same facts a player can see.
 * Returns 'L', 'R', or null for "keep holding".
 */
function decide(st, turn) {
  if (st.phase !== PHASE.PLAYER) return null;

  const read = didRead(turn);
  if (read === null) return null;          // has not taken the turn in yet

  // Pick the moment to let go: somewhere in the green band, with a human's jitter on it.
  if (turn.target === null) {
    const mid = (st.bandLo + st.bandHi) / 2;
    turn.target = mid + gauss(P.jitter);
  }
  if (st.fuse > turn.target) return null;  // not yet

  if (!read) {
    // Misread the turn: throw at a side without checking what is standing there.
    return rnd() < 0.5 ? 'L' : 'R';
  }

  // A green dud must NOT be thrown - holding it to the end is worth points.
  if (st.bomb === BOMB.DUD) return null;

  const open = [];
  if (st.slots[0] === 0) open.push(0);
  if (st.slots[1] === 0) open.push(1);
  if (open.length === 0) return null;      // both blocked: wait for one to open

  const slot = open[0];
  // Mirrored turns swap the keys: to reach the left slot you press RIGHT Shift.
  const leftKeyGoesTo = st.mirrored ? 1 : 0;
  return slot === leftKeyGoesTo ? 'L' : 'R';
}

(async () => {
  const exe = BROWSERS[WHICH];
  if (!fs.existsSync(exe)) { console.log('NO BROWSER at ' + exe); process.exit(2); }
  if (!fs.existsSync(path.join(BUILD, 'index.html'))) {
    console.log('NO WEBGL BUILD at ' + BUILD); process.exit(2);
  }
  fs.mkdirSync(OUT, { recursive: true });
  const gifEarly = path.join(OUT, 'gif_early');
  const gifLate = path.join(OUT, 'gif_late');
  fs.mkdirSync(gifEarly, { recursive: true });
  fs.mkdirSync(gifLate, { recursive: true });

  const { server, port } = await serve(BUILD);
  const url = 'http://127.0.0.1:' + port + '/index.html';
  console.log('serving ' + BUILD);
  console.log('  at ' + url);

  const profile = path.join(OUT, '_profile');
  const browser = spawn(exe, [
    '--headless=new',
    '--remote-debugging-port=' + PORT,
    '--user-data-dir=' + profile,
    '--no-first-run', '--no-default-browser-check',
    '--disable-extensions', '--disable-sync',
    '--autoplay-policy=no-user-gesture-required',
    '--window-size=1280,720',
    '--use-gl=angle', '--use-angle=swiftshader',
    url,
  ], { stdio: 'ignore' });

  let cdp = null;
  const summary = { runs: [], errors: [] };
  try {
    // Wait for the debugging endpoint, then attach to the page.
    let targets = null;
    for (let i = 0; i < 60; i++) {
      try {
        const list = await getJson('http://127.0.0.1:' + PORT + '/json/list');
        targets = list.filter((t) => t.type === 'page' && t.webSocketDebuggerUrl);
        if (targets.length) break;
      } catch (e) { /* not up yet */ }
      await sleep(500);
    }
    if (!targets || !targets.length) throw new Error('browser never exposed a page target');

    // Node's own global WebSocket - no 'ws' package to install, which is why the other
    // harness in this folder uses it too.
    const ws = new WebSocket(targets[0].webSocketDebuggerUrl);
    await new Promise((res, rej) => {
      ws.addEventListener('open', res, { once: true });
      ws.addEventListener('error', rej, { once: true });
    });
    cdp = new Cdp(ws);
    ws.addEventListener('message', (m) => {
      let msg;
      try { msg = JSON.parse(m.data); } catch (e) { return; }
      if (msg.id && cdp.pending.has(msg.id)) {
        const { resolve, reject } = cdp.pending.get(msg.id);
        cdp.pending.delete(msg.id);
        if (msg.error) reject(new Error(msg.error.message));
        else resolve(msg.result);
      }
    });
    await cdp.send('Runtime.enable');
    await cdp.send('Page.enable');

    console.log('waiting for the build to boot and publish state...');
    let st = null;
    for (let i = 0; i < 180; i++) {
      st = await cdp.state();
      traceFuse(st);
      if (st) break;
      await sleep(1000);
    }
    if (!st) {
      throw new Error('the build never published state - is WebBridge wired and the jslib in the build?');
    }
    console.log('build is live. phase=' + st.phase);

    // Clear the Sticky Keys tip if it is showing, so the GIFs are clean. It does not block play.
    await cdp.send('Input.dispatchMouseEvent',
                   { type: 'mousePressed', x: 640, y: 690, button: 'left', clickCount: 1 });
    await cdp.send('Input.dispatchMouseEvent',
                   { type: 'mouseReleased', x: 640, y: 690, button: 'left', clickCount: 1 });
    await sleep(300);

    let earlyDone = false, lateDone = false;
    let framesEarly = 0, framesLate = 0;
    // How many run records the page held before this run. The game reports a run end on its
    // GameOver event, which lands AFTER the explosion finishes playing - noticeably later than
    // the Failing phase this loop breaks on. Reading "the last record" after a fixed wait
    // therefore attributed run N-1's result to run N, and left run 1 with none at all.
    let seenRuns = 0;

    for (let run = 1; run <= RUNS; run++) {
      // --- start the run ---
      st = await cdp.state();
      traceFuse(st);
      for (let i = 0; i < 40 && st && st.phase !== PHASE.PLAYER; i++) {
        if (st.phase === PHASE.TITLE || (st.phase === PHASE.OVER && !st.locked)) await cdp.tap('L');
        await sleep(200);
        st = await cdp.state();
      traceFuse(st);
      }
      if (!st || st.phase === PHASE.TITLE) {
        summary.errors.push('run ' + run + ': never started');
        continue;
      }

      // --- play it ---
      const t0 = Date.now();
      const wantEarly = run === 1 && !earlyDone;
      let turn = newTurnState();
      let lastPasses = -1;
      let lastPhase = -1;
      while (Date.now() - t0 < 180000) {
        st = await cdp.state();
      traceFuse(st);
        if (!st) break;
        if (st.phase === PHASE.OVER || st.phase === PHASE.FAILING) break;

        // GIF frames. Early: the opening passes. Late: once blockers are in play.
        if (wantEarly && framesEarly < 70 && st.passes <= 6) {
          await cdp.shotTo(path.join(gifEarly, 'f' + String(framesEarly++).padStart(3, '0') + '.png'));
        } else if (!lateDone && framesLate < 70 && st.passes >= 14 &&
                   (st.slots[0] !== 0 || st.slots[1] !== 0 || st.mirrored || st.bomb !== BOMB.NORMAL)) {
          await cdp.shotTo(path.join(gifLate, 'f' + String(framesLate++).padStart(3, '0') + '.png'));
          if (framesLate >= 70) lateDone = true;
        }

        // A new catch starts a new turn: the tells have to be read again.
        if (st.passes !== lastPasses || (st.phase === PHASE.PLAYER && lastPhase !== PHASE.PLAYER)) {
          turn = newTurnState();
          lastPasses = st.passes;
        }
        lastPhase = st.phase;

        const press = decide(st, turn);
        if (press) { await cdp.tap(press); await sleep(120); turn = newTurnState(); }
        else await sleep(30);
      }
      if (wantEarly && framesEarly > 0) earlyDone = true;

      // --- read the result the game itself reported ---
      // Wait for a NEW record rather than a fixed delay, so each run reports its own result.
      let list = [];
      for (let i = 0; i < 60; i++) {
        const ended = await cdp.evaluate('JSON.stringify(window.shiftItRuns || [])');
        list = JSON.parse(ended || '[]');
        if (list.length > seenRuns) break;
        await sleep(250);
      }
      const fresh = list.length > seenRuns ? list[list.length - 1] : null;
      seenRuns = list.length;
      const rec = fresh
        ? { run: run, passes: fresh.passes, score: fresh.score, reason: fresh.reason }
        : { run: run, passes: st ? st.passes : -1, score: st ? st.score : -1, reason: 'UNREPORTED' };
      summary.runs.push(rec);
      console.log('  run ' + String(run).padStart(2) +
                  '  passes=' + String(rec.passes).padStart(3) +
                  '  score=' + String(rec.score).padStart(4) +
                  '  ' + rec.reason);

      // Clear the lockout before the next retry press.
      for (let i = 0; i < 30; i++) {
        st = await cdp.state();
      traceFuse(st);
        if (st && !st.locked) break;
        await sleep(200);
      }
    }

    // --- report ---
    const ok = summary.runs.filter((r) => r.passes >= 0);
    const passes = ok.map((r) => r.passes).sort((a, b) => a - b);
    const mean = passes.length ? passes.reduce((a, b) => a + b, 0) / passes.length : 0;
    const median = passes.length ? passes[Math.floor(passes.length / 2)] : 0;
    summary.skill = SKILL;
    summary.stats = {
      runs: ok.length,
      meanPasses: +mean.toFixed(1),
      medianPasses: median,
      minPasses: passes.length ? passes[0] : 0,
      maxPasses: passes.length ? passes[passes.length - 1] : 0,
      framesEarly: framesEarly,
      framesLate: framesLate,
    };
    fs.writeFileSync(path.join(OUT, 'session.json'), JSON.stringify(summary, null, 2));

    console.log('');
    console.log('SKILL   ' + SKILL + '  (read ' + P.read + 's, misread ' +
                (P.misread * 100).toFixed(0) + '%, jitter ' + P.jitter + ')');
    console.log('RESULT  runs=' + ok.length + '/' + RUNS +
                '  mean=' + summary.stats.meanPasses +
                '  median=' + summary.stats.medianPasses +
                '  range=' + summary.stats.minPasses + '-' + summary.stats.maxPasses);
    console.log('FRAMES  early=' + framesEarly + '  late=' + framesLate);
    reportFuseTrace();
    if (summary.errors.length) console.log('ERRORS  ' + summary.errors.join('; '));
    console.log((ok.length === RUNS && framesEarly > 0 && framesLate > 0)
      ? 'PLAY SESSION OK' : 'PLAY SESSION INCOMPLETE');
  } catch (e) {
    console.log('PLAY SESSION FAILED: ' + e.message);
    process.exitCode = 1;
  } finally {
    try { if (cdp) await cdp.send('Browser.close'); } catch (e) { /* closing anyway */ }
    try { browser.kill(); } catch (e) { /* already gone */ }
    server.close();
  }
})();
