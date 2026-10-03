// Writes test-results/icon-variants.html: every app-icon variant from Assets/Art/Icon/<variant>/
// as inline SVG — 256 px, 16/24/32/48 px (icon-<size>.svg when present), light and dark
// backgrounds, a 40 px taskbar mock-up — next to the shipped Assets/Resources/app_icon.png.
// Signature of a variant = <title> of its icon.svg. No external dependencies in the page.
// Usage: node tools/icon-gallery.mjs
import fs from 'node:fs';
import path from 'node:path';

const repo = path.resolve(import.meta.dirname, '..');
const iconRoot = path.join(repo, 'Assets', 'Art', 'Icon');
const outPath = path.join(repo, 'test-results', 'icon-variants.html');
const currentPng = fs.readFileSync(path.join(repo, 'Assets', 'Resources', 'app_icon.png')).toString('base64');
const preferredOrder = ['monogram', 'monogram-perspective', 'cabinet-ruler', 'iso-corner', 'iso-cabinet', 'plan'];
const rowSizes = [16, 24, 32, 48];

let instance = 0;
function inline(file, size) {
  const n = ++instance;
  return fs.readFileSync(file, 'utf8')
    .replace(/<title>[\s\S]*?<\/title>\s*/, '')
    .replace(/id="([^"]+)"/g, `id="$1-${n}"`)
    .replace(/url\(#([^)]+)\)/g, `url(#$1-${n})`)
    .replace('<svg ', `<svg width="${size}" height="${size}" `);
}
const sourceFor = (dir, size) => {
  const specific = path.join(dir, `icon-${size}.svg`);
  return fs.existsSync(specific) ? specific : path.join(dir, 'icon.svg');
};

const names = fs.readdirSync(iconRoot, { withFileTypes: true })
  .filter(d => d.isDirectory() && fs.existsSync(path.join(iconRoot, d.name, 'icon.svg')))
  .map(d => d.name)
  .sort((a, b) => ((preferredOrder.indexOf(a) + 1) || 99) - ((preferredOrder.indexOf(b) + 1) || 99) || a.localeCompare(b));

const neighbours = [
  '<svg width="24" height="24" viewBox="0 0 24 24"><rect x="3" y="3" width="8" height="8" fill="#3A8DDE"/><rect x="13" y="3" width="8" height="8" fill="#3A8DDE"/><rect x="3" y="13" width="8" height="8" fill="#3A8DDE"/><rect x="13" y="13" width="8" height="8" fill="#3A8DDE"/></svg>',
  '<svg width="24" height="24" viewBox="0 0 24 24"><path d="M2 6 H9 L11 8 H22 V20 H2 Z" fill="#E8B03A"/><rect x="2" y="9" width="20" height="11" fill="#F5C84C"/></svg>',
  '<svg width="24" height="24" viewBox="0 0 24 24"><circle cx="12" cy="12" r="10" fill="#2F9E6E"/><circle cx="12" cy="12" r="4" fill="#E9F5EF"/></svg>',
  null,
  '<svg width="24" height="24" viewBox="0 0 24 24"><rect x="2" y="3" width="20" height="18" rx="3" fill="#2B2B2B" stroke="#8A8A8A"/><path d="M6 9 L9 12 L6 15" stroke="#EBEBEB" stroke-width="2" fill="none"/><rect x="11" y="14" width="6" height="2" fill="#EBEBEB"/></svg>',
  '<svg width="24" height="24" viewBox="0 0 24 24"><rect x="3" y="2" width="18" height="20" rx="2" fill="#5B6CC7"/><rect x="7" y="7" width="10" height="2" fill="#fff"/><rect x="7" y="11" width="10" height="2" fill="#fff"/></svg>',
];

function card(title, big, small, task) {
  const theme = mode => `
      <div class="panel ${mode}">
        <div class="big">${big()}</div>
        <div class="row">${rowSizes.map(s => `<figure>${small(s)}<figcaption>${s}</figcaption></figure>`).join('')}</div>
        <div class="taskbar">${neighbours.map(n => n ? `<span class="slot">${n}</span>` : `<span class="slot active">${task()}</span>`).join('')}</div>
      </div>`;
  return `
    <section>
      <h2>${title}</h2>
      <div class="pair">${theme('light')}${theme('dark')}</div>
    </section>`;
}

const cards = names.map(name => {
  const dir = path.join(iconRoot, name);
  const title = fs.readFileSync(path.join(dir, 'icon.svg'), 'utf8').match(/<title>([^<]*)<\/title>/)?.[1] ?? name;
  return card(`${title} <small>${name}</small>`, () => inline(path.join(dir, 'icon.svg'), 256), s => inline(sourceFor(dir, s), s), () => inline(sourceFor(dir, 24), 24));
});
const img = s => `<img width="${s}" height="${s}" src="data:image/png;base64,${currentPng}" alt="">`;
cards.push(card('Current icon <small>Assets/Resources/app_icon.png</small>', () => img(256), img, () => img(24)));

const html = `<!doctype html>
<html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width, initial-scale=1">
<title>Icon variants</title>
<style>
  :root { --bg: #FAFAFA; --ink: #1F1F24; --muted: #6B7080; }
  body { margin: 0; padding: 24px 16px; background: var(--bg); color: var(--ink); font: 14px/1.4 "Segoe UI", system-ui, sans-serif; }
  h1 { font-size: 20px; margin: 0 0 4px; } p.note { color: var(--muted); margin: 0 0 24px; }
  section { margin-bottom: 32px; }
  h2 { font-size: 16px; margin: 0 0 8px; } h2 small { color: var(--muted); font-weight: normal; margin-left: 8px; }
  .pair { display: flex; flex-wrap: wrap; gap: 12px; }
  .panel { padding: 16px; border-radius: 8px; display: flex; flex-direction: column; gap: 16px; align-items: flex-start; }
  .panel.light { background: #F3F3F3; color: #202020; border: 1px solid #E0E0E0; }
  .panel.dark { background: #202020; color: #EBEBEB; border: 1px solid #202020; }
  .big svg, .big img, .row svg, .row img, .slot svg, .slot img { display: block; }
  .row { display: flex; align-items: flex-end; gap: 20px; }
  figure { margin: 0; display: flex; flex-direction: column; align-items: center; gap: 4px; }
  figcaption { font-size: 11px; opacity: .6; }
  .taskbar { height: 40px; display: flex; align-items: center; gap: 4px; padding: 0 8px; border-radius: 4px; }
  .light .taskbar { background: #EEEEEE; box-shadow: 0 -1px 0 #D6D6D6; }
  .dark .taskbar { background: #1C1C1C; box-shadow: 0 -1px 0 #2E2E2E; }
  .slot { width: 40px; height: 40px; display: flex; align-items: center; justify-content: center; border-radius: 4px; position: relative; }
  .slot.active { background: rgba(127,127,127,.18); }
  .slot.active::after { content: ""; position: absolute; bottom: 2px; width: 16px; height: 3px; border-radius: 2px; background: #4D80BF; }
</style></head>
<body>
  <h1>Kitchen Designer — app icon variants</h1>
  <p class="note">Inline SVG from Assets/Art/Icon/&lt;variant&gt;/. Sizes 16/24/32 use the pixel-hinted icon-&lt;size&gt;.svg; view at 100 % browser zoom for true pixels. Taskbar: 40 px bar, 24 px icons, ours is the highlighted one.</p>
${cards.join('\n')}
</body></html>
`;
fs.mkdirSync(path.dirname(outPath), { recursive: true });
fs.writeFileSync(outPath, html);
console.log(`${names.length} variants -> ${outPath}`);
