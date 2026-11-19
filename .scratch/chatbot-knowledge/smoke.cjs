const assert = require("node:assert/strict");
const path = require("node:path");
const { chromium } = require("playwright");
const { baseUrl, e2ePassword } = require("../../scripts/e2e-config.cjs");

assert.equal(new URL(baseUrl).hostname, "127.0.0.1");
assert.match(process.env.DORM_BROWSER_FIXTURE_DB ?? "", /^DormitoryManagementSystem_Test_[a-f0-9]{32}$/);

async function signIn(browser, email, options) {
  const context = await browser.newContext({ locale: "vi-VN", ...options });
  const page = await context.newPage();
  page.setDefaultTimeout(8000);
  await page.goto(`${baseUrl}/Account/Login`);
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(e2ePassword);
  await Promise.all([
    page.waitForURL(url => url.pathname !== "/Account/Login"),
    page.getByRole("button", { name: "Đăng nhập", exact: true }).click()
  ]);
  return page;
}

async function ask(page, question, entry = "button") {
  const dialog = page.getByRole("dialog", { name: "Trợ lý AI NẾP KTX" });
  if (!(await dialog.isVisible())) await page.getByRole("button", { name: "Mở trợ lý AI", exact: true }).click();
  const pending = page.waitForResponse(response => new URL(response.url()).pathname === "/Chatbot/Ask");
  if (entry === "suggested") {
    await dialog.getByRole("button", { name: question, exact: true }).click();
  } else {
    const input = dialog.getByRole("textbox");
    await input.fill(question);
    if (entry === "keyboard") await input.press("Enter");
    else await dialog.getByRole("button", { name: "Gửi câu hỏi" }).click();
  }
  const response = await pending;
  assert.equal(response.status(), 200);
  const answer = await response.json();
  assert.equal(answer.usedGemini, false);
  await dialog.getByText(answer.text, { exact: true }).last().waitFor();
  return { answer, dialog };
}

async function assertComposerLayout(page) {
  const positions = await page.locator("[data-chat-form]").evaluate(form => {
    const field = form.querySelector(".ai-composer-field");
    const input = field.querySelector("[data-chat-input]");
    const measure = (input, button) => {
      const bounds = input.getBoundingClientRect();
      const send = button.getBoundingClientRect();
      return send.left >= bounds.right && Math.abs((send.top + send.height / 2) - (bounds.top + bounds.height / 2)) <= 2;
    };
    const current = measure(input, field.querySelector("button"));
    // A running server can still render the earlier composer while static CSS updates.
    const legacyInput = document.createElement("input");
    legacyInput.setAttribute("data-chat-input", "");
    const legacyButton = document.createElement("button");
    legacyButton.type = "submit";
    field.replaceWith(legacyInput, legacyButton);
    const privacy = form.querySelector("small");
    privacy.hidden = true;
    const legacy = measure(legacyInput, legacyButton);
    legacyInput.replaceWith(field);
    legacyButton.remove();
    privacy.hidden = false;
    return { current, legacy };
  });
  assert.deepEqual(positions, { current: true, legacy: true });
}

