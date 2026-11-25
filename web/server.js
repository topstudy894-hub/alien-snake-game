// Hungry Alien .io - authoritative multiplayer server (Node.js + ws)
// The server owns ALL game state. Clients only send {angle, boost}.
const http = require('http'), fs = require('fs'), path = require('path');
const { WebSocketServer } = require('ws');

const PORT = process.env.PORT || 3000;
const WS = 3000, SP = 8, MAXP = 12, FOODN = 350, TICK = 33; // ~30 ticks/s, snapshots at ~15/s
const AVS = ['👑', '👽', '😎', '🤖', '👾', '🐸', '🦄', '🐙', '🔥'];
const THEMES = 6; // map looks: the client has 6 themes (space, jungle, lava, ice, neon, desert)
const rnd = (a, b) => a + Math.random() * (b - a);
const R = p => 8 + Math.min(10, p.seg.length / 40);

const PUB = path.join(__dirname, 'public');
const MIME = { '.html': 'text/html; charset=utf-8', '.js': 'text/javascript; charset=utf-8', '.json': 'application/json', '.webmanifest': 'application/manifest+json', '.png': 'image/png', '.svg': 'image/svg+xml', '.ico': 'image/x-icon', '.txt': 'text/plain; charset=utf-8' };
const MAX_PER_IP = +process.env.MAX_PER_IP || 8, MAX_ROOMS = +process.env.MAX_ROOMS || 300;
const ORIGINS = (process.env.ALLOWED_ORIGINS || '').split(',').map(x => x.trim()).filter(Boolean); // e.g. https://your-game.onrender.com
const ipCount = new Map();
const ipOf = req => String(req.headers['x-forwarded-for'] || req.socket.remoteAddress || '').split(',')[0].trim();

const srv = http.createServer((req, res) => {
  const hdr = { 'X-Content-Type-Options': 'nosniff', 'Referrer-Policy': 'no-referrer' };
  let u; try { u = decodeURIComponent((req.url || '/').split('?')[0]); } catch (e) { res.writeHead(400, hdr); return res.end('bad request'); }
  if (u === '/health') { res.writeHead(200, hdr); return res.end('ok'); }
  if (u === '/') u = '/index.html';
  const f = path.normalize(path.join(PUB, u));
  if (f !== PUB && !f.startsWith(PUB + path.sep)) { res.writeHead(403, hdr); return res.end('forbidden'); }   // no path traversal
  fs.readFile(f, (err, data) => {
    if (err) { res.writeHead(404, hdr); return res.end('not found'); }
    const ext = path.extname(f).toLowerCase();
    const fresh = ext === '.html' || ext === '.json' || ext === '.webmanifest' || f.endsWith('sw.js');
    res.writeHead(200, Object.assign({ 'Content-Type': MIME[ext] || 'application/octet-stream', 'Cache-Control': fresh ? 'no-cache' : 'public, max-age=86400' }, hdr));
    res.end(data);
  });
});
// Native clients (Unity) send no Origin header and are always allowed; browsers must match ALLOWED_ORIGINS if you set it.
const wss = new WebSocketServer({ server: srv, maxPayload: 2048, verifyClient: info => !ORIGINS.length || !info.origin || ORIGINS.includes(info.origin) });
const rooms = new Map();
let nextId = 1;

function newRoom(priv) {
  if (rooms.size >= MAX_ROOMS) return null;
  let c;
  do { c = Array.from({ length: 5 }, () => 'ABCDEFGHJKLMNPQRSTUVWXYZ23456789'[Math.random() * 32 | 0]).join(''); } while (rooms.has(c));
  const r = { code: c, priv, ps: new Map(), foods: [], pu: [], tk: 0, theme: Math.random() * THEMES | 0, seed: Math.random() * 1e9 | 0 };
  for (let i = 0; i < FOODN; i++) food(r);
  for (let i = 0; i < 6; i++) mkpu(r);
  rooms.set(c, r);
  return r;
}
function food(r, x, y, v) {
  v = v || (Math.random() < .15 ? 3 : 1);
  r.foods.push({ x: x === undefined ? rnd(20, WS - 20) : x, y: y === undefined ? rnd(20, WS - 20) : y, v, e: Math.random() * 40 | 0 });
}
// power-ups: 0 = magnet, 1 = shield, 2 = double score (10 s each)
function mkpu(r) { r.pu.push({ x: rnd(100, WS - 100), y: rnd(100, WS - 100), t: Math.random() * 3 | 0 }); }
// Fresh arena: new food, new power-ups, new map theme (used when a player starts over and nobody else is alive in the room)
function resetRoom(r) {
  r.foods = []; for (let i = 0; i < FOODN; i++) food(r);
  r.pu = []; for (let i = 0; i < 6; i++) mkpu(r);
  let t; do { t = Math.random() * THEMES | 0; } while (t === r.theme);
  r.theme = t; r.seed = Math.random() * 1e9 | 0;
}
function respawn(p) {
  const r = p.room; if (!r || p.alive) return;
  if ([...r.ps.values()].every(o => o === p || !o.alive)) resetRoom(r);   // alone -> brand-new map, no leftover food
  spawn(p);                                                               // others still playing -> same arena (their food stays for everyone)
}
function spawn(p) {
  const a = rnd(0, 6.283), x = rnd(300, WS - 300), y = rnd(300, WS - 300);
  Object.assign(p, { x, y, a, ta: a, score: 0, alive: true, boost: false, eat: 0, drain: 0, mg: 0, sh: 90, dx: 0, // sh: 3 s spawn protection
   
    seg: Array.from({ length: 10 }, (_, i) => ({ x: x - Math.cos(a) * i * SP, y: y - Math.sin(a) * i * SP })) });
}
const send = (p, o) => { if (p.ws.readyState === 1 && p.ws.bufferedAmount < 2e5) p.ws.send(JSON.stringify(o)); };
const bc = (r, o) => { const m = JSON.stringify(o); for (const p of r.ps.values()) if (p.ws.readyState === 1) p.ws.send(m); };

