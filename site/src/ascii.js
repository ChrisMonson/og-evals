// The ASCII picture above the scenario tabs: each scenario drawn on one
// character grid, sweeping from one scene to the next when the tab changes.
(() => {
// The grid is 96 columns wide, or 64 on narrow screens so the characters stay readable.
// Shapes are drawn in a 50-wide space (u) scaled to the grid; objects keep their size
// and take their place from LAYOUT. K = cell width / cell height, so X = x*K keeps shapes round.
const R = 32, K = 0.52;
let C = 96, f = 1, L = null;
const LAYOUT = {
  96: { torch: 26, eyes: 45, merchant: 69, cart: 74, beam: 4, stone: 70, ledger: 36, examiner: 60, crater: 24, boat: 58 },
  64: { torch: 17, eyes: 30, merchant: 44, cart: 49, beam: 2, stone: 48, ledger: 18, examiner: 42, crater: 16, boat: 41 },
};
const rnd = (x, y, s = 0) => { const n = Math.sin(x * 127.1 + y * 311.7 + s * 74.7) * 43758.5453; return n - Math.floor(n); };

// A grid cell is [char, tone]: tone "" = muted, "f" = full ink, "a" = accent.
const blank = () => Array.from({ length: R }, () => Array.from({ length: C }, () => [" ", ""]));
function stamp(g, x0, y0, lines, tones = {}) {
  lines.forEach((line, dy) => [...line].forEach((ch, dx) => {
    const x = x0 + dx, y = y0 + dy;
    if (ch === "`" || x < 0 || y < 0 || x >= C || y >= R) return;   // ` = transparent
    g[y][x] = [ch, tones[ch] || "f"];
  }));
}

// An arch standing on the ground at row `base`: inside test, and the outline character for a cell.
const inArch = (x, y, cx, base, rx, ry) => y < base && ((x * K - cx) / rx) ** 2 + ((y - base) / ry) ** 2 < 1;
function archEdge(x, y, cx, base, rx, ry) {
  if (!inArch(x, y, cx, base, rx, ry)) return null;
  const out = [[1, 0], [-1, 0], [0, -1]].some(([dx, dy]) => !inArch(x + dx, y + dy, cx, base, rx, ry));
  if (!out) return "";
  const a = Math.atan2(base - y, x * K - cx) * 180 / Math.PI;      // 0 = right foot, 90 = top, 180 = left foot
  return a < 28 ? "|" : a < 72 ? "\\" : a < 108 ? "_" : a < 152 ? "/" : "|";
}

function cave() {
  const g = blank();
  const hill = X => 11.5 + 2.4 * Math.sin(X * 0.11 + 0.6) + 0.7 * Math.sin(X * 0.37);
  for (let y = 0; y < R; y++) for (let x = 0; x < C; x++) {
    const X = x * K / f, Y = y, top = hill(X), p = rnd(x, y, 1);
    if (Y < top - 0.5) { if (p < 0.022) g[y][x] = [p < 0.006 ? "*" : ".", p < 0.006 ? "f" : ""]; continue; }
    if (Y >= 27) { g[y][x] = [Y === 27 ? "=" : p < 0.25 ? "." : p < 0.32 ? "," : " ", Y === 27 ? "f" : ""]; continue; }
    const e = archEdge(x, y, 25 * f, 27, 7.5 * f, 12);
    if (e !== null) { g[y][x] = e ? [e, "f"] : [" ", ""]; continue; }  // the cave mouth
    if (Y < top + 0.5) { const s = hill(X + K / f) - hill(X); g[y][x] = [s < -0.12 ? "/" : s > 0.12 ? "\\" : "_", "f"]; continue; }
    const depth = Y - top;
    g[y][x] = p < 0.16 + Math.min(depth, 12) * 0.018 ? [".:'.,"[Math.floor(rnd(x, y, 2) * 5)], ""] : [" ", ""];
  }
  // the beast's eyes in the dark, the torch, the merchant's cart
  stamp(g, L.eyes, 21, ["o``o"], { o: "a" });
  stamp(g, L.torch, 14, ["`(`", "(*)", "\\^/", "`|`", "`|`", "`|`", "`|`", "`|`", "`|`", "`|`", "`|`", "`|`", "`|`"], { "(": "a", ")": "a", "*": "a", "^": "a", "\\": "a", "/": "a" });
  for (let y = 12; y < 20; y++) for (let x = L.torch - 4; x < L.torch + 7; x++) {   // torchlight on the hillside
    if ((x - L.torch - 1.5) ** 2 * 0.3 + (y - 15) ** 2 < 14 && g[y][x][1] === "" && g[y][x][0] !== " ") g[y][x][1] = "a";
  }
  stamp(g, L.cart, 20, [
    "`/`````i```",
    "/______|__",
    "|_|____|_|",
    "|________|",
    "`(o)```(o)",
  ], { i: "a" });
  stamp(g, L.merchant, 22, ["`o`", "/|\\", "/`\\"]);
  return g;
}

function trials() {
  const g = blank();
  for (let y = 0; y < R; y++) for (let x = 0; x < C; x++) {
    const X = x * K, Y = y, p = rnd(x, y, 3);
    if (Y >= 27) { g[y][x] = [Y === 27 ? "=" : (x + y * 2) % 9 === 0 ? "/" : p < 0.08 ? "." : " ", Y === 27 ? "f" : ""]; continue; }
    const e = archEdge(x, y, 25 * f, 27, 8.5 * f, 15.5);
    if (e !== null) {                                              // the gate: an arch of bars
      g[y][x] = e ? [e, "f"] : x % 4 === 0 ? ["|", "f"] : Y === 18 ? ["-", ""] : [" ", ""];
      continue;
    }
    const row = Math.floor(Y / 4), off = row % 2 ? 6 : 0;             // the hall's stone wall
    g[y][x] = Y % 4 === 3 ? [p < 0.85 ? "_" : " ", ""] : (x + off) % 12 === 0 ? ["|", ""] : p < 0.04 ? [".", ""] : [" ", ""];
  }
  stamp(g, Math.round(25 * f / K), 11, ["@"], { "@": "a" });     // keystone
  stamp(g, L.beam, 19, [
    "_______________",
    "===============",
    "`||`````````||`",
    "`||`````````||`",
    "`||`````````||`",
    "/__\\```````/__\\",
  ]);
  stamp(g, L.stone, 17, [
    "`__________",
    "/:::::::::/|",
    "/_________/:|",
    "|`````````|:|",
    "|`.```:```|:|",
    "|`````````|:/",
    "|_________|/",
  ]);
  stamp(g, L.ledger, 25, [
    "```_________`_________",
    "``/ ~~~~ [x]| ~~~~ [x]\\",
    "`/  ~~~~~[x]| ~~~~    \\",
    "/___________|__________\\",
  ], { "[": "a", "]": "a", x: "a" });
  stamp(g, L.examiner, 22, ["``z", "`z", "(-.-)", "/|_|\\"], { z: "" });
  return g;
}

function ferry() {
  const g = blank();
  const cone = Y => (Y - 6) * 0.85;                                  // half-width of the volcano at height Y
  for (let y = 0; y < R; y++) for (let x = 0; x < C; x++) {
    const X = x * K / f, Y = y, p = rnd(x, y, 5);
    if (Y >= 20) {                                                 // the sea
      const w = (x + Math.floor(Y * 2.7) + Math.floor(rnd(0, y, 6) * 9)) % 11;
      g[y][x] = Y === 20 ? ["_", ""] : w < 2 ? ["~", Y > 26 ? "f" : ""] : w === 5 && p < 0.4 ? ["-", ""] : [" ", ""];
      continue;
    }
    if (Y >= 6) {
      const half = cone(Y), dx = X - 14;
      if (Math.abs(dx) < half + 2 && Y > 6) {
        if (Math.abs(Math.abs(dx) - (half + 2)) < 0.6) { g[y][x] = [dx < 0 ? "/" : "\\", "f"]; continue; }
        const lava = Math.abs(dx - Math.sin(Y * 0.7) * 1.6) < 0.45 || Math.abs(dx + 5 - Math.sin(Y * 0.5 + 2) * 1.2) < 0.4 && Y > 11;
        g[y][x] = lava ? ["|", "a"] : p < 0.5 ? [":.'"[Math.floor(rnd(x, y, 7) * 3)], ""] : [" ", ""];
        continue;
      }
    }
    if (Y < 6.5) {                                                 // the plume
      const spread = 3 + (6 - Y) * 1.9, dx = X - 14 - (6 - Y) * 0.8;
      if (Math.abs(dx) < spread) {
        const q = rnd(x, y, 8);
        if (q < 0.12) { g[y][x] = ["*", "a"]; continue; }
        if (q < 0.55) { g[y][x] = ["()o.@"[Math.floor(rnd(x, y, 9) * 5)], q < 0.3 ? "f" : ""]; continue; }
      }
    }
    if (p < 0.01) g[y][x] = [".", ""];
  }
  stamp(g, L.crater, 5, ["\\_/"], { "\\": "a", _: "a", "/": "a" });
  stamp(g, 0, 20, [
    "__________",
    "          |__",
    "    o        |__",
    "   /|\\         |__",
    "   / \\            |__",
    "                     |",
  ]);
  stamp(g, L.boat, 13, [
    "`````````````|\\",
    "`````````````|`\\",
    "`````````````|``\\",
    "`````````````|```\\",
    "`````````````|____\\",
    "`````````````|",
    "`````_____o__|______",
    "`````\\```````````(_)/",
    "``````\\____________/",
  ]);
  return g;
}


// Shift between the three scenes whenever the scenario tab changes.
const art = document.getElementById("ascii");
let SCENES = {};
function build() {                   // pick the grid for the column's width and draw the three scenes on it
  C = art.clientWidth < 560 ? 64 : 96; f = C / 96; L = LAYOUT[C];
  SCENES = { "tab-cave": cave(), "tab-trials": trials(), "tab-ferry": ferry() };
}
const escH = c => c === "<" ? "&lt;" : c === ">" ? "&gt;" : c === "&" ? "&amp;" : c;
function render(g) {
  art.innerHTML = g.map(row => {
    let out = "", tone = null;
    for (const [c, t] of row) {
      if (t !== tone) { if (tone) out += "</span>"; if (t) out += `<span class="${t}">`; tone = t; }
      out += escH(c);
    }
    return out + (tone ? "</span>" : "");
  }).join("\n");
}
function fit() {                     // size the font so the 96 columns fill the column exactly
  const probe = document.createElement("span"); probe.textContent = "M".repeat(50);
  probe.style.cssText = "font-size:100px;visibility:hidden;position:absolute";
  art.appendChild(probe); const per = probe.getBoundingClientRect().width / 50 / 100; probe.remove();
  art.style.fontSize = (art.clientWidth / (C * per) - 0.01) + "px";
}
const GLYPHS = "!<>-_\\/[]{}=+*^?#%~:;|";
const still = matchMedia("(prefers-reduced-motion: reduce)").matches;
build();
let current = SCENES["tab-cave"], frame = null;
function shift(target) {
  cancelAnimationFrame(frame);
  const from = current; current = target;
  if (still) { render(target); return; }
  const delay = [], start = performance.now(), SWEEP = 900, SPIN = 300, seed = Math.random() * 100;
  for (let y = 0; y < R; y++) { delay[y] = []; for (let x = 0; x < C; x++) delay[y][x] = (x / C) * SWEEP + rnd(x, y, seed) * 250; }
  const step = now => {
    const t = now - start; let busy = false;
    render(target.map((row, y) => row.map((cell, x) => {
      const d = delay[y][x], a = from[y][x];
      if (t < d) { busy = true; return a; }
      if (t < d + SPIN && (a[0] !== " " || cell[0] !== " ")) {
        busy = true;
        return [GLYPHS[Math.floor(Math.random() * GLYPHS.length)], cell[1] === "a" || a[1] === "a" ? "a" : ""];
      }
      return cell;
    })));
    if (busy) frame = requestAnimationFrame(step);
  };
  frame = requestAnimationFrame(step);
}
const selected = () => document.querySelector('.tablist [role="tab"][aria-selected="true"]');
new MutationObserver(() => { const s = SCENES[selected()?.id]; if (s && s !== current) shift(s); })
  .observe(document.querySelector(".tablist"), { subtree: true, attributes: true, attributeFilter: ["aria-selected"] });
current = SCENES[selected()?.id] || current;
fit(); render(current);
addEventListener("resize", () => {
  const was = C; build();
  if (C !== was) { cancelAnimationFrame(frame); current = SCENES[selected()?.id] || SCENES["tab-cave"]; render(current); }
  fit();
});
document.fonts.ready.then(() => { fit(); });
})();
