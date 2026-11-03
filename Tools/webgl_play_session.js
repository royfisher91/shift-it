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
    const h = { 'Content-Type': types[path.extname(file).toLowerCase()] || 'application/octet-stream' };
    // Unity can ship its payload gzipped; say so or the browser will not unpack it.
    if (file.endsWith('.gz')) h['Content-Encoding'] = 'gzip';
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
 * Decide what to press, from the same facts a player can see.
 * Returns 'L', 'R', or null for "keep holding".
 */
function decide(st) {
  if (st.phase !== PHASE.PLAYER) return null;

  // A green dud must NOT be thrown - holding it to the end is worth points.
  if (st.bomb === BOMB.DUD) return null;

  // Which slots can actually be thrown at right now.
  const open = [];
  if (st.slots[0] === 0) open.push(0);
  if (st.slots[1] === 0) open.push(1);
  if (open.length === 0) return null;            // both blocked: wait for one to open

  // Aim for a PERFECT: throw once the remaining fuse has fallen into the green band. Accept
  // "already below the band" too, because a dropped frame must not turn into holding to zero.
  const inBand = st.fuse <= st.bandHi && st.fuse >= st.bandLo;
  const belowBand = st.fuse < st.bandLo;
  if (!inBand && !belowBand) return null;        // too early, keep holding

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

    for (let run = 1; run <= RUNS; run++) {
      // --- start the run ---
      st = await cdp.state();
      for (let i = 0; i < 40 && st && st.phase !== PHASE.PLAYER; i++) {
        if (st.phase === PHASE.TITLE || (st.phase === PHASE.OVER && !st.locked)) await cdp.tap('L');
        await sleep(200);
        st = await cdp.state();
      }
      if (!st || st.phase === PHASE.TITLE) {
        summary.errors.push('run ' + run + ': never started');
        continue;
      }

      // --- play it ---
      const t0 = Date.now();
      const wantEarly = run === 1 && !earlyDone;
      while (Date.now() - t0 < 180000) {
        st = await cdp.state();
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

        const press = decide(st);
        if (press) { await cdp.tap(press); await sleep(120); }
        else await sleep(40);
      }
      if (wantEarly && framesEarly > 0) earlyDone = true;

      // --- read the result the game itself reported ---
      await sleep(900);
      const ended = await cdp.evaluate('JSON.stringify(window.shiftItRuns || [])');
      const list = JSON.parse(ended || '[]');
      const last = list.length ? list[list.length - 1] : null;
      const rec = last
        ? { run: run, passes: last.passes, score: last.score, reason: last.reason }
        : { run: run, passes: st ? st.passes : -1, score: st ? st.score : -1, reason: 'UNREPORTED' };
      summary.runs.push(rec);
      console.log('  run ' + String(run).padStart(2) +
                  '  passes=' + String(rec.passes).padStart(3) +
                  '  score=' + String(rec.score).padStart(4) +
                  '  ' + rec.reason);

      // Clear the lockout before the next retry press.
      for (let i = 0; i < 30; i++) {
        st = await cdp.state();
        if (st && !st.locked) break;
        await sleep(200);
      }
    }

    // --- report ---
    const ok = summary.runs.filter((r) => r.passes >= 0);
    const passes = ok.map((r) => r.passes).sort((a, b) => a - b);
    const mean = passes.length ? passes.reduce((a, b) => a + b, 0) / passes.length : 0;
    const median = passes.length ? passes[Math.floor(passes.length / 2)] : 0;
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
    console.log('RESULT  runs=' + ok.length + '/' + RUNS +
                '  mean=' + summary.stats.meanPasses +
                '  median=' + summary.stats.medianPasses +
                '  range=' + summary.stats.minPasses + '-' + summary.stats.maxPasses);
    console.log('FRAMES  early=' + framesEarly + '  late=' + framesLate);
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
