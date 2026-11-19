const assert = require("node:assert/strict");
const { chromium } = require("playwright");
const { baseUrl, e2ePassword } = require("../../scripts/e2e-config.cjs");

assert.equal(new URL(baseUrl).hostname, "127.0.0.1");
assert.match(process.env.DORM_BROWSER_FIXTURE_DB ?? "", /^DormitoryManagementSystem_Test_[a-f0-9]{32}$/);

async function signIn(browser, email, configure = () => {}) {
  const context = await browser.newContext();
  const page = await context.newPage();
  configure(page);
  await page.goto(`${baseUrl}/Account/Login`);
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(e2ePassword);
  await Promise.all([
    page.waitForURL(url => url.pathname !== "/Account/Login"),
    page.getByRole("button", { name: "Đăng nhập", exact: true }).click()
  ]);
  return page;
}

async function submitForm(page, form, fields) {
  const post = await form.evaluate(element => ({
    url: element.action,
    fields: Object.fromEntries(new FormData(element))
  }));
  const response = await page.request.post(post.url, {
    form: { ...post.fields, ...fields },
    maxRedirects: 0
  });
  assert.equal(response.status(), 302);
  await page.goto(new URL(response.headers().location, baseUrl).href);
}

async function recordCollection(manager) {
  await manager.goto(`${baseUrl}/Billing?Search=HD-2026-09-02106`);
  await submitForm(manager, manager.locator("form.collection-form"), {
    Method: "Cash", Amount: "1525000", ExternalReference: ""
  });
  const history = await manager.locator(".billing-history").innerText();
  assert.match(history, /THU-\d{8}-\d{6}/,
    (await manager.locator(".workspace-flash.error").allTextContents()).join(" ") || "The receipt did not appear.");
}

async function reverseCollection(manager) {
  const dialogId = await manager.locator(".billing-history-list [data-open-dialog]").getAttribute("data-open-dialog");
  assert.match(dialogId, /^reverse-\d+$/);
  await submitForm(manager, manager.locator(`#${dialogId} form`), { Reason: "Hoàn tác khoản thu kiểm thử" });
  assert.match(await manager.locator(".billing-history").innerText(), /Đã hoàn tác/);
}

async function run() {
  const browser = await chromium.launch({ channel: "msedge" });
  try {
    const student = await signIn(browser, "student@nepktx.local");
    const manager = await signIn(browser, "manager@nepktx.local");
    await manager.goto(`${baseUrl}/Students?Search=SV000001`);
    const otherEmail = "realtime-fixture@student.nepktx.local";
    await submitForm(manager, manager.locator("#create-account-dialog form"), {
      Email: otherEmail, TemporaryPassword: e2ePassword
    });
    const otherSignals = [];
    const other = await signIn(browser, otherEmail, page => page.on("websocket", socket => {
      socket.on("framereceived", ({ payload }) => {
        const signal = String(payload).match(/StudentNotificationsChanged|OperationsAttentionChanged/);
        if (signal) otherSignals.push(signal[0]);
      });
    }));
    const otherConnected = other.waitForResponse(response =>
      new URL(response.url()).pathname === "/Student/NotificationSnapshot" && response.ok());
    await other.goto(`${baseUrl}/Student/Notifications`);
    await otherConnected;

    const anonymous = await browser.newContext();
    const blockedHub = await anonymous.request.post(`${baseUrl}/hubs/operations/negotiate?negotiateVersion=1`, { maxRedirects: 0 });
    assert.ok([302, 401].includes(blockedHub.status()));
    await anonymous.close();
    const blockedStaffSnapshot = await other.request.get(`${baseUrl}/Home/AttentionSnapshot`, { maxRedirects: 0 });
    assert.ok([302, 403].includes(blockedStaffSnapshot.status()));

    const staffWatch = await manager.context().newPage();
    await staffWatch.goto(`${baseUrl}/`);
    const alertsBefore = Number(await staffWatch.locator("[data-live-overdue-invoices]").innerText());
    const secondTab = await student.context().newPage();
    await secondTab.goto(`${baseUrl}/Student/Dashboard`);
    await student.goto(`${baseUrl}/Student/Notifications`);
    await student.evaluate(() => { window.realtimeProbe = "same-document"; });
    const before = Number(await student.locator(".student-notification span").innerText());
    await recordCollection(manager);
    await student.waitForFunction(count =>
      document.querySelector(".student-notification span")?.textContent.trim() === String(count),
      before + 1, { timeout: 6000 });
    assert.match(await student.locator(".notification-page-list a").first().innerText(), /Đã ghi nhận khoản thu/);
    assert.equal(await student.evaluate(() => window.realtimeProbe), "same-document");
    await secondTab.locator(".student-notification-list").getByText("Đã ghi nhận khoản thu", { exact: true }).waitFor();
    await staffWatch.waitForFunction(count =>
      document.querySelector("[data-live-overdue-invoices]")?.textContent.trim() === String(count),
      alertsBefore - 1, { timeout: 6000 });

    await secondTab.goto(`${baseUrl}/Student/Notifications`);
    await secondTab.getByRole("button", { name: "Đánh dấu tất cả đã đọc" }).click();
    assert.equal(await secondTab.locator(".student-notification span").textContent(), "0");
    await student.waitForFunction(() =>
      document.querySelector(".student-notification span")?.textContent === "0" &&
      !document.querySelector(".notification-page-list a.unread"), null, { timeout: 6000 });
    assert.equal(await student.locator(".notification-page-list a.unread").count(), 0);

    await student.context().setOffline(true);
    try {
      await reverseCollection(manager);
      assert.equal(await student.locator(".student-notification span").textContent(), "0");
    } finally {
      await student.context().setOffline(false);
    }
    await student.waitForFunction(() =>
      document.querySelector(".student-notification span")?.textContent === "1" &&
      document.querySelector(".notification-page-list a strong")?.textContent.includes("điều chỉnh"), null, { timeout: 9000 });
    assert.equal(await student.evaluate(() => window.realtimeProbe), "same-document");
    assert.equal(await other.locator(".student-notification span").textContent(), "0");
    assert.equal(await other.locator(".notification-page-list a").count(), 0);
    assert.deepEqual(otherSignals, [], "Another student must not receive the recipient or staff signal");
    console.log("SignalR browser smoke passed: staff counters, dashboard and student inbox, read state across tabs, offline catch-up, and private role/account scope.");
  } finally {
    await browser.close();
  }
}

run().catch(error => { console.error(error.message); process.exitCode = 1; });
