const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const outputDir = path.resolve(__dirname, "..", "artifacts", "screenshots");
fs.mkdirSync(outputDir, { recursive: true });

const managerRoutes = [
  ["/", "Tổng quan vận hành"],
  ["/Rooms/Map", "Sơ đồ phòng"],
  ["/Rooms/Catalog", "Danh mục tòa, tầng và phòng"],
  ["/Students", "Sinh viên"],
  ["/Applications", "Xử lý đơn lưu trú"],
  ["/Billing", "Thu phí và công nợ"],
  ["/Maintenance", "Sự cố & bảo trì"],
  ["/Reports", "Báo cáo ký túc xá"],
  ["/Settings", "Cài đặt hệ thống"],
  ["/Account/ChangePassword", "Đổi mật khẩu"]
];

const studentRoutes = [
  ["/Student/Dashboard", "Chào An"],
  ["/Student/Rooms", "Tìm một chỗ ở phù hợp hơn"],
  ["/Student/Applications", "Đăng ký của tôi"],
  ["/Student/Invoices", "Hóa đơn và công nợ"],
  ["/Student/Repairs", "Yêu cầu hỗ trợ"],
  ["/Student/Notifications", "Thông báo"],
  ["/Account/ChangePassword", "Đổi mật khẩu"]
];

async function signIn(page, email) {
  await page.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Mật khẩu").fill(e2ePassword);
  await Promise.all([
    page.waitForLoadState("networkidle"),
    page.getByRole("button", { name: "Đăng nhập" }).click()
  ]);
}

function watch(page, label, issues) {
  page.on("pageerror", error => issues.push(`${label}: pageerror ${error.message}`));
  page.on("console", message => {
    if (message.type() === "error") issues.push(`${label}: console ${message.text()}`);
  });
  page.on("requestfailed", request => issues.push(`${label}: request failed ${request.url()}`));
}

async function auditRoutes(page, label, routes, issues, mobile = false) {
  for (const [route, heading] of routes) {
    const response = await page.goto(`${baseUrl}${route}`, { waitUntil: "networkidle" });
    if (!response || response.status() >= 400) issues.push(`${label} ${route}: HTTP ${response?.status()}`);
    const h1 = await page.locator("main h1").first().textContent().catch(() => "");
    if (!h1?.includes(heading)) issues.push(`${label} ${route}: expected h1 containing '${heading}', got '${h1?.trim()}'`);
    const deadLinks = await page.locator('main a[href="#"]').count();
    if (deadLinks) issues.push(`${label} ${route}: ${deadLinks} dead main-content link(s)`);
    const invalidPosts = await page.locator('form[method="post"]:not(:has(input[name="__RequestVerificationToken"]))').count();
    if (invalidPosts) issues.push(`${label} ${route}: ${invalidPosts} POST form(s) without antiforgery token`);
    const overflow = await page.evaluate(() => document.documentElement.scrollWidth > window.innerWidth + 1);
    if (overflow) issues.push(`${label} ${route}: horizontal overflow at ${await page.evaluate(() => innerWidth)}px`);
    if (!mobile && route === "/" && (await page.locator(".operations-spine").count()) !== 1)
      issues.push("manager dashboard: operations spine missing");
  }
}

