const fs = require("fs");
const path = require("path");
const { chromium } = require("playwright");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const outputDir = path.resolve(__dirname, "..", "artifacts", "screenshots");
fs.mkdirSync(outputDir, { recursive: true });

const managerRoutes = [
  ["/", "Tổng quan"],
  ["/Rooms/Map", "Sơ đồ phòng"],
  ["/Rooms/Catalog", "Danh mục tòa, tầng và phòng"],
  ["/Students", "Sinh viên"],
  ["/Applications", "Đơn lưu trú"],
  ["/Billing", "Thu phí và công nợ"],
  ["/Maintenance", "Sự cố và bảo trì"],
  ["/Reports", "Báo cáo ký túc xá"],
  ["/Settings", "Thông tin hệ thống"],
  ["/Notifications", "Thông báo"],
  ["/Account/ChangePassword", "Đổi mật khẩu"]
];

const studentRoutes = [
  ["/Student/Dashboard", "Chào An"],
  ["/Student/Rooms", "Tìm phòng"],
  ["/Student/Applications", "Đăng ký của tôi"],
  ["/Student/Invoices", "Hóa đơn và công nợ"],
  ["/Student/Repairs", "Yêu cầu hỗ trợ"],
  ["/Student/Notifications", "Thông báo"],
  ["/Account/ChangePassword", "Đổi mật khẩu"]
];
const laptopRoutes = managerRoutes.filter(([route]) => ["/", "/Rooms/Map", "/Applications", "/Billing"].includes(route));

async function signIn(page, email) {
  await page.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(e2ePassword);
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
  page.on("requestfailed", request => {
    const reason = request.failure()?.errorText;
    if (reason !== "net::ERR_ABORTED") issues.push(`${label}: request failed ${request.url()} (${reason})`);
  });
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
    if (route === "/Rooms/Map") {
      if (mobile) {
        const toggle = page.locator("[data-filter-toggle]");
        await toggle.click();
        if (await toggle.getAttribute("aria-expanded") !== "true")
          issues.push(`${label} ${route}: mobile filter toggle did not announce its open state`);
      }
      await page.keyboard.press("Tab");
      const price = page.locator('.filter-fields select[name="MaxPrice"]');
      await price.focus();
      const usable = await price.evaluate(control => {
        const field = control.closest("label");
        const title = field.querySelector("span").getBoundingClientRect();
        const bounds = control.getBoundingClientRect();
        const style = getComputedStyle(control);
        const outset = style.outlineStyle === "none" ? 0 : Math.max(0, parseFloat(style.outlineWidth) + parseFloat(style.outlineOffset));
        return bounds.top - outset >= title.bottom && bounds.bottom <= field.getBoundingClientRect().bottom
          && (outset > 0 || getComputedStyle(field).outlineStyle !== "none");
      });
      if (!usable) issues.push(`${label} ${route}: price filter focus overlaps its label or has no visible outline`);
      if (await price.evaluate(control => getComputedStyle(control).appearance === "base-select")) {
        await page.keyboard.press("Space");
        const pickerOpen = await price.evaluate(control => control.matches(":open") &&
          getComputedStyle(control, "::picker(select)").borderRadius === "8px");
        if (!pickerOpen) issues.push(`${label} ${route}: keyboard did not open the styled price picker`);
        if (label === "manager desktop" && pickerOpen)
          await page.screenshot({ path: path.join(outputDir, "room-filter-picker-desktop.png") });
        await page.keyboard.press("Escape");
        if (await price.evaluate(control => control.matches(":open")))
          issues.push(`${label} ${route}: Escape did not close the price picker`);
      }
    }
    if (!mobile && route === "/" && (await page.locator(".attention-focus").count()) !== 1)
      issues.push("manager dashboard: attention section missing");
  }
}

