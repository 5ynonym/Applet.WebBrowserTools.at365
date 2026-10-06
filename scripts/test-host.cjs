const fs = require('node:fs');
const path = require('node:path');
const assert = require('node:assert/strict');
const root = path.resolve(__dirname, '..');
const host = path.resolve(process.argv[2] || path.join(root, '../AppDock.at365'));
const packaged = process.argv[3] ? path.resolve(process.argv[3]) : null;
const { _electron: electron } = require(path.join(host, 'node_modules/playwright'));
const { createDefaultSettings } = require(path.join(host, 'out/main/shared/settings-schema.js'));
const profile = path.join(root, 'artifacts', `host-${Date.now()}`);
const folder = path.join(profile, 'extensions/Applet.WebBrowserTools.at365');
fs.mkdirSync(folder, { recursive: true });
for (const name of ['extension.json', 'Applet.WebBrowserTools.at365.dll', 'Applet.WebBrowserTools.at365.deps.json'])
  fs.copyFileSync(path.join(root, 'publish/Applet.WebBrowserTools.at365', name), path.join(folder, name));
const id = 'at365.web-browser-tools';
const settings = createDefaultSettings();
settings.host.notifications = false;
settings.extensions[id] = { enabled: true, settings: {} };
fs.writeFileSync(path.join(profile, 'settings.json'), JSON.stringify(settings));
let application;
async function snapshot(page) { return page.evaluate(() => window.dock.snapshot()); }
async function until(check, message) {
  const end = Date.now() + 15000;
  while (Date.now() < end) { if (await check()) return; await new Promise(r => setTimeout(r, 80)); }
  throw new Error(message);
}
async function state(page) { return (await snapshot(page)).extensions.find(e => e.id === id); }
(async () => {
  try {
    application = await electron.launch({
      executablePath: packaged || require(path.join(host, 'node_modules/electron')),
      env: Object.fromEntries(Object.entries(process.env).filter(([key]) => key !== 'ELECTRON_RUN_AS_NODE')),
      args: packaged ? [`--test-profile=${profile}`] : [host, `--test-profile=${profile}`], timeout: 30000,
    });
    const page = await application.firstWindow();
    await page.getByRole('heading', { name: 'ホーム', exact: true }).waitFor();
    await until(async () => (await state(page))?.state === 'running' && (await state(page))?.commands.length === 11, 'Initial 11 commands missing');
    assert.equal((await state(page)).state, 'running');
    await page.keyboard.press('Control+,');
    await page.locator('.settings-applet-list').getByRole('button', { name: 'Applet.WebBrowserTools.at365', exact: true }).click();
    const input = page.getByLabel('タブを閉じる：送信キー', { exact: true });
    await input.fill('Ctrl+W');
    await page.getByRole('button', { name: '変更をすべて保存', exact: true }).click();
    await until(async () => (await state(page)).panel?.facts.some(f => f.label === 'タブを閉じる' && f.value === 'Ctrl+W'), 'Live edit not applied');
    await page.screenshot({ path: path.join(profile, 'key-settings.png'), fullPage: true });
    await page.evaluate(extension => window.dock.restartExtension(extension), id);
    await until(async () => (await state(page)).state === 'running' && (await state(page)).panel?.facts.some(f => f.label === 'タブを閉じる' && f.value === 'Ctrl+W'), 'Restart lost key setting');
    const saved = JSON.parse(fs.readFileSync(path.join(profile, 'settings.json')));
    assert.equal(saved.extensions[id].settings['keys.close-tab'], 'Ctrl+W');
    await page.evaluate(extension => window.dock.toggleExtension(extension, false), id);
    await until(async () => (await state(page)).state === 'stopped', 'Deactivate failed');
    const errors = (await snapshot(page)).logs.filter(entry => entry.level === 'error');
    assert.equal(errors.length, 0, JSON.stringify(errors));
    console.log('PASS 11 commands, live key setting, persisted setting after restart, deactivate, no host errors');
    console.log(profile);
  } finally {
    if (application) { try { fs.writeFileSync(path.join(profile, 'snapshot.json'), JSON.stringify(await snapshot(await application.firstWindow()), null, 2)); } catch {} await application.close(); }
  }
})().catch(error => { console.error(error); process.exitCode = 1; });