function die(r, p, k) {
  p.alive = false; p.boost = false;
  for (let i = 0; i < p.seg.length; i += 2) food(r, p.seg[i].x + rnd(-8, 8), p.seg[i].y + rnd(-8, 8), 2);
  send(p, { t: 'dead', sc: p.score, by: k && k.name });
  bc(r, { t: 'kill', v: p.name, k: k && k.name }); // every player in the room sees the death
}
function step(r, p) {
  if (!p.alive) return;
  if (p.mg > 0) p.mg--; if (p.sh > 0) p.sh--; if (p.dx > 0) p.dx--;
  const d = ((p.ta - p.a + Math.PI * 3) % (2 * Math.PI)) - Math.PI;
  p.a += Math.max(-.15, Math.min(.15, d));
  const bo = p.boost && p.seg.length > 12, sp = bo ? 9.2 : 5.4;
  p.x += Math.cos(p.a) * sp; p.y += Math.sin(p.a) * sp;
  if (bo && ++p.drain >= 4) { p.drain = 0; if (p.score > 0) p.score--; const t = p.seg[p.seg.length - 1]; food(r, t.x, t.y, 1); }
  const g = p.seg; g[0].x = p.x; g[0].y = p.y;
  const tl = 10 + Math.floor(p.score * .8);
  while (g.length < tl) g.push({ x: g[g.length - 1].x, y: g[g.length - 1].y });
  while (g.length > tl && g.length > 10) g.pop();
  for (let i = 1; i < g.length; i++) {
    const dx = g[i - 1].x - g[i].x, dy = g[i - 1].y - g[i].y, dd = Math.hypot(dx, dy);
    if (dd > SP) { const k = (dd - SP) / dd; g[i].x += dx * k; g[i].y += dy * k; }
  }
  const rd = R(p);
  for (let i = r.foods.length - 1; i >= 0; i--) {
    const f = r.foods[i], dx = f.x - p.x, dy = f.y - p.y, d2 = dx * dx + dy * dy, rr = rd + (f.v >= 3 ? 13 : 9) + 4;
    if (d2 < rr * rr) { p.score += f.v * (p.dx > 0 ? 2 : 1); p.eat = 6; r.foods.splice(i, 1); }
    else if (d2 < (rd + (p.mg > 0 ? 260 : 80)) ** 2) { const k = p.mg > 0 ? .3 : .2; f.x -= dx * k; f.y -= dy * k; }
  }
  for (let i = r.pu.length - 1; i >= 0; i--) { const q = r.pu[i]; if (Math.hypot(q.x - p.x, q.y - p.y) < rd + 18) { p[['mg', 'sh', 'dx'][q.t]] = 300; r.pu.splice(i, 1); } }
  if (p.eat > 0) p.eat--;
  if (p.x < 0 || p.y < 0 || p.x > WS || p.y > WS) return die(r, p, null);
  if (p.sh <= 0) for (const o of r.ps.values()) {
    if (o === p || !o.alive) continue;
    const lim = rd + R(o) * .8;
    for (let i = 1; i < o.seg.length; i++) {
      const dx = o.seg[i].x - p.x, dy = o.seg[i].y - p.y;
      if (dx * dx + dy * dy < lim * lim) return die(r, p, o);
    }
  }
}

