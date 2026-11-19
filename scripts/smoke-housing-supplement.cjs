const assert = require("node:assert/strict");
const { chromium } = require("playwright");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

assert.equal(new URL(baseUrl).hostname, "127.0.0.1");
assert.match(process.env.DORM_BROWSER_FIXTURE_DB ?? "", /^DormitoryManagementSystem_Test_[a-f0-9]{32}$/);

async function signIn(browser, role) {
  const context = await browser.newContext();
  const page = await context.newPage();
  await page.goto(baseUrl + "/Account/Login");
  await page.getByLabel("Email", { exact: true }).fill(role + "@nepktx.local");
  await page.getByLabel("Mật khẩu", { exact: true }).fill(e2ePassword);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.waitForURL(url => url.pathname !== "/Account/Login");
  return page;
}

async function openQueue(page, path) {
  const connected = page.waitForResponse(response =>
    new URL(response.url()).pathname === "/Home/AttentionSnapshot" && response.ok());
  await page.goto(baseUrl + path);
  await connected;
}

async function badgeCount(page, key) {
  return Number(await page.locator(`[data-live-attention="${key}"]`).textContent());
}

async function waitForBadge(page, key, expected) {
  try {
    await page.waitForFunction(({ key, expected }) => {
      const badge = document.querySelector(`[data-live-attention="${key}"]`);
      return badge?.textContent.trim() === String(expected) && (expected === 0 || badge.getClientRects().length > 0);
    }, { key, expected }, { timeout: 5000 });
  } catch {
    assert.equal(await badgeCount(page, key), expected, `Live ${key} count in ${new URL(page.url()).pathname}`);
    assert.ok(expected === 0 || await page.locator(`[data-live-attention="${key}"]`).isVisible(), `Live ${key} count is visible`);
  }
}

async function requestInformation(admin, reason) {
  await admin.getByRole("button", { name: "Yêu cầu bổ sung", exact: true }).click();
  await admin.locator("#information-dialog textarea").fill(reason);
  await admin.locator("#information-dialog button[type=submit]").click();
  await admin.locator(".case-header .request-state").getByText("Cần bổ sung", { exact: true }).waitFor();
}

async function sendSupplement(student, reply) {
  await student.goto(baseUrl + "/Student/Applications");
  const form = student.locator(".student-supplement-form");
  await form.locator("textarea").fill(reply);
  await form.getByRole("button", { name: "Gửi bổ sung và chuyển lại BQL" }).click();
  await student.getByText(/Đã gửi bổ sung cho đơn/).waitFor();
}