async function auditSearchAndReset(page, issues) {
  await page.goto(`${baseUrl}/Rooms/Map`, { waitUntil: "networkidle" });
  const roomLink = page.locator(".room-detail .text-button").first();
  if (!await roomLink.evaluate(link => getComputedStyle(link).textDecorationLine === "none"
    && getComputedStyle(link).display === "inline-flex"))
    issues.push("room map: detail navigation did not use the secondary action style");
  const search = page.locator("[data-global-search]");
  await page.keyboard.press("Control+k");
  if (!await search.evaluate(input => input === document.activeElement && getComputedStyle(input).outlineStyle === "none"
    && getComputedStyle(input.form).outlineStyle === "solid"))
    issues.push("global search: Ctrl+K did not focus the field with a single outer focus ring");
  await search.fill("b 301");
  await Promise.all([
    page.waitForURL(url => url.pathname === "/Rooms/Map" && url.searchParams.get("Room") === "B-301"),
    search.press("Enter")
  ]);
  if (!await page.locator(".room-card.selected").getByText("B-301", { exact: true }).isVisible())
    issues.push("global search: the normalized room code did not select the matching room");

  const price = page.locator('.filter-fields select[name="MaxPrice"]');
  await Promise.all([
    page.waitForURL(url => url.searchParams.get("MaxPrice") === "1250000"),
    price.selectOption("1250000")
  ]);
  const reset = page.locator(".filter-bar").getByRole("link", { name: "Xóa bộ lọc" });
  await Promise.all([
    page.waitForURL(url => url.searchParams.get("Building") === "B" && !url.searchParams.has("MaxPrice")),
    reset.click()
  ]);
  if (await price.inputValue() !== "") issues.push("room map: clearing filters left the price filter active");

  await search.fill("SV002106");
  await Promise.all([
    page.waitForURL(url => url.pathname.startsWith("/Students") && url.searchParams.get("Search") === "SV002106"),
    page.locator(".global-search").getByRole("button", { name: "Tìm kiếm" }).click()
  ]);
  if (!await page.locator("[data-record-list]").getByText(/SV002106/).isVisible())
    issues.push("global search: the search button did not find the student");
}

async function auditRecordViews(page, label, issues, { mobile = false, capture = false } = {}) {
  for (const route of ["/Students", "/Applications", "/Billing", "/Maintenance"]) {
    const params = route === "/Students" ? "?Page=2" : "";
    await page.goto(`${baseUrl}${route}${params}`, { waitUntil: "networkidle" });
    const switcher = page.getByRole("group", { name: "Chế độ xem kết quả" });
    const listButton = switcher.getByRole("button", { name: "Danh sách", exact: true });
    const queue = page.locator("[data-record-queue]");
    const detail = page.locator("[data-record-detail]");
    await listButton.click();
    if (!await queue.isVisible() || await detail.isVisible())
      issues.push(`${label} ${route}: list mode does not separate results from the detail`);
    if (!mobile && !await queue.locator(".record-list-columns").isVisible())
      issues.push(`${label} ${route}: table headings are missing in full-width mode`);
    if (!mobile) {
      const columnsAlign = await queue.evaluate(results => {
        const headings = [...results.querySelectorAll(".record-list-columns > span")];
        const row = results.querySelector("[data-record-list] > a");
        const cells = [...(row?.children ?? [])].filter(cell => cell.getClientRects().length);
        if (cells.length !== headings.length || cells.length === 0) return false;
        return cells.every((cell, index) => {
          const cellBounds = cell.getBoundingClientRect();
          const headingBounds = headings[index].getBoundingClientRect();
          const rowBounds = row.getBoundingClientRect();
          return Math.abs(cellBounds.left - headingBounds.left) <= 2
            && Math.abs(cellBounds.top + cellBounds.height / 2 - (rowBounds.top + rowBounds.height / 2)) <= 7;
        });
      });
      if (!columnsAlign) issues.push(`${label} ${route}: row cells are not aligned beneath their headings`);
    }
    if (params && !(await queue.getByRole("navigation", { name: /Phân trang/ }).innerText()).includes("Trang 2 /"))
      issues.push(`${label} ${route}: expected to browse page 2`);
    if (capture)
      await page.screenshot({ path: path.join(outputDir, `records-${route.slice(1).toLowerCase()}-desktop.png`) });

    await queue.locator("[data-record-list] > a").nth(1).click();
    await page.waitForURL(url => url.hash === "#selected-case");
    if (!await detail.isVisible() || await detail.getAttribute("tabindex") !== "-1")
      issues.push(`${label} ${route}: opening a row did not show its detail`);
    if (params && new URL(page.url()).searchParams.get("Page") !== "2")
      issues.push(`${label} ${route}: opening a row lost the result page`);
    if (mobile && await queue.isVisible())
      issues.push(`${label} ${route}: queue competes with the selected detail on mobile`);
    await listButton.click();
    if (!await queue.isVisible() || await detail.isVisible())
      issues.push(`${label} ${route}: returning to the list failed`);
  }
}

