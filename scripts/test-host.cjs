const fs = require("node:fs"),
  path = require("node:path"),
  assert = require("node:assert/strict");
const root = path.resolve(__dirname, ".."),
  host = path.resolve(process.argv[2] || path.join(root, "../AppDock.at365"));
const packaged = process.argv[3] ? path.resolve(process.argv[3]) : null;
const { _electron: electron } = require(
  path.join(host, "node_modules/playwright"),
);
const { createDefaultSettings } = require(
  path.join(host, "out/main/shared/settings-schema.js"),
);
const profile = path.join(root, "artifacts", `host-${Date.now()}`),
  folder = path.join(profile, "extensions/Applet.WebBrowserTools.at365");
fs.mkdirSync(folder, { recursive: true });
for (const name of ["extension.json", "Applet.WebBrowserTools.at365.exe"])
  fs.copyFileSync(
    path.join(root, "publish/Applet.WebBrowserTools.at365", name),
    path.join(folder, name),
  );
const id = "at365.web-browser-tools",
  settings = createDefaultSettings();
settings.host.notifications = false;
settings.host.hardwareAcceleration = false;
delete settings.gestures;
settings.extensions[id] = {
  enabled: true,
  settings: {
    "gestures.up": "new-tab",
    "gestures.right": "none",
    "gestures.wheel-delay-ms": 200,
    "gestures.indicator-position": "browser-center",
    "gestures.indicator-opacity": 0.3,
    "gestures.distance": 20,
    "keys.close-tab": "Ctrl+F4",
  },
};
fs.writeFileSync(path.join(profile, "settings.json"), JSON.stringify(settings));
let application;
const snapshot = (page) => page.evaluate(() => window.dock.snapshot());
const state = async (page) =>
  (await snapshot(page)).extensions.find((e) => e.id === id);
async function until(check, message) {
  const end = Date.now() + 15000;
  while (Date.now() < end) {
    if (await check()) return;
    await new Promise((r) => setTimeout(r, 80));
  }
  throw Error(message);
}
(async () => {
  try {
    application = await electron.launch({
      executablePath:
        packaged || require(path.join(host, "node_modules/electron")),
      env: Object.fromEntries(
        Object.entries(process.env).filter(
          ([key]) => key !== "ELECTRON_RUN_AS_NODE",
        ),
      ),
      args: packaged
        ? [`--test-profile=${profile}`]
        : [host, `--test-profile=${profile}`],
      timeout: 30000,
    });
    const page = await application.firstWindow();
    await until(
      async () =>
        (await snapshot(page)).startupReady &&
        (await state(page))?.commands.length === 11,
      "Initial 11 commands missing",
    );
    const migrated = (await snapshot(page)).settings.value.gestures;
    assert.equal(migrated.bindings.length, 7);
    assert.equal(
      migrated.bindings.find((r) => r.gesture === "move-up").command,
      id + ".new-tab",
    );
    assert.equal(migrated.distance, 20);
    assert.equal(migrated.wheelDelayMs, 200);
    assert.equal(migrated.indicatorPosition, "window-center");
    assert.equal(migrated.indicatorOpacity, 0.3);
    assert.match((await state(page)).panel.description, /AppDock本体/);
    await page.evaluate(() =>
      window.dock.executeCommand("appdock.settings.open"),
    );
    await page
      .locator(".settings-applet-list")
      .getByRole("button", { name: "WebBrowserTools.at365", exact: true })
      .click();
    assert.equal(
      await page.getByLabel("ジェスチャー：上", { exact: true }).count(),
      0,
    );
    await page
      .getByLabel("タブを閉じる：送信キー", { exact: true })
      .fill("Ctrl+W");
    await page
      .getByRole("button", { name: "変更をすべて保存", exact: true })
      .click();
    await until(
      async () =>
        (await state(page)).panel?.facts.some(
          (f) => f.label === "タブを閉じる" && f.value === "Ctrl+W",
        ),
      "Live key edit not applied",
    );
    await page
      .getByRole("button", { name: "マウスジェスチャー", exact: true })
      .click();
    await page
      .getByLabel("割り当て1のジェスチャー")
      .selectOption("click-middle");
    await page
      .getByRole("switch", { name: "マウスジェスチャーを有効にする", exact: true })
      .click();
    await page
      .getByRole("button", { name: "変更をすべて保存", exact: true })
      .click();
    await until(
      async () => !(await snapshot(page)).settings.value.gestures.enabled,
      "Host gesture setting not saved",
    );
    await page.screenshot({ path: path.join(profile, "host-gestures.png") });
    await page.evaluate(
      (extension) => window.dock.restartExtension(extension),
      id,
    );
    await until(
      async () =>
        (await state(page)).state === "running" &&
        (await state(page)).panel?.facts.some(
          (f) => f.label === "タブを閉じる" && f.value === "Ctrl+W",
        ),
      "Restart lost key",
    );
    const saved = JSON.parse(
      fs.readFileSync(path.join(profile, "settings.json")),
    );
    assert.equal(saved.extensions[id].settings["keys.close-tab"], "Ctrl+W");
    assert.equal(saved.extensions[id].settings["gestures.up"], "new-tab");
    assert.equal(saved.gestures.enabled, false);
    assert.equal(saved.gestures.bindings.length, 7);
    assert.equal(saved.gestures.bindings.find(row => row.id === "migrated.move-up").gesture, "click-middle");
    await page.evaluate(
      (extension) => window.dock.toggleExtension(extension, false),
      id,
    );
    await until(
      async () => (await state(page)).state === "stopped",
      "Deactivate failed",
    );
    assert.deepEqual(
      (await snapshot(page)).logs.filter((e) => e.level === "error"),
      [],
    );
    fs.writeFileSync(
      path.join(profile, "result.json"),
      JSON.stringify(
        {
          ok: true,
          profile,
          checks: [
            "native EXE and 11 commands",
            "legacy gesture migration and host-managed panel",
            "live key edit and restart",
            "host gesture edit, disable and old values preserved",
            "deactivation and no host errors",
          ],
        },
        null,
        2,
      ),
    );
    console.log(
      "PASS migration, host gestures, live keys, restart, native Applet and no errors",
    );
    console.log(profile);
  } finally {
    if (application) {
      try {
        fs.writeFileSync(
          path.join(profile, "snapshot.json"),
          JSON.stringify(
            await snapshot(await application.firstWindow()),
            null,
            2,
          ),
        );
      } catch {}
      await application.close();
    }
  }
})().catch((e) => {
  console.error(e);
  process.exitCode = 1;
});
