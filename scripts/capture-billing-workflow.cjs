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
  const response = await page.goto(`${baseUrl}/Billing/Index?Status=overdue`, { waitUntil: "networkidle" });
  if (!response?.ok()) issues.push(`billing returned ${response?.status()}`);
  const pageText = await page.locator(".billing-page").innerText();
  for (const expected of ["Thu phí và công nợ", "Quá hạn", "HD-MGR-0001", "Ghi nhận khoản thu", "Lịch sử thu và điều chỉnh"]) {
    if (!pageText.includes(expected)) issues.push(`billing screen missing: ${expected}`);
  }
  if ((await page.locator(".billing-queue-item").count()) < 2) issues.push("expected at least two overdue invoices");
  await page.screenshot({ path: path.join(outputDir, "billing-workflow-desktop.png"), fullPage: true });
  await desktop.close();

  const mobile = await browser.newContext({ viewport: { width: 430, height: 932 }, deviceScaleFactor: 1 });
  const mobilePage = await mobile.newPage();
  watch(mobilePage, "mobile", issues);
  await signIn(mobilePage);
  await mobilePage.goto(`${baseUrl}/Billing/Index?Status=overdue`, { waitUntil: "networkidle" });
  await mobilePage.screenshot({ path: path.join(outputDir, "billing-workflow-mobile-list.png") });
  await mobilePage.evaluate(() => {
    const detail = document.querySelector(".billing-detail");
    if (detail) window.scrollTo({ top: Math.max(0, detail.offsetTop - 78), behavior: "instant" });
  });
  await mobilePage.screenshot({ path: path.join(outputDir, "billing-workflow-mobile-detail.png") });
  await mobile.close();

  await sharp({ create: { width: 884, height: 956, channels: 4, background: "#eef3f2" } })
    .composite([
      { input: path.join(outputDir, "billing-workflow-mobile-list.png"), left: 0, top: 12 },
      { input: path.join(outputDir, "billing-workflow-mobile-detail.png"), left: 454, top: 12 }
    ])
    .png()
    .toFile(path.join(outputDir, "billing-workflow-mobile.png"));

  const student = await browser.newContext({ viewport: { width: 1365, height: 920 }, deviceScaleFactor: 1 });
  const studentPage = await student.newPage();
  watch(studentPage, "student", issues);
  await signIn(studentPage, "student@nepktx.local");
  await studentPage.goto(`${baseUrl}/Student/Invoices`, { waitUntil: "networkidle" });
  const studentText = await studentPage.locator(".student-main").innerText();
  for (const expected of ["Hóa đơn và công nợ", "1.525.000", "Đã cân đối", "Lịch sử khoản thu"]) {
    if (!studentText.includes(expected)) issues.push(`student invoice screen missing: ${expected}`);
  }
  await studentPage.screenshot({ path: path.join(outputDir, "student-invoices-ledger.png"), fullPage: true });
  await studentPage.goto(`${baseUrl}/Billing/Index`, { waitUntil: "networkidle" });
  if (!studentPage.url().includes("/Account/AccessDenied")) issues.push(`student reached manager billing route: ${studentPage.url()}`);
  await student.close();

  await browser.close();
  process.stdout.write(JSON.stringify({ outputDir, issues }, null, 2));
  if (issues.length) process.exitCode = 1;
})().catch(error => {
  process.stderr.write(error.stack);
  process.exitCode = 1;
});