setInterval(() => {
  for (const r of rooms.values()) {
    if (!r.ps.size) { rooms.delete(r.code); continue; }
    const L = [...r.ps.values()];
    for (const p of L) step(r, p);
    if (r.foods.length < FOODN) for (let i = 0; i < 4; i++) food(r);
    if (r.pu.length < 6 && Math.random() < .02) mkpu(r);
    if (++r.tk % 2) continue;
    const all = L.map(p => ({ i: p.id, n: p.name, v: p.av, k: p.sk, x: p.x | 0, y: p.y | 0, a: +p.a.toFixed(2), s: p.score,
      l: p.alive ? 1 : 0, b: p.boost && p.seg.length > 12 ? 1 : 0, u: (p.mg > 0 ? 1 : 0) | (p.sh > 0 ? 2 : 0) | (p.dx > 0 ? 4 : 0), e: p.eat > 0 ? 1 : 0, g: p.alive ? p.seg.flatMap(s => [s.x | 0, s.y | 0]) : [] }));
    for (const me of L) {
      const f = [];
      for (const o of r.foods) if (Math.abs(o.x - me.x) < 1050 && Math.abs(o.y - me.y) < 800) f.push(o.x | 0, o.y | 0, o.v, o.e);
      const ps = all.map(o => (Math.abs(o.x - me.x) < 1700 && Math.abs(o.y - me.y) < 1700) ? o : { ...o, g: [] });
      const pu = [];
      for (const q of r.pu) if (Math.abs(q.x - me.x) < 1050 && Math.abs(q.y - me.y) < 800) pu.push(q.x | 0, q.y | 0, q.t);
      send(me, { t: 's', p: ps, f, pu, m: [me.mg, me.sh, me.dx], th: r.theme, sd: r.seed, n: wss.clients.size });
    }
  }
}, TICK);
setInterval(() => { const m = JSON.stringify({ t: 'online', n: wss.clients.size }); for (const c of wss.clients) if (c.readyState === 1) c.send(m); }, 3000);

function leave(p) {
  if (!p.room) return;
  p.room.ps.delete(p.id); p.room = null; p.alive = false;
}
function join(p, r) {
  if (r.ps.size >= MAXP) return send(p, { t: 'err', m: 'Room full (12/12)' });
  leave(p); r.ps.set(p.id, p); p.room = r; spawn(p);
  send(p, { t: 'joined', id: p.id, code: r.code, priv: r.priv });
}

wss.on('connection', (ws, req) => {
  const ip = ipOf(req), n = (ipCount.get(ip) || 0) + 1;
  if (n > MAX_PER_IP) { ws.close(1008, 'too many connections'); return; }
  ipCount.set(ip, n);
  const p = { id: nextId++, ws, name: 'Player', av: AVS[0], sk: [130, 0], alive: false, seg: [], room: null, mc: 0, mt: 0 };
  send(p, { t: 'online', n: wss.clients.size });
  ws.on('message', raw => {
    const now = Date.now();
    if (now - p.mt > 1000) { p.mt = now; p.mc = 0; }
    if (++p.mc > 90) return; // flood protection
    let d; try { d = JSON.parse(raw); } catch (e) { return; }
    if (!d || typeof d !== 'object') return;
    switch (d.t) {
      case 'hello':
        p.name = String(d.name || 'Player').replace(/[<>&"'`]/g, '').trim().slice(0, 10) || 'Player';
        p.av = AVS.includes(d.av) ? d.av : AVS[0];
        p.sk = [Math.max(0, Math.min(359, +d.h || 0)) | 0, d.rb ? 1 : 0, Math.max(0, Math.min(15, +d.sp || 0)) | 0];
        break;
      case 'quick': {
        let best = null;
        for (const r of rooms.values()) if (!r.priv && r.ps.size < MAXP && (!best || r.ps.size > best.ps.size)) best = r;
        const r = best || newRoom(false); if (!r) send(p, { t: 'err', m: 'Server busy, try again soon' }); else join(p, r); break;
      }
      case 'create': { const r = newRoom(true); if (!r) send(p, { t: 'err', m: 'Server busy, try again soon' }); else join(p, r); break; }
      case 'join': {
        const r = rooms.get(String(d.code || '').toUpperCase().slice(0, 5));
        if (!r) send(p, { t: 'err', m: 'Room not found' }); else join(p, r); break;
      }
      case 'in':
        if (p.room && p.alive && isFinite(+d.a)) { p.ta = +d.a; p.boost = !!d.b; } break;
      case 'ping': send(p, { t: 'pong', ts: +d.ts || 0 }); break;
      case 'respawn': respawn(p); break;
      case 'leave': leave(p); break;
    }
  });
  ws.on('close', () => { leave(p); const c = (ipCount.get(ip) || 1) - 1; if (c <= 0) ipCount.delete(ip); else ipCount.set(ip, c); });
  ws.on('error', () => leave(p));
});

srv.listen(PORT, () => console.log('Hungry Alien server on http://localhost:' + PORT));
process.on('uncaughtException', e => console.error('uncaught', e));
