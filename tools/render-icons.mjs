// Rasterises the app-icon variants in Assets/Art/Icon/<variant>/ with headless Chrome.
// For every size the most specific source wins: icon-<size>.svg, else icon.svg.
// Writes <variant>/icon-256.png and <variant>/icon.ico (all sizes, PNG entries),
// and the comparison sheet test-results/icon-variants.png (current icon included).
// Usage: node tools/render-icons.mjs
import fs from 'node:fs';
import path from 'node:path';
import { execFileSync } from 'node:child_process';

const repo = path.resolve(import.meta.dirname, '..');
const iconRoot = path.join(repo, 'Assets', 'Art', 'Icon');
const currentIcon = path.join(repo, 'Assets', 'Resources', 'app_icon.png');
const workDir = path.join(repo, 'test-results', 'icon-build');
const sheetPath = path.join(repo, 'test-results', 'icon-variants.png');
const sizes = [16, 24, 32, 48, 64, 128, 256];
const chromeCandidates = [
  'C:/Program Files/Google/Chrome/Application/chrome.exe',
  'C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe',
];

const dataUrl = (file, mime) => `data:${mime};base64,${fs.readFileSync(file).toString('base64')}`;

function sourceFor(dir, size) {
  const specific = path.join(dir, `icon-${size}.svg`);
  return fs.existsSync(specific) ? specific : path.join(dir, 'icon.svg');
}

const variants = fs.readdirSync(iconRoot, { withFileTypes: true })
  .filter(d => d.isDirectory() && fs.existsSync(path.join(iconRoot, d.name, 'icon.svg')))
  .map(d => ({
    name: d.name,
    dir: path.join(iconRoot, d.name),
    sources: Object.fromEntries(sizes.map(s => [s, dataUrl(sourceFor(path.join(iconRoot, d.name), s), 'image/svg+xml')])),
  }));

const page = `<!doctype html><meta charset="utf-8"><body><pre id="out"></pre><script>
const sizes = ${JSON.stringify(sizes)};
const variants = ${JSON.stringify(variants.map(v => ({ name: v.name, sources: v.sources })))};
const current = ${JSON.stringify(dataUrl(currentIcon, 'image/png'))};
const load = async src => { const i = new Image(); i.src = src; await i.decode(); return i; };
function raster(img, size) {
  const c = document.createElement('canvas'); c.width = c.height = size;
  const g = c.getContext('2d'); g.imageSmoothingQuality = 'high';
  g.drawImage(img, 0, 0, size, size); return c;
}
function sheet(rows) {
  const zoom = { 16: 8, 24: 6, 32: 4 };
  const pad = 16, gap = 12, label = 28;
  const nativeW = sizes.reduce((a, s) => a + s + gap, 0);
  const zoomW = Object.entries(zoom).reduce((a, [s, k]) => a + s * k + gap, 0);
  const panelW = pad + nativeW + 24 + zoomW + pad, rowH = label + 256 + pad;
  const c = document.createElement('canvas');
  c.width = panelW; c.height = rows.length * 2 * rowH;
  const g = c.getContext('2d');
  let y = 0;
  for (const row of rows) for (const bg of [{ fill: '#F3F3F3', ink: '#202020', name: 'light' }, { fill: '#1C1C1C', ink: '#EBEBEB', name: 'dark' }]) {
    g.fillStyle = bg.fill; g.fillRect(0, y, panelW, rowH);
    g.fillStyle = bg.ink; g.font = '16px Segoe UI, sans-serif';
    g.fillText(row.name + ' — ' + bg.name + '   (native 16/24/32/48/64/128/256 | zoom 16×8, 24×6, 32×4)', pad, y + 20);
    let x = pad; const base = y + label + 256;
    for (const s of sizes) { g.drawImage(row.rasters[s], x, base - s); x += s + gap; }
    x += 24; g.imageSmoothingEnabled = false;
    for (const [s, k] of Object.entries(zoom)) { g.drawImage(row.rasters[s], x, base - s * k, s * k, s * k); x += s * k + gap; }
    g.imageSmoothingEnabled = true; y += rowH;
  }
  return c;
}
(async () => { try {
  const out = { variants: {}, sheet: null };
  const rows = [];
  const cur = await load(current);
  rows.push({ name: 'current (Assets/Resources/app_icon.png)', rasters: Object.fromEntries(sizes.map(s => [s, raster(cur, s)])) });
  for (const v of variants) {
    const rasters = {};
    for (const s of sizes) rasters[s] = raster(await load(v.sources[s]), s);
    rows.push({ name: v.name, rasters });
    out.variants[v.name] = Object.fromEntries(sizes.map(s => [s, rasters[s].toDataURL('image/png')]));
  }
  out.sheet = sheet(rows).toDataURL('image/png');
  document.getElementById("out").textContent = JSON.stringify(out); } catch (e) { document.getElementById("out").textContent = "ERR " + e; }
})();
</script>`;

function packIco(pngs) {
  const header = Buffer.alloc(6 + 16 * pngs.length);
  header.writeUInt16LE(0, 0); header.writeUInt16LE(1, 2); header.writeUInt16LE(pngs.length, 4);
  let offset = header.length;
  pngs.forEach(({ size, data }, i) => {
    const e = 6 + 16 * i;
    header.writeUInt8(size >= 256 ? 0 : size, e); header.writeUInt8(size >= 256 ? 0 : size, e + 1);
    header.writeUInt8(0, e + 2); header.writeUInt8(0, e + 3);
    header.writeUInt16LE(1, e + 4); header.writeUInt16LE(32, e + 6);
    header.writeUInt32LE(data.length, e + 8); header.writeUInt32LE(offset, e + 12);
    offset += data.length;
  });
  return Buffer.concat([header, ...pngs.map(p => p.data)]);
}

fs.mkdirSync(workDir, { recursive: true });
const htmlPath = path.join(workDir, 'render.html');
fs.writeFileSync(htmlPath, page);
const chrome = chromeCandidates.find(fs.existsSync);
function renderOnce() {
  const dom = execFileSync(chrome, [
    '--headless=new', '--disable-gpu', '--dump-dom', '--virtual-time-budget=20000',
    '--user-data-dir=' + path.join(workDir, 'profile'), 'file:///' + htmlPath.replaceAll('\\', '/'),
  ], { maxBuffer: 256 * 1024 * 1024, encoding: 'utf8', stdio: ['ignore', 'pipe', 'ignore'] });
  const text = dom.match(/<pre id="out">([\s\S]*?)<\/pre>/)?.[1] ?? '';
  if (text.startsWith('ERR')) throw new Error(text);
  return text ? JSON.parse(text.replaceAll('&quot;', '"').replaceAll('&amp;', '&')) : null;
}
let result = null;
for (let attempt = 0; attempt < 3 && !result; attempt++) result = renderOnce();
if (!result) throw new Error('headless Chrome returned an empty page three times');
const decode = url => Buffer.from(url.split(',')[1], 'base64');

for (const v of variants) {
  const pngs = sizes.map(size => ({ size, data: decode(result.variants[v.name][size]) }));
  fs.writeFileSync(path.join(v.dir, 'icon-256.png'), pngs.at(-1).data);
  fs.writeFileSync(path.join(v.dir, 'icon.ico'), packIco(pngs));
}
fs.writeFileSync(sheetPath, decode(result.sheet));
fs.rmSync(workDir, { recursive: true, force: true });
console.log(`variants: ${variants.map(v => v.name).join(', ')}; sheet: ${sheetPath}`);
