const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright");
const sharp = require("sharp");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const outputDir = path.resolve(__dirname, "..", "artifacts", "screenshots");
fs.mkdirSync(outputDir, { recursive: true });

async function signIn(page) {
  await page.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  await page.getByLabel("Email").fill("manager@nepktx.local");
  await page.getByLabel("Mật khẩu").fill(e2ePassword);
  await Promise.all([
    page.waitForLoadState("networkidle"),
    page.getByRole("button", { name: "Đăng nhập" }).click()
  ]);
}

(async () => {
  const browser = await chromium.launch({
    executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
    headless: true
  });
  const issues = [];

  const desktop = await browser.newContext({ viewport: { width: 1586, height: 992 }, deviceScaleFactor: 1 });
  const desktopPage = await desktop.newPage();
  desktopPage.on("pageerror", error => issues.push(`desktop pageerror: ${error.message}`));
  desktopPage.on("console", message => { if (message.type() === "error") issues.push(`desktop console: ${message.text()}`); });
  await signIn(desktopPage);
  await desktopPage.goto(`${baseUrl}/Rooms/Map?Building=A&Room=A-302`, { waitUntil: "networkidle" });
  const buildingLabels = await desktopPage.locator(".building-tabs a strong").allTextContents();
  if (JSON.stringify(buildingLabels) !== JSON.stringify(["Tòa A", "Tòa B", "Tòa C", "Tòa D"])) issues.push(`building tabs: ${buildingLabels.join(", ")}`);
  const totalSummary = await desktopPage.locator(".capacity-summary").innerText();
  for (const expected of ["1.280", "1.142", "24", "94", "20"]) if (!totalSummary.includes(expected)) issues.push(`missing total ${expected}`);
  if ((await desktopPage.locator(".room-detail h2").innerText()) !== "Phòng A-302") issues.push("selected detail does not match A-302");
  await desktopPage.screenshot({ path: path.join(outputDir, "room-map-desktop.png") });
  await desktopPage.goto(`${baseUrl}/Rooms/Map?Building=A&Status=held&OnlyAvailable=true`, { waitUntil: "networkidle" });
  if (!(await desktopPage.locator(".empty-state").isVisible())) issues.push("empty filter state is not visible");
  await desktop.close();

  const mobile = await browser.newContext({ viewport: { width: 430, height: 932 }, deviceScaleFactor: 1 });
  const mobilePage = await mobile.newPage();
  mobilePage.on("pageerror", error => issues.push(`mobile pageerror: ${error.message}`));
  mobilePage.on("console", message => { if (message.type() === "error") issues.push(`mobile console: ${message.text()}`); });
  await signIn(mobilePage);
  await mobilePage.goto(`${baseUrl}/Rooms/Map?Building=A&Floor=3&Room=A-302`, { waitUntil: "networkidle" });
  if ((await mobilePage.locator(".floor-row:visible").count()) !== 1) issues.push("mobile must show exactly one floor");
  await mobilePage.screenshot({ path: path.join(outputDir, "room-map-mobile-map.png") });
  await mobilePage.getByRole("link", { name: /A-302, 6 trên 8/ }).click();
  await mobilePage.waitForLoadState("networkidle");
  await mobilePage.screenshot({ path: path.join(outputDir, "room-map-mobile-detail.png") });
  await mobile.close();

  const mobileMap = path.join(outputDir, "room-map-mobile-map.png");
  const mobileDetail = path.join(outputDir, "room-map-mobile-detail.png");
  await sharp({ create: { width: 884, height: 956, channels: 4, background: "#eef3f2" } })
    .composite([
      { input: mobileMap, left: 0, top: 12 },
      { input: mobileDetail, left: 454, top: 12 }
    ])
    .png()
    .toFile(path.join(outputDir, "room-map-mobile.png"));

  await browser.close();
  process.stdout.write(JSON.stringify({ outputDir, issues }, null, 2));
})().catch(error => {
  process.stderr.write(error.stack);
  process.exitCode = 1;
});