async function run() {
  const browser = await chromium.launch({ channel: "msedge" });
  try {
    const guest = await browser.newContext();
    const deniedGuest = await guest.request.post(`${baseUrl}/Chatbot/Ask`, { data: { message: "Nội quy" }, maxRedirects: 0 });
    assert.ok([302, 401].includes(deniedGuest.status()));

    const manager = await signIn(browser, "manager@nepktx.local");
    const deniedManager = await manager.request.post(`${baseUrl}/Chatbot/Ask`, { data: { message: "Nội quy" }, maxRedirects: 0 });
    assert.ok([302, 403].includes(deniedManager.status()));

    const student = await signIn(browser, "student@nepktx.local", { viewport: { width: 1366, height: 900 } });
    const deniedNoToken = await student.request.post(`${baseUrl}/Chatbot/Ask`, { data: { message: "Nội quy" }, maxRedirects: 0 });
    assert.equal(deniedNoToken.status(), 400);
    const out = process.env.DORM_BROWSER_FIXTURE_ARTIFACTS;
    const launcher = student.getByRole("button", { name: "Mở trợ lý AI", exact: true });
    await launcher.click();
    const welcome = student.locator("[data-chat-welcome]");
    await welcome.waitFor();
    await assertComposerLayout(student);
    assert.equal(await welcome.getByRole("button").count(), 4);
    await student.locator("[data-ai-assistant]").screenshot({ path: path.join(out, "chatbot-welcome-desktop.png"), animations: "disabled" });
    const draft = student.getByRole("textbox", { name: "Câu hỏi cho trợ lý AI" });
    const singleLine = (await draft.boundingBox()).height;
    await draft.fill("Dòng đầu");
    await draft.press("Shift+Enter");
    await draft.pressSequentially("Dòng sau");
    assert.match(await draft.inputValue(), /\n/);
    assert.ok((await draft.boundingBox()).height > singleLine);
    await draft.clear();
    await student.keyboard.press("Escape");
    await welcome.waitFor({ state: "hidden" });
    assert.equal(await launcher.evaluate(button => document.activeElement === button), true);
    const { answer: demo, dialog } = await ask(student, "Đăng ký phòng thế nào?", "suggested");
    await welcome.waitFor({ state: "hidden" });
    assert.equal(demo.source.isDemo, true);
    await dialog.getByText("Tài liệu minh họa · Sổ tay minh họa NẾP KTX · Đăng ký phòng").waitFor();
    assert.equal(await dialog.getByRole("link", { name: "Mở Tìm phòng" }).getAttribute("href"), "/Student/Rooms");
    await dialog.screenshot({ path: path.join(out, "chatbot-source-desktop.png"), animations: "disabled" });

    const { answer: missing } = await ask(student, "Phí phòng A-302 là bao nhiêu?");
    assert.equal(missing.source, null);
    assert.match(missing.text, /Ban quản lý/);
    const { answer: debt } = await ask(student, "Công nợ của em còn bao nhiêu?");
    assert.equal(debt.action, "invoices");
    assert.equal(debt.source, null);
    await Promise.all([
      student.waitForURL(`${baseUrl}/Student/Invoices`),
      dialog.getByRole("link", { name: "Xem hóa đơn" }).click()
    ]);

    const { answer: contact, dialog: contactDialog } = await ask(student, "Liên hệ với bên hỗ trợ");
    assert.equal(contact.action, "contact");
    assert.match(contact.text, /KTX Fixture/);
    assert.match(contact.text, /support@example\.test/);
    const { answer: followUp } = await ask(student, "Ở đâu vậy?", "keyboard");
    assert.equal(followUp.action, "contact");
    assert.match(followUp.text, /KTX Fixture/);
    const contactLink = contactDialog.getByRole("link", { name: "Xem thông tin liên hệ" }).last();
    assert.equal(await contactLink.getAttribute("href"), "/Home/Privacy#contact");
    await contactDialog.screenshot({ path: path.join(out, "chatbot-contact-desktop.png"), animations: "disabled" });
    await Promise.all([
      student.waitForURL(`${baseUrl}/Home/Privacy#contact`),
      contactLink.click()
    ]);
    const contactSection = student.locator("#contact");
    assert.equal(await contactSection.getByRole("link", { name: "support@example.test" }).getAttribute("href"), "mailto:support@example.test");
    assert.equal(await contactSection.getByRole("link", { name: "+1 202 555 0142" }).getAttribute("href"), "tel:+12025550142");

    const mobile = await signIn(browser, "student@nepktx.local", { viewport: { width: 390, height: 844 }, isMobile: true, hasTouch: true });
    await mobile.locator(".student-topbar").getByRole("button", { name: "Mở trợ lý AI", exact: true }).click();
    assert.equal(await mobile.locator("[data-chat-input]").evaluate(input => document.activeElement === input), false);
    await assertComposerLayout(mobile);
    const { answer: unclear } = await ask(mobile, "Abcxyz");
    assert.match(unclear.text, /chưa hiểu/);
    const { answer: available, dialog: mobileChat } = await ask(mobile, "Phòng nào còn trống");
    assert.equal(available.action, "rooms");
    assert.equal(available.source, null);
    const rooms = [...available.text.matchAll(/^• (\S+) — còn (\d+) chỗ$/gm)];
    assert.ok(rooms.length >= 1 && rooms.length <= 3);
    const mobileBounds = await mobileChat.boundingBox();
    assert.ok(mobileBounds && mobileBounds.x >= 0 && mobileBounds.x + mobileBounds.width <= 391);
    const roomLink = mobileChat.getByRole("link", { name: "Mở Tìm phòng" });
    assert.equal(await roomLink.getAttribute("href"), "/Student/Rooms");
    await mobile.screenshot({ path: path.join(out, "chatbot-mobile.png"), animations: "disabled" });
    await Promise.all([mobile.waitForURL(`${baseUrl}/Student/Rooms`), roomLink.click()]);
    for (const [, code, beds] of rooms) {
      const card = mobile.locator(".student-room-card").filter({ has: mobile.getByRole("heading", { name: `Phòng ${code}`, exact: true }) });
      await card.getByText(`${beds} chỗ có thể xếp`, { exact: true }).waitFor();
    }
    console.log(JSON.stringify({ result: "PASS", screenshots: out }));
  } finally {
    await browser.close();
  }
}

run().catch(error => { console.error(error); process.exitCode = 1; });
