/*
 * Confirm the WebGL build works in a real Chromium browser, including LEFT vs RIGHT Shift,
 * by driving the PAGE over the DevTools Protocol instead of the operating system.
 *
 * Roy's brief: "Detect them separately (Unity Input System leftShiftKey / rightShiftKey) and
 * confirm it works in WebGL in Chrome and Edge."
 *
 * Why this and not synthetic OS keystrokes: the first attempt launched a visible browser and sent
 * real VK_LSHIFT presses through keybd_event. Its safety guard then refused to fire, because the
 * foreground window was not the browser - Edge had put a sync dialog and an extension page in
 * front of the game. Had the guard not been there it would have typed Shift into a Windows
 * Security dialog. CDP has none of that exposure: the key event is delivered to the page itself,
 * the browser never needs focus, and nothing can be typed into another application.
 *
 * It also sends GENUINE KeyboardEvent.code values - 'ShiftLeft' with location 1 and 'ShiftRight'
 * with location 2 - which is exactly the signal Unity's Input System reads in a browser. That is
 * the thing under test.
 *
 *   node Tools/webgl_cdp_check.js [--build <dir>] [--out <dir>] [--browser edge|chrome]
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
const OUT = arg('out', 'D:\\KT\\ShiftIt\\Review\\webgl_cdp');
const WHICH = arg('browser', 'edge');
const PORT = parseInt(arg('port', '9333'), 10);

const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

// ---------------------------------------------------------------- static server
function serve(root) {
  const types = {
    '.html': 'text/html', '.js': 'application/javascript', '.json': 'application/json',
    '.wasm': 'application/wasm', '.data': 'application/octet-stream',
    '.css': 'text/css', '.png': 'image/png', '.unityweb': 'application/octet-stream',
  };
  const server = http.createServer((req, res) => {
    const rel = decodeURIComponent(req.url.split('?')[0]).replace(/^\/+/, '') || 'index.html';
    const file = path.join(root, rel);
    if (!file.startsWith(root)) { res.writeHead(403).end(); return; }
    fs.readFile(file, (err, buf) => {
      if (err) { res.writeHead(404).end('not found'); return; }
      const h = { 'Content-Type': types[path.extname(file).toLowerCase()] || 'application/octet-stream' };
      // Unity ships .unityweb as gzip when "decompression fallback" is off; this build has the
      // fallback ON, so the loader handles it and no Content-Encoding header is needed.
      res.writeHead(200, h).end(buf);
    });
  });
  return new Promise((resolve) => {
    server.listen(0, '127.0.0.1', () => resolve({ server, port: server.address().port }));
  });
}

// ---------------------------------------------------------------- CDP
function getJson(url) {
  return new Promise((resolve, reject) => {
    http.get(url, (res) => {
      let d = '';
      res.on('data', (c) => (d += c));
      res.on('end', () => { try { resolve(JSON.parse(d)); } catch (e) { reject(e); } });
    }).on('error', reject);
  });
}

class Cdp {
  constructor(ws) { this.ws = ws; this.id = 0; this.pending = new Map(); this.events = []; }

  static async connect(url) {
    const ws = new WebSocket(url);
    await new Promise((res, rej) => { ws.onopen = res; ws.onerror = rej; });
    const c = new Cdp(ws);
    ws.onmessage = (m) => {
      const msg = JSON.parse(m.data);
      if (msg.id && c.pending.has(msg.id)) {
        const { resolve, reject } = c.pending.get(msg.id);
        c.pending.delete(msg.id);
        msg.error ? reject(new Error(JSON.stringify(msg.error))) : resolve(msg.result);
      } else if (msg.method) {
        c.events.push(msg);
      }
    };
    return c;
  }

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
    const r = await this.send('Runtime.evaluate', { expression: expr, returnByValue: true, awaitPromise: false });
    return r.result ? r.result.value : undefined;
  }

  async shot(name, caption) {
    const r = await this.send('Page.captureScreenshot', { format: 'png' });
    const file = path.join(OUT, name + '.png');
    fs.writeFileSync(file, Buffer.from(r.data, 'base64'));
    fs.writeFileSync(path.join(OUT, name + '.txt'), caption);
    console.log('    shot ' + name.padEnd(26) + file);
    return file;
  }

  /** A genuine browser key event: this is the signal Unity's Input System reads. */
  async key(type, code, location) {
    await this.send('Input.dispatchKeyEvent', {
      type,
      key: 'Shift',
      code,
      location,
      windowsVirtualKeyCode: 16,
      nativeVirtualKeyCode: 16,
      modifiers: type === 'keyDown' ? 8 : 0,
    });
  }

  async tapShift(code, location, holdMs = 90) {
    await this.key('keyDown', code, location);
    await sleep(holdMs);
    await this.key('keyUp', code, location);
  }
}

