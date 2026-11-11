const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright");
const sharp = require("sharp");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const outputDir = path.resolve(__dirname, "..", "artifacts", "screenshots");
fs.mkdirSync(outputDir, { recursive: true });

async function signIn(page, email = "manager@nepktx.local") {
  await page.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Mật khẩu").fill(e2ePassword);
  await Promise.all([
    page.waitForNavigation({ waitUntil: "networkidle" }),
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
  const response = await page.goto(`${baseUrl}/Maintenance/Index?Status=open`, { waitUntil: "networkidle" });
  if (!response?.ok()) issues.push(`maintenance returned ${response?.status()}`);
  const text = (await page.locator(".maintenance-page").innerText()).toLocaleLowerCase("vi");
  for (const expected of ["Sự cố & bảo trì", "Khẩn cấp", "SC-0284", "Ảnh hưởng sức chứa", "Cập nhật sự cố"]) {
    if (!text.includes(expected.toLocaleLowerCase("vi"))) issues.push(`maintenance screen missing: ${expected}`);
  }
  if ((await page.locator(".maintenance-ticket").count()) < 5) issues.push("expected at least five open incidents");
  await page.screenshot({ path: path.join(outputDir, "maintenance-workflow-desktop.png"), fullPage: true });
  await desktop.close();

  const mobile = await browser.newContext({ viewport: { width: 430, height: 932 }, deviceScaleFactor: 1 });
  const mobilePage = await mobile.newPage();
  watch(mobilePage, "mobile", issues);
  await signIn(mobilePage);
  await mobilePage.goto(`${baseUrl}/Maintenance/Index?Status=open`, { waitUntil: "networkidle" });
  await mobilePage.screenshot({ path: path.join(outputDir, "maintenance-workflow-mobile-list.png") });
  await mobilePage.evaluate(() => {
    const detail = document.querySelector(".maintenance-detail");
    if (detail) window.scrollTo({ top: Math.max(0, detail.offsetTop - 78), behavior: "instant" });
  });
  await mobilePage.screenshot({ path: path.join(outputDir, "maintenance-workflow-mobile-detail.png") });
  await mobile.close();

  await sharp({ create: { width: 884, height: 956, channels: 4, background: "#eef3f2" } })
    .composite([
      { input: path.join(outputDir, "maintenance-workflow-mobile-list.png"), left: 0, top: 12 },
      { input: path.join(outputDir, "maintenance-workflow-mobile-detail.png"), left: 454, top: 12 }
    ])
    .png()
    .toFile(path.join(outputDir, "maintenance-workflow-mobile.png"));

  const student = await browser.newContext({ viewport: { width: 1200, height: 800 } });
  const studentPage = await student.newPage();
  await signIn(studentPage, "student@nepktx.local");
  await studentPage.goto(`${baseUrl}/Maintenance/Index`, { waitUntil: "networkidle" });
  if (!studentPage.url().includes("/Account/AccessDenied")) issues.push(`student reached manager maintenance route: ${studentPage.url()}`);
  await student.close();

  await browser.close();
  process.stdout.write(JSON.stringify({ outputDir, issues }, null, 2));
  if (issues.length) process.exitCode = 1;
})().catch(error => {
  process.stderr.write(error.stack);
  process.exitCode = 1;
});
