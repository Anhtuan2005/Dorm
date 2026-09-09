const fs = require("fs");
const path = require("path");
const { chromium } = require("C:\\Users\\Admin\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\node\\node_modules\\playwright");
const sharp = require("C:\\Users\\Admin\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\node\\node_modules\\sharp");

const baseUrl = "http://127.0.0.1:5189";
const outputDir = path.resolve(__dirname, "..", "artifacts", "screenshots");
fs.mkdirSync(outputDir, { recursive: true });

async function signIn(page) {
  await page.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  await page.getByLabel("Email").fill("manager@nepktx.local");
  await page.getByLabel("Mật khẩu").fill("Demo@123");
  await Promise.all([
    page.waitForLoadState("networkidle"),
    page.getByRole("button", { name: "Đăng nhập" }).click()
  ]);
}

function watch(page, prefix, issues) {
  page.on("pageerror", error => issues.push(`${prefix} pageerror: ${error.message}`));
  page.on("console", message => { if (message.type() === "error") issues.push(`${prefix} console: ${message.text()}`); });
  page.on("requestfailed", request => issues.push(`${prefix} request failed: ${request.url()}`));
}

(async () => {
  const browser = await chromium.launch({ executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", headless: true });
  const issues = [];

  const desktop = await browser.newContext({ viewport: { width: 1586, height: 992 }, deviceScaleFactor: 1 });
  const page = await desktop.newPage();
  watch(page, "desktop", issues);
  await signIn(page);
  await page.goto(`${baseUrl}/Applications/Index?Scope=attention`, { waitUntil: "networkidle" });
  if ((await page.locator(".request-queue-item").count()) !== 6) issues.push("expected six active requests");
  const pageText = await page.locator(".applications-page").innerText();
  for (const expected of ["4", "DK-2026-0108", "C-504", "Còn 8 chỗ", "Duyệt và xếp phòng"]) {
    if (!pageText.includes(expected)) issues.push(`application screen missing: ${expected}`);
  }
  await page.screenshot({ path: path.join(outputDir, "application-workflow-desktop.png") });
  await page.getByRole("button", { name: "Yêu cầu bổ sung" }).click();
  if (!(await page.locator("#information-dialog").isVisible())) issues.push("information dialog did not open");
  await page.locator("#information-dialog [data-dialog-close]").first().click();

  await page.goto(`${baseUrl}/Students/Index?Search=SV002106`, { waitUntil: "networkidle" });
  const studentText = await page.locator(".students-page").innerText();
  for (const expected of ["Nguyễn Minh An", "A-302", "student@nepktx.local", "Đang hoạt động"]) {
    if (!studentText.includes(expected)) issues.push(`student directory missing: ${expected}`);
  }
  await page.screenshot({ path: path.join(outputDir, "student-directory-desktop.png") });
  await page.goto(`${baseUrl}/Student/Dashboard`, { waitUntil: "networkidle" });
  if (!page.url().includes("/Account/AccessDenied")) issues.push(`manager reached student route: ${page.url()}`);
  await desktop.close();

  const mobile = await browser.newContext({ viewport: { width: 430, height: 932 }, deviceScaleFactor: 1 });
  const mobilePage = await mobile.newPage();
  watch(mobilePage, "mobile", issues);
  await signIn(mobilePage);
  await mobilePage.goto(`${baseUrl}/Applications/Index?Scope=attention`, { waitUntil: "networkidle" });
  await mobilePage.screenshot({ path: path.join(outputDir, "application-workflow-mobile-queue.png") });
  await mobilePage.evaluate(() => {
    const casePanel = document.querySelector(".application-case");
    if (casePanel) window.scrollTo({ top: Math.max(0, casePanel.offsetTop - 116), behavior: "instant" });
  });
  await mobilePage.screenshot({ path: path.join(outputDir, "application-workflow-mobile-case.png") });
  await mobile.close();

  await sharp({ create: { width: 884, height: 956, channels: 4, background: "#eef3f2" } })
    .composite([
      { input: path.join(outputDir, "application-workflow-mobile-queue.png"), left: 0, top: 12 },
      { input: path.join(outputDir, "application-workflow-mobile-case.png"), left: 454, top: 12 }
    ])
    .png()
    .toFile(path.join(outputDir, "application-workflow-mobile.png"));

  await browser.close();
  process.stdout.write(JSON.stringify({ outputDir, issues }, null, 2));
})().catch(error => {
  process.stderr.write(error.stack);
  process.exitCode = 1;
});