async function run() {
  const browser = await chromium.launch({ channel: "msedge" });
  try {
    const [student, admin, manager] = await Promise.all(
      ["student", "admin", "manager"].map(role => signIn(browser, role))
    );
    const adminInbox = await admin.context().newPage();
    await openQueue(adminInbox, "/Notifications");
    const managerInbox = await manager.context().newPage();
    await openQueue(managerInbox, "/Notifications");
    await openQueue(manager, "/Applications");
    const managerWithoutHub = await manager.context().newPage();
    await managerWithoutHub.setViewportSize({ width: 390, height: 844 });
    await managerWithoutHub.route(url => url.pathname.startsWith("/hubs/operations/"), route => route.abort());
    await openQueue(managerWithoutHub, "/Rooms/Map");
    await managerWithoutHub.bringToFront();
    const fallbackUnread = await badgeCount(managerWithoutHub, "unread");
    const inboxes = [adminInbox, managerInbox];
    const initialCounts = await Promise.all(inboxes.map(async page => ({
      applications: await badgeCount(page, "applications"),
      maintenance: await badgeCount(page, "maintenance"),
      unread: await badgeCount(page, "unread")
    })));
    await student.goto(baseUrl + "/Student/Rooms?Building=C");
    await student.locator("[data-apply-room]").first().click();
    await student.locator("[data-tour-bed-select]").selectOption({ index: 1 });
    await student.locator("[data-room-booking] textarea").fill("Đơn chuyển phòng để kiểm tra tiếp nhận thông tin bổ sung.");
    await student.locator("[data-room-booking] button[type=submit]").click();
    await student.waitForURL("**/Student/Applications");
    const code = (await student.locator(".application-card.current").innerText()).match(/DK-\d{4}-\d{4,}/)?.[0];
    assert.ok(code);
    await manager.locator(".request-queue-item").first().getByText("SV002106", { exact: false }).waitFor();
    await Promise.all(inboxes.flatMap((page, index) => [
      waitForBadge(page, "applications", initialCounts[index].applications + 1),
      waitForBadge(page, "unread", initialCounts[index].unread + 1)
    ]));
    await waitForBadge(managerWithoutHub, "unread", fallbackUnread + 1);
    await student.goto(baseUrl + "/Student/Repairs");
    await manager.goto(baseUrl + "/Maintenance");
    const selectedTitle = await manager.locator(".maintenance-detail-head h2").innerText();
    const draft = manager.locator(".maintenance-action textarea");
    if (await draft.count()) await draft.fill("Nội dung đang nhập cần giữ nguyên.");
    const repairTitle = "Quạt giường số 3 cần kiểm tra";
    const repair = await student.locator('form[action*="CreateRepair"]').evaluate(form => ({
      url: form.action, csrf: form.querySelector('input[name="__RequestVerificationToken"]').value
    }));
    const repaired = await student.request.post(repair.url, { form: {
      __RequestVerificationToken: repair.csrf, Category: "Thiết bị", Priority: "Low",
      Title: repairTitle, Description: "Quạt chạy lúc được lúc không và cần kiểm tra."
    }, maxRedirects: 0 });
    assert.equal(repaired.status(), 302);
    await manager.locator(".maintenance-ticket").first().getByText(repairTitle, { exact: true }).waitFor();
    assert.equal(await manager.locator(".maintenance-detail-head h2").innerText(), selectedTitle);
    if (await draft.count()) assert.equal(await draft.inputValue(), "Nội dung đang nhập cần giữ nguyên.");

    const adminEntry = adminInbox.locator(".notification-entry.unread").filter({ hasText: repairTitle });
    const managerEntry = managerInbox.locator(".notification-entry.unread").filter({ hasText: repairTitle });
    await adminEntry.waitFor();
    await managerEntry.waitFor();
    await Promise.all(inboxes.flatMap((page, index) => [
      waitForBadge(page, "maintenance", initialCounts[index].maintenance + 1),
      waitForBadge(page, "unread", initialCounts[index].unread + 2)
    ]));
    const otherId = await adminEntry.locator("input[name=id]").inputValue();
    const openForm = await managerEntry.locator("form").evaluate(form => ({
      url: form.action, csrf: form.querySelector('input[name="__RequestVerificationToken"]').value
    }));
    const forbidden = await managerInbox.request.post(openForm.url, {
      form: { __RequestVerificationToken: openForm.csrf, id: otherId }, maxRedirects: 0
    });
    assert.equal(forbidden.status(), 404);
    await adminEntry.getByRole("button", { name: "Xem hồ sơ" }).click();
    await adminInbox.locator(".maintenance-detail-head h2").getByText(repairTitle, { exact: true }).waitFor();
    await adminInbox.locator(".notification-button").click();
    await adminInbox.locator(".notification-entry:not(.unread)").filter({ hasText: repairTitle }).waitFor();
    await managerEntry.waitFor();
    await managerInbox.getByRole("button", { name: "Đánh dấu tất cả đã đọc" }).click();
    await managerInbox.locator(".notification-entry:not(.unread)").filter({ hasText: repairTitle }).waitFor();
    assert.equal(await managerInbox.locator(".notification-entry.unread").count(), 0);

    await admin.goto(baseUrl + "/Applications?Search=" + code);
    await admin.getByRole("button", { name: "Nhận xử lý đơn" }).click();
    await requestInformation(admin, "Vui lòng gửi thời gian dự kiến bàn giao phòng cũ.");
    await Promise.all(inboxes.map((page, index) => waitForBadge(page, "applications", initialCounts[index].applications)));
    await Promise.all([
      openQueue(admin, "/Applications?Scope=info&Search=" + code),
      openQueue(manager, "/Applications")
    ]);
    const reply = "Em sẽ bàn giao phòng cũ lúc 9 giờ sáng thứ Hai.";
    await sendSupplement(student, reply);
    await Promise.all(inboxes.map((page, index) => waitForBadge(page, "applications", initialCounts[index].applications + 1)));
    await adminInbox.locator(".notification-entry.unread").filter({ hasText: "Sinh viên đã bổ sung đơn lưu trú" }).waitFor();
    const fresh = await admin.context().newPage();
    await fresh.goto(baseUrl + "/Applications?Search=" + code);
    const results = await Promise.allSettled([
      fresh.locator(".student-supplement").getByText(reply, { exact: true }).waitFor({ timeout: 1600 }),
      fresh.locator(".request-queue-item .request-state").getByText("Đã bổ sung", { exact: true }).waitFor({ timeout: 1600 }),
      admin.getByRole("link", { name: "Xem đơn " + code, exact: true }).waitFor({ timeout: 5000 }),
      manager.getByRole("link", { name: "Xem đơn " + code, exact: true }).waitFor({ timeout: 5000 })
    ]);
    const checks = ["visible reply", "queue status", "live admin link on waiting filter", "live manager link"];
    const failures = results.flatMap((result, index) => result.status === "rejected" ? [checks[index]] : []);
    assert.deepEqual(failures, [], failures.join(", "));

    await admin.getByRole("link", { name: "Xem đơn " + code, exact: true }).click();
    await admin.locator(".student-supplement").getByText(reply, { exact: true }).waitFor();
    await admin.getByRole("button", { name: "Nhận xử lý đơn" }).click();
    await admin.locator(".student-supplement").getByText(reply, { exact: true }).waitFor();
    await requestInformation(admin, "Xin gửi thêm giờ dự kiến nhận phòng mới.");
    const longReply = "Em dự kiến nhận phòng lúc 10 giờ sáng thứ Ba. ".repeat(20).slice(0, 599).trimEnd().padEnd(599, "a") + ".";
    await sendSupplement(student, longReply);
    await admin.goto(baseUrl + "/Applications?Search=" + code);
    assert.equal(await admin.locator(".student-supplement blockquote").innerText(), longReply);
    assert.ok((await admin.locator(".case-audit").textContent()).includes(reply));
    console.log("Operations smoke passed: sidebar and inbox counts update live for both roles, including low-priority repairs and supplement handoffs. Case links, draft text and reply history stay intact.");
  } finally {
    await browser.close();
  }
}

run().catch(error => { console.error(error.message); process.exitCode = 1; });
