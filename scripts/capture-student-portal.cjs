const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright");
const sharp = require("sharp");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const outputDir = path.resolve(__dirname, "..", "artifacts", "screenshots");
fs.mkdirSync(outputDir, { recursive: true });

async function signIn(page) {
  await page.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  await page.getByLabel("Email").fill("student@nepktx.local");
  await page.getByLabel("Mật khẩu").fill(e2ePassword);
  await Promise.all([
    page.waitForLoadState("networkidle"),
    page.getByRole("button", { name: "Đăng nhập" }).click()
  ]);
}

function collectClientIssues(page, prefix, issues) {
  page.on("pageerror", error => issues.push(`${prefix} pageerror: ${error.message}`));
  page.on("console", message => {
    if (message.type() === "error") issues.push(`${prefix} console: ${message.text()}`);
  });
  page.on("requestfailed", request => issues.push(`${prefix} request failed: ${request.url()}`));
}

(async () => {
  const browser = await chromium.launch({
    executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
    headless: true
  });
  const issues = [];

  const desktop = await browser.newContext({ viewport: { width: 1586, height: 992 }, deviceScaleFactor: 1 });
  const desktopPage = await desktop.newPage();
  collectClientIssues(desktopPage, "desktop", issues);
  await signIn(desktopPage);

  if (!desktopPage.url().includes("/Student/Dashboard")) issues.push(`student redirect: ${desktopPage.url()}`);
  if ((await desktopPage.locator(".student-sidebar").count()) !== 1) issues.push("student sidebar missing");
  if ((await desktopPage.locator(".sidebar").count()) !== 0) issues.push("admin sidebar leaked into student portal");
  if (!(await desktopPage.locator(".room-pass").innerText()).includes("A-302")) issues.push("current room A-302 missing");
  if (!(await desktopPage.locator(".student-focus-card").innerText()).includes("1.525.000")) issues.push("current invoice amount missing");
  await desktopPage.screenshot({ path: path.join(outputDir, "student-dashboard-desktop.png") });

  await desktopPage.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  if (!desktopPage.url().includes("/Student/Dashboard")) issues.push(`authenticated student login route: ${desktopPage.url()}`);

  await desktopPage.goto(`${baseUrl}/Student/Rooms`, { waitUntil: "networkidle" });
  if ((await desktopPage.locator(".student-room-card").count()) < 1) issues.push("no assignable room cards");
  await desktopPage.locator("[data-apply-room]").first().click();
  if (!(await desktopPage.locator("[data-application-dialog]").isVisible())) issues.push("housing application dialog did not open");
  await desktopPage.locator("[data-dialog-close]").first().click();
  await desktopPage.screenshot({ path: path.join(outputDir, "student-rooms-desktop.png") });

  await desktopPage.goto(`${baseUrl}/Student/Dashboard`, { waitUntil: "networkidle" });
  await desktopPage.locator(".ai-launcher").click();
  await desktopPage.getByRole("button", { name: "Báo sự cố ở đâu?" }).click();
  await desktopPage.waitForTimeout(800);
  const chatText = await desktopPage.locator("[data-ai-assistant]").innerText();
  for (const expected of ["Trợ lý AI NẾP KTX", "Nguồn:", "Gửi yêu cầu hỗ trợ"]) {
    if (!chatText.includes(expected)) issues.push(`chat missing: ${expected}`);
  }
  await desktopPage.screenshot({ path: path.join(outputDir, "student-chat-desktop.png") });

  await desktopPage.goto(`${baseUrl}/Rooms/Map`, { waitUntil: "networkidle" });
  if (!desktopPage.url().includes("/Account/AccessDenied")) issues.push(`student reached admin route: ${desktopPage.url()}`);
  await desktop.close();

  const mobile = await browser.newContext({ viewport: { width: 430, height: 932 }, deviceScaleFactor: 1 });
  const mobilePage = await mobile.newPage();
  collectClientIssues(mobilePage, "mobile", issues);
  await signIn(mobilePage);
  await mobilePage.screenshot({ path: path.join(outputDir, "student-dashboard-mobile.png") });

  await mobilePage.locator(".ai-launcher").click();
  if (!(await mobilePage.locator("[data-ai-assistant]").isVisible())) issues.push("mobile AI panel did not open");
  await mobilePage.screenshot({ path: path.join(outputDir, "student-chat-mobile.png") });

  await mobilePage.locator("[data-chat-close]").click();
  await mobilePage.goto(`${baseUrl}/Student/Repairs#new-request`, { waitUntil: "networkidle" });
  if (!(await mobilePage.locator("[data-repair-dialog]").isVisible())) issues.push("repair deep-link dialog did not open");
  await mobilePage.locator("[data-dialog-close]").first().click();
  await mobile.close();

  const mobileDashboard = path.join(outputDir, "student-dashboard-mobile.png");
  const mobileChat = path.join(outputDir, "student-chat-mobile.png");
  await sharp({ create: { width: 884, height: 956, channels: 4, background: "#eef3f2" } })
    .composite([
      { input: mobileDashboard, left: 0, top: 12 },
      { input: mobileChat, left: 454, top: 12 }
    ])
    .png()
    .toFile(path.join(outputDir, "student-portal-mobile.png"));

  await browser.close();
  process.stdout.write(JSON.stringify({ outputDir, issues }, null, 2));
})().catch(error => {
  process.stderr.write(error.stack);
  process.exitCode = 1;
});