let browser;
(async () => {
  browser = await chromium.launch({
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
  await auditSearchAndReset(managerPage, issues);

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
    if (!(await managerPage.locator("main h1").innerText()).includes("Đơn lưu trú"))
      issues.push("room map placement action did not reach the application worklist");
  }

  await managerPage.goto(`${baseUrl}/Applications`, { waitUntil: "networkidle" });
  await managerPage.screenshot({ path: path.join(outputDir, "updated-applications-desktop.png"), fullPage: true });
  await managerPage.goto(`${baseUrl}/Students?Search=SV002106`, { waitUntil: "networkidle" });
  if (await managerPage.getByRole("button", { name: "Đặt lại mật khẩu", exact: true }).count() !== 1)
    issues.push("student account management controls are missing");
  await managerPage.screenshot({ path: path.join(outputDir, "updated-students-desktop.png"), fullPage: true });
  await auditRecordViews(managerPage, "manager desktop", issues, { capture: true });

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
  await managerPage.setViewportSize({ width: 1200, height: 900 });
  await auditRoutes(managerPage, "manager laptop", laptopRoutes, issues);
  const desktopRooms = managerPage.getByRole("group", { name: "Lưu trú", exact: true });
  if (!await desktopRooms.getByText("Danh mục phòng", { exact: true }).isVisible())
    issues.push("manager laptop: room catalogue label is hidden");
  await manager.close();

  const managerMobile = await browser.newContext({ viewport: { width: 430, height: 932 }, deviceScaleFactor: 1 });
  const managerMobilePage = await managerMobile.newPage();
  watch(managerMobilePage, "manager mobile", issues);
  await signIn(managerMobilePage, "manager@nepktx.local");
  await auditRoutes(managerMobilePage, "manager mobile", managerRoutes, issues, true);
  await auditRecordViews(managerMobilePage, "manager mobile", issues, { mobile: true });
  await managerMobilePage.goto(`${baseUrl}/`, { waitUntil: "networkidle" });
  await managerMobilePage.screenshot({ path: path.join(outputDir, "final-dashboard-mobile.png"), fullPage: true });
  await managerMobilePage.getByRole("button", { name: "Mở menu" }).click();
  const drawer = managerMobilePage.locator(".sidebar.open");
  if (!await drawer.getByRole("group", { name: "Vận hành", exact: true }).getByText("Sự cố và bảo trì", { exact: true }).isVisible())
    issues.push("manager mobile: maintenance route is missing from the drawer");
  const logout = drawer.getByRole("button", { name: "Đăng xuất" });
  if (!await logout.evaluate(button => {
    const rect = button.getBoundingClientRect();
    return button.contains(document.elementFromPoint(rect.x + rect.width / 2, rect.y + rect.height / 2));
  }))
    issues.push("manager mobile: logout is missing from the drawer");
  await managerMobilePage.screenshot({ path: path.join(outputDir, "final-manager-menu-mobile.png") });
  await drawer.getByRole("link", { name: "Danh mục phòng", exact: true }).click();
  if (await managerMobilePage.locator(".sidebar.open").count()) issues.push("manager mobile: drawer stayed open after navigation");
  await managerMobile.close();

  const admin = await browser.newContext({ viewport: { width: 1440, height: 900 } });
  const adminPage = await admin.newPage();
  watch(adminPage, "admin desktop", issues);
  await signIn(adminPage, "admin@nepktx.local");
  await auditRecordViews(adminPage, "admin desktop", issues);
  await admin.close();

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
  await studentPage.setViewportSize({ width: 430, height: 932 });
  await auditRoutes(studentPage, "student mobile", studentRoutes, issues, true);
  await student.close();

  await browser.close();
  process.stdout.write(JSON.stringify({ routesChecked: (managerRoutes.length + studentRoutes.length) * 2 + laptopRoutes.length, screenshots: outputDir, issues }, null, 2));
  if (issues.length) process.exitCode = 1;
})().catch(async error => {
  await browser?.close();
  process.stderr.write(`${error.stack}\n`);
  process.exitCode = 1;
});
