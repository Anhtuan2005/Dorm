const assert = require("node:assert/strict");
const { chromium } = require("playwright");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

assert.equal(new URL(baseUrl).hostname, "127.0.0.1");
assert.match(process.env.DORM_BROWSER_FIXTURE_DB ?? "", /^DormitoryManagementSystem_Test_[a-f0-9]{32}$/);

const loginPath = "/Account/Login";
const studentEmail = "student@nepktx.local";
const adminLock = "Tài khoản đã bị quản trị viên khóa. Vui lòng liên hệ Ban quản lý.";
const retryLock = "Tài khoản tạm khóa do đăng nhập sai nhiều lần.";

async function signIn(page, email, password = e2ePassword) {
  await page.goto(baseUrl + loginPath);
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(password);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.waitForLoadState("domcontentloaded");
}

async function openStudentTab(context, blockHub = false) {
  const page = await context.newPage();
  if (blockHub) await page.route("**/hubs/operations/**", route => route.abort());
  const snapshot = blockHub ? null : page.waitForResponse(response => response.url().includes("/Student/NotificationSnapshot") && response.ok());
  await page.goto(`${baseUrl}/Student/Dashboard`);
  if (snapshot) await snapshot;
  return page;
}

async function run() {
  const browser = await chromium.launch({ channel: "msedge" });
  try {
    const admin = await browser.newPage();
    await signIn(admin, "admin@nepktx.local");
    await admin.locator('.main-nav a[href="/Rooms/Map"]').click();
    await admin.locator("main").getByRole("link", { name: "Danh mục phòng", exact: true }).click();
    await admin.getByRole("button", { name: "Thêm phòng", exact: true }).click();
    const room = admin.locator("#create-room-dialog");
    const building = (await room.locator('[name="Code"]').getAttribute("placeholder")).split("-")[0];
    const roomCode = `${building}-QA101`;
    await room.locator('[name="Code"]').fill(roomCode);
    await room.locator('[name="RoomType"]').selectOption("Nam");
    await room.getByRole("checkbox", { name: "Wi-Fi", exact: true }).check();
    await room.getByRole("button", { name: "Tạo phòng", exact: true }).click();
    await admin.getByRole("table").getByText(roomCode, { exact: true }).waitFor();
    await admin.goto(`${baseUrl}/Students?Search=${studentEmail}`);

    const studentContext = await browser.newContext();
    const student = await studentContext.newPage();
    await signIn(student, studentEmail);
    await student.waitForURL("**/Student/Dashboard");

    const changedPassword = `${e2ePassword}Updated`;
    await student.goto(baseUrl + "/Account/ChangePassword");
    await student.getByLabel("Mật khẩu hiện tại", { exact: true }).fill(e2ePassword);
    await student.getByLabel("Mật khẩu mới", { exact: true }).fill(changedPassword);
    await student.getByLabel("Nhập lại mật khẩu mới", { exact: true }).fill(changedPassword);
    await student.getByRole("button", { name: "Đổi mật khẩu", exact: true }).click();
    await student.waitForURL("**/Student/Dashboard");
    await student.locator('form[action="/Account/Logout"] button').click();
    await student.waitForURL(url => url.pathname === loginPath);
    await signIn(student, studentEmail, changedPassword);
    await student.waitForURL("**/Student/Dashboard");

    await admin.getByRole("button", { name: "Đặt lại mật khẩu", exact: true }).click();
    const reset = admin.locator("#reset-password-dialog");
    await reset.locator('[name="TemporaryPassword"]').fill(e2ePassword);
    await reset.getByRole("button", { name: "Cập nhật mật khẩu", exact: true }).click();
    await admin.getByRole("status").filter({ hasText: "Đã đặt lại mật khẩu" }).waitFor();
    await signIn(student, studentEmail);
    await student.waitForURL("**/Student/Dashboard");
    const second = await openStudentTab(studentContext);
    const siblingWithoutHub = await openStudentTab(studentContext, true);
    const fallbackContext = await browser.newContext();
    const fallbackLogin = await fallbackContext.newPage();
    await signIn(fallbackLogin, studentEmail);
    const fallback = await openStudentTab(fallbackContext, true);
    await fallbackLogin.close();
    const cookie = (await studentContext.cookies()).find(entry => entry.name === ".NepKtx.Auth");
    assert.ok(cookie);

    admin.once("dialog", dialog => dialog.accept());
    await admin.getByRole("button", { name: "Khóa tài khoản", exact: true }).click();
    await admin.getByRole("button", { name: "Mở khóa", exact: true }).waitFor();

    const fresh = await browser.newPage();
    await signIn(fresh, studentEmail);
    const results = await Promise.allSettled([
      fresh.getByRole("alert").getByText(adminLock, { exact: true }).waitFor({ timeout: 1800 }),
      student.waitForURL(url => url.pathname === loginPath, { timeout: 5000 }),
      second.waitForURL(url => url.pathname === loginPath, { timeout: 5000 }),
      siblingWithoutHub.waitForURL(url => url.pathname === loginPath, { timeout: 5000 }),
      fallback.waitForURL(url => url.pathname === loginPath, { timeout: 20000 })
    ]);
    const checks = ["admin lock message", "open session", "second tab", "inter-tab lock event", "SignalR fallback"];
    const failures = results.flatMap((result, index) => result.status === "rejected" ? [checks[index]] : []);
    assert.deepEqual(failures, [], `Failed checks: ${failures.join(", ")}. Login said: ${await fresh.getByRole("alert").innerText()}`);
    for (const tab of [student, second, siblingWithoutHub, fallback]) {
      assert.equal(new URL(tab.url()).searchParams.get("reason"), "locked");
      await tab.getByRole("alert").getByText(adminLock, { exact: true }).waitFor({ timeout: 5000 });
    }

    const replay = await browser.newContext();
    await replay.addCookies([cookie]);
    async function assertOldCookieRejected() {
      const response = await replay.request.get(`${baseUrl}/Student/Invoices`, { maxRedirects: 0 });
      assert.equal(response.status(), 302);
      assert.match(response.headers().location, /\/Account\/Login/);
    }
    await assertOldCookieRejected();

    admin.once("dialog", dialog => dialog.accept());
    await admin.getByRole("button", { name: "Mở khóa", exact: true }).click();
    await admin.getByRole("button", { name: "Khóa tài khoản", exact: true }).waitFor();
    await replay.addCookies([cookie]);
    await assertOldCookieRejected();
    await signIn(student, studentEmail);
    await student.waitForURL("**/Student/Dashboard");

    for (let attempt = 0; attempt < 5; attempt++) await signIn(fresh, studentEmail, "Incorrect1!");
    await fresh.getByRole("alert").getByText(retryLock, { exact: true }).waitFor();
    console.log("Browser smoke passed: room creation, lock reasons in every tab, SignalR fallback, old cookie rejection, password change/reset, logout and retry lock.");
  } finally {
    await browser.close();
  }
}

run().catch(error => { console.error(error.message); process.exitCode = 1; });