(async () => {
  const browser = await chromium.launch({
    executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
    headless: true
  });
  const issues = [];

  const anonymous = await browser.newContext();
  const anonymousPage = await anonymous.newPage();
  const anonymousResponse = await anonymousPage.goto(`${baseUrl}/Reports`, { waitUntil: "networkidle" });
  if (!anonymousPage.url().includes("/Account/Login")) issues.push(`anonymous report access was not challenged: ${anonymousPage.url()}`);
  if (!anonymousResponse?.ok()) issues.push(`anonymous login redirect ended with HTTP ${anonymousResponse?.status()}`);
  await anonymous.close();

  const manager = await browser.newContext({ viewport: { width: 1586, height: 992 }, deviceScaleFactor: 1 });
  const managerPage = await manager.newPage();
  watch(managerPage, "manager desktop", issues);
  await signIn(managerPage, "manager@nepktx.local");
  await auditRoutes(managerPage, "manager desktop", managerRoutes, issues);

  await managerPage.goto(`${baseUrl}/Rooms/Map?Building=A&Room=A-302`, { waitUntil: "networkidle" });
  if (await managerPage.locator("[data-demo-message]").count())
    issues.push("room map still exposes demo-only management controls");
  const placementLink = managerPage.getByRole("link", { name: "Xếp sinh viên", exact: true });
  if (await placementLink.count() !== 1) {
    issues.push("room map placement action is not a real link");
  } else {
    const target = new URL(await placementLink.getAttribute("href"), baseUrl);
    if (!target.pathname.startsWith("/Applications") || target.searchParams.get("Search") !== "A-302")
      issues.push("room map placement link lost its selected-room context");
    await placementLink.click();
    await managerPage.waitForLoadState("networkidle");
    if (!(await managerPage.locator("main h1").innerText()).includes("Xử lý đơn lưu trú"))
      issues.push("room map placement action did not reach the application worklist");
  }

  await managerPage.goto(`${baseUrl}/Applications`, { waitUntil: "networkidle" });
  await managerPage.screenshot({ path: path.join(outputDir, "updated-applications-desktop.png"), fullPage: true });
  await managerPage.goto(`${baseUrl}/Students?Search=SV002106`, { waitUntil: "networkidle" });
  if (await managerPage.getByRole("button", { name: "Đặt lại mật khẩu", exact: true }).count() !== 1)
    issues.push("student account management controls are missing");
  await managerPage.screenshot({ path: path.join(outputDir, "updated-students-desktop.png"), fullPage: true });

  await managerPage.goto(`${baseUrl}/Reports`, { waitUntil: "networkidle" });
  const reportText = await managerPage.locator("main").innerText();
  for (const expected of ["Tỷ lệ thu kỳ", "Phát hành và thu ròng", "Kết quả trong kỳ", "Sức chứa và công nợ"])
    if (!reportText.includes(expected)) issues.push(`report missing '${expected}'`);
  const exportResponse = await managerPage.request.get(`${baseUrl}/Reports/ExportCsv`);
  if (!exportResponse.ok()) issues.push(`CSV export HTTP ${exportResponse.status()}`);
  const disposition = exportResponse.headers()["content-disposition"] || "";
  if (!disposition.includes("attachment")) issues.push("CSV export lacks attachment disposition");

  await managerPage.goto(`${baseUrl}/`, { waitUntil: "networkidle" });
  await managerPage.screenshot({ path: path.join(outputDir, "final-dashboard-desktop.png"), fullPage: true });
  await managerPage.goto(`${baseUrl}/Reports`, { waitUntil: "networkidle" });
  await managerPage.screenshot({ path: path.join(outputDir, "final-reports-desktop.png"), fullPage: true });
  await managerPage.goto(`${baseUrl}/Rooms/Catalog`, { waitUntil: "networkidle" });
  await managerPage.screenshot({ path: path.join(outputDir, "final-catalog-desktop.png"), fullPage: true });
  await manager.close();

  const managerMobile = await browser.newContext({ viewport: { width: 430, height: 932 }, deviceScaleFactor: 1 });
  const managerMobilePage = await managerMobile.newPage();
  watch(managerMobilePage, "manager mobile", issues);
  await signIn(managerMobilePage, "manager@nepktx.local");
  await auditRoutes(managerMobilePage, "manager mobile", managerRoutes, issues, true);
  await managerMobilePage.goto(`${baseUrl}/`, { waitUntil: "networkidle" });
  await managerMobilePage.screenshot({ path: path.join(outputDir, "final-dashboard-mobile.png"), fullPage: true });
  await managerMobile.close();

  const student = await browser.newContext({ viewport: { width: 1365, height: 920 }, deviceScaleFactor: 1 });
  const studentPage = await student.newPage();
  watch(studentPage, "student", issues);
  await signIn(studentPage, "student@nepktx.local");
  await auditRoutes(studentPage, "student", studentRoutes, issues);
  for (const protectedRoute of ["/Reports", "/Settings", "/Billing", "/Applications", "/Students", "/Rooms/Map", "/Rooms/Catalog"]) {
    await studentPage.goto(`${baseUrl}${protectedRoute}`, { waitUntil: "networkidle" });
    if (!studentPage.url().includes("/Account/AccessDenied"))
      issues.push(`student reached protected route ${protectedRoute}: ${studentPage.url()}`);
  }
  const studentExport = await studentPage.request.get(`${baseUrl}/Reports/ExportCsv`, { maxRedirects: 0 });
  if (![302, 403].includes(studentExport.status())) issues.push(`student CSV export returned ${studentExport.status()}`);
  await student.close();

  await browser.close();
  process.stdout.write(JSON.stringify({ routesChecked: managerRoutes.length * 2 + studentRoutes.length, screenshots: outputDir, issues }, null, 2));
  if (issues.length) process.exitCode = 1;
})().catch(error => {
  process.stderr.write(`${error.stack}\n`);
  process.exitCode = 1;
});