// ---------------------------------------------------------------- main
(async () => {
  const exe = BROWSERS[WHICH];
  if (!fs.existsSync(exe)) {
    console.log('BROWSER NOT INSTALLED: ' + exe);
    console.log('  NOT a pass and NOT a failure - the check did not run.');
    process.exit(4);
  }
  if (!fs.existsSync(path.join(BUILD, 'index.html'))) {
    console.log('NO WEBGL BUILD at ' + BUILD);
    process.exit(2);
  }
  fs.mkdirSync(OUT, { recursive: true });

  const { server, port } = await serve(BUILD);
  const url = `http://127.0.0.1:${port}/index.html`;
  console.log('serving ' + BUILD + ' at ' + url);

  const profile = path.join(OUT, '_profile_' + WHICH);
  const browser = spawn(exe, [
    '--headless=new',
    '--remote-debugging-port=' + PORT,
    '--user-data-dir=' + profile,
    '--no-first-run', '--no-default-browser-check', '--disable-sync',
    '--disable-extensions', '--disable-background-networking',
    '--window-size=1280,720',
    // WebGL in headless needs a software rasteriser.
    '--use-gl=angle', '--use-angle=swiftshader', '--enable-unsafe-swiftshader',
    'about:blank',
  ], { stdio: 'ignore' });

  let cdp = null;
  let code = 1;
  try {
    // Wait for the debugging endpoint.
    let targets = null;
    for (let i = 0; i < 60; i++) {
      await sleep(500);
      try { targets = await getJson(`http://127.0.0.1:${PORT}/json/list`); break; } catch (e) { /* not up yet */ }
    }
    if (!targets) throw new Error('the browser never opened its debugging port');

    const page = targets.find((t) => t.type === 'page');
    if (!page) throw new Error('no page target');
    cdp = await Cdp.connect(page.webSocketDebuggerUrl);
    await cdp.send('Page.enable');
    await cdp.send('Runtime.enable');
    console.log('  connected to ' + WHICH + ' over CDP');

    await cdp.send('Page.navigate', { url });
    console.log('  loading the Unity player...');

    // Wait for Unity to actually be running, not merely for the document to load.
    let ready = false;
    for (let i = 0; i < 90; i++) {
      await sleep(1000);
      const state = await cdp.evaluate(
        "(() => { const c = document.querySelector('#unity-canvas') || document.querySelector('canvas');" +
        " return JSON.stringify({canvas: !!c, w: c ? c.width : 0, h: c ? c.height : 0," +
        " inst: typeof unityInstance !== 'undefined' && !!unityInstance}); })()");
      if (state) {
        const s = JSON.parse(state);
        if (s.canvas && s.w > 0) { ready = true; console.log('  canvas ' + s.w + 'x' + s.h + ', unityInstance=' + s.inst); break; }
      }
    }
    if (!ready) throw new Error('the Unity canvas never appeared');

    await sleep(6000);                       // let the title screen settle
    await cdp.shot('01_title',
      'TITLE in headless ' + WHICH + '. If the logo, tagline and PRESS SHIFT TO START are readable, ' +
      'the built-in font survived the WebGL build - which no editor test can prove.');

    // Give the canvas keyboard focus, as a player's click would.
    await cdp.send('Input.dispatchMouseEvent', { type: 'mousePressed', x: 640, y: 400, button: 'left', clickCount: 1 });
    await cdp.send('Input.dispatchMouseEvent', { type: 'mouseReleased', x: 640, y: 400, button: 'left', clickCount: 1 });
    await cdp.evaluate("(document.querySelector('#unity-canvas')||document.querySelector('canvas')).focus()");
    await sleep(400);

    await cdp.tapShift('ShiftLeft', 1);
    await sleep(900);
    await cdp.shot('02_after_left_shift_start',
      'After one LEFT SHIFT: the title should be gone and a bomb should be in the hands.');

    await sleep(900);
    await cdp.tapShift('ShiftLeft', 1);
    await sleep(350);
    await cdp.shot('03_threw_LEFT', 'LEFT SHIFT thrown: the bomb should be heading LEFT.');

    await sleep(1700);
    await cdp.tapShift('ShiftRight', 2);
    await sleep(350);
    await cdp.shot('04_threw_RIGHT',
      'RIGHT SHIFT thrown: the bomb should be heading RIGHT. If 03 and 04 differ, the two Shift ' +
      'keys are being told apart in the browser - which is the whole question.');

    // A held key must be ONE throw, not a stream (the Sticky Keys safety, in a browser).
    await sleep(1700);
    await cdp.key('keyDown', 'ShiftLeft', 1);
    await sleep(1600);
    await cdp.key('keyUp', 'ShiftLeft', 1);
    await sleep(400);
    await cdp.shot('05_held_left_shift',
      'LEFT SHIFT held for 1.6 s: the score should have moved by ONE throw, not a stream.');

    await sleep(2500);
    await cdp.shot('06_later', 'A few seconds on: still playing, or showing the BOOM! card.');

    console.log('\nSCREENSHOTS: ' + OUT);
    console.log('03 vs 04 is the left/right Shift evidence. 01 is the font evidence.');
    code = 0;
  } catch (e) {
    console.log('FAILED: ' + e.message);
    console.log('  NOT a pass and NOT a failure if the browser never rendered - headless WebGL');
    console.log('  depends on a software rasteriser and can simply refuse.');
    code = 3;
  } finally {
    try { if (cdp) cdp.ws.close(); } catch (e) { /* ignore */ }
    try { browser.kill(); } catch (e) { /* ignore */ }
    server.close();
    setTimeout(() => process.exit(code), 500);
  }
})();
