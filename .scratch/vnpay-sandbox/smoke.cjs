const assert = require("node:assert/strict");
const { createHmac } = require("node:crypto");
const { mkdirSync } = require("node:fs");
const path = require("node:path");
const { chromium } = require("playwright");

const base = new URL(process.env.DORM_E2E_BASE_URL);
const fixtureDb = process.env.DORM_VNPAY_FIXTURE_DB;
const secret = process.env.Vnpay__HashSecret;
const merchant = process.env.Vnpay__MerchantCode;
const password = process.env.DORM_E2E_PASSWORD;
assert.equal(base.hostname, "127.0.0.1");
assert.match(fixtureDb, /^DormitoryManagementSystem_Test_[a-f0-9]{32}$/);
assert.ok(secret?.startsWith("fixture-only-") && merchant === "TESTKTX1" && password);

const invoiceCode = "HD-2026-09-02106";
const snapshots = path.resolve(__dirname, "../../artifacts/screenshots");
mkdirSync(snapshots, { recursive: true });

async function signIn(page, user) {
  await page.goto(new URL("/Account/Login", base).href);
  await page.getByLabel("Email", { exact: true }).fill(user);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(password);
  await Promise.all([
    page.waitForURL(url => url.pathname !== "/Account/Login"),
    page.getByRole("button", { name: "Đăng nhập", exact: true }).click()
  ]);
}

function sign(fields) {
  const payload = new URLSearchParams(
    Object.keys(fields).sort().map(key => [key, fields[key]])
  ).toString();
  return `${payload}&vnp_SecureHash=${createHmac("sha512", secret).update(payload).digest("hex")}`;
}

async function run() {
  const browser = await chromium.launch({ channel: "msedge", headless: true });
  try {
    const desktop = await browser.newContext({ viewport: { width: 1440, height: 990 } });
    const student = await desktop.newPage();
    await signIn(student, "student@nepktx.local");
    assert.equal((await desktop.request.post(new URL("/Student/Payments/Vnpay/Start", base).href, {
      form: { invoiceId: 1 }, maxRedirects: 0
    })).status(), 400, "Checkout POST requires an antiforgery token");

    await student.goto(new URL("/Student/Invoices", base).href);
    const card = student.locator(".student-invoice-card").filter({ hasText: invoiceCode });
    const checkout = card.getByRole("button", { name: "Thanh toán qua VNPAY" });
    assert.ok(await checkout.isVisible());
    const invoiceId = await card.locator('input[name="invoiceId"]').getAttribute("value");
    assert.match(invoiceId, /^\d+$/);
    await student.screenshot({ path: path.join(snapshots, "vnpay-sandbox-invoice-desktop.png"), fullPage: true });

    // Intercept the external navigation: this test never contacts VNPAY.
    await desktop.route("https://sandbox.vnpayment.vn/paymentv2/**", route =>
      route.fulfill({ contentType: "text/plain", body: "Sandbox fixture" }));
    const redirectResponse = student.waitForResponse(response =>
      response.request().method() === "POST" && new URL(response.url()).pathname === "/Student/Payments/Vnpay/Start");
    const outgoing = student.waitForURL("https://sandbox.vnpayment.vn/paymentv2/**");
    await checkout.click();
    await outgoing;
    const response = await redirectResponse;
    assert.equal(response.status(), 302);
    const location = new URL((await response.allHeaders()).location);
    assert.equal(location.origin, "https://sandbox.vnpayment.vn");
    const payment = location.searchParams;
    const signature = payment.get("vnp_SecureHash");
    const outgoingFields = Object.fromEntries(payment.entries());
    delete outgoingFields.vnp_SecureHash;
    assert.equal(new URLSearchParams(sign(outgoingFields)).get("vnp_SecureHash"), signature);
    assert.equal(payment.get("vnp_Amount"), "152500000");
    const reference = payment.get("vnp_TxnRef");
    assert.match(reference, /^[a-f0-9]{32}$/);

    const fields = {
      vnp_TmnCode: merchant,
      vnp_TxnRef: reference,
      vnp_Amount: payment.get("vnp_Amount"),
      vnp_OrderInfo: payment.get("vnp_OrderInfo"),
      vnp_BankCode: "NCB",
      vnp_TransactionNo: "2026111701",
      vnp_ResponseCode: "00",
      vnp_TransactionStatus: "00"
    };
    const reply = sign(fields);
    await student.goto(new URL(`/payments/vnpay/return?${reply}`, base).href);
    assert.equal(new URL(student.url()).search, "", "Return parameters do not remain in the invoice URL");
    assert.ok(await student.getByText("Chờ xác nhận", { exact: true }).isVisible());

    const mobile = await browser.newContext({ viewport: { width: 390, height: 844 }, isMobile: true, deviceScaleFactor: 1 });
    const mobilePage = await mobile.newPage();
    await signIn(mobilePage, "student@nepktx.local");
    await mobilePage.goto(new URL("/Student/Invoices", base).href);
    const button = mobilePage.getByRole("button", { name: "Thanh toán qua VNPAY" }).first();
    assert.ok(await button.isVisible());
    assert.ok((await button.boundingBox()).height >= 44);
    assert.equal(await mobilePage.evaluate(() => document.documentElement.scrollWidth > innerWidth + 1), false);
    await mobilePage.screenshot({ path: path.join(snapshots, "vnpay-sandbox-invoice-mobile.png"), fullPage: true });

    const callbacks = await browser.newContext();
    const corrupted = new URLSearchParams(reply);
    corrupted.set("vnp_Amount", "1");
    const forged = await callbacks.request.get(new URL(`/payments/vnpay/ipn?${corrupted.toString()}`, base).href);
    assert.equal((await forged.json()).RspCode, "97");
    const ipn = new URL(`/payments/vnpay/ipn?${reply}`, base).href;
    assert.equal((await (await callbacks.request.get(ipn)).json()).RspCode, "00");
    assert.equal((await (await callbacks.request.get(ipn)).json()).RspCode, "02");

    await student.getByRole("link", { name: "Kiểm tra lại" }).click();
    assert.ok(await student.getByText("Đã ghi nhận", { exact: true }).isVisible());
    await student.screenshot({ path: path.join(snapshots, "vnpay-sandbox-status-desktop.png"), fullPage: true });
    await student.getByRole("link", { name: "Về hóa đơn" }).click();
    assert.equal(await checkout.count(), 0);
    assert.ok(await card.getByText("Đã thu đủ", { exact: true }).isVisible());

    const manager = await browser.newPage();
    await signIn(manager, "manager@nepktx.local");
    await manager.goto(new URL(`/Billing?SelectedId=${invoiceId}`, base).href);
    const collected = manager.locator(".billing-history-list article").filter({ hasText: "VNPAY Sandbox" });
    assert.equal(await collected.count(), 1);
    assert.equal(await collected.getByRole("button", { name: "Hoàn tác" }).count(), 0);
    console.log("VNPAY browser smoke: signed sandbox redirect, read-only return, CSRF, forged and replayed IPN, student and manager receipts, desktop and mobile passed.");
  } finally {
    await browser.close();
  }
}

run().catch(error => { console.error(error.message); process.exitCode = 1; });
