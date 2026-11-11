const { chromium } = require("playwright");
const { spawnSync } = require("child_process");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const marker = "[E2E] Thu phi va hoan tac";
const sqlcmd = "C:\\Program Files\\Microsoft SQL Server\\Client SDK\\ODBC\\170\\Tools\\Binn\\SQLCMD.EXE";
const server = "DESKTOP-OSSBOT1\\TUANANH";

function runSql(sql) {
  const settings = "SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;";
  const result = spawnSync(sqlcmd, ["-S", server, "-d", "DormitoryManagementSystem", "-E", "-b", "-Q", `${settings} ${sql}`], { encoding: "utf8" });
  if (result.status !== 0) throw new Error(`SQL failed: ${result.stderr || result.stdout}`);
  return result.stdout;
}

function cleanup() {
  runSql(`
SET NOCOUNT ON;
DECLARE @InvoiceId bigint = (SELECT Id FROM Invoices WHERE Code = N'HD-MGR-0001');
DELETE FROM Notifications WHERE Body LIKE N'%THU-%' AND Body LIKE N'%HD-MGR-0001%';
DELETE FROM PaymentTransactions WHERE InvoiceId = @InvoiceId AND Amount < 0 AND CHARINDEX(N'${marker}', Note) > 0;
DELETE FROM PaymentTransactions WHERE InvoiceId = @InvoiceId AND CHARINDEX(N'${marker}', Note) > 0;
UPDATE Invoices SET LedgerUpdatedAt = NULL WHERE Id = @InvoiceId;`);
}

async function signIn(page) {
  await page.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  await page.getByLabel("Email").fill("manager@nepktx.local");
  await page.getByLabel("Mật khẩu").fill(e2ePassword);
  await Promise.all([
    page.waitForLoadState("networkidle"),
    page.getByRole("button", { name: "Đăng nhập" }).click()
  ]);
}

(async () => {
  cleanup();
  const browser = await chromium.launch({ executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", headless: true });
  const issues = [];
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 950 } });
    const page = await context.newPage();
    await signIn(page);
    await page.goto(`${baseUrl}/Billing/Index?Search=HD-MGR-0001`, { waitUntil: "networkidle" });
    const before = await page.locator(".billing-ledger-summary .balance strong").innerText();
    if (!before.includes("1.471.000")) issues.push(`unexpected opening balance: ${before}`);

    await page.locator('select[name="Method"]').selectOption("Cash");
    await page.locator('input[name="Amount"]').fill("123000");
    await page.locator('textarea[name="Note"]').fill(marker);
    const paymentPayload = await page.locator(".collection-form").evaluate(form => Object.fromEntries(new FormData(form)));
    await Promise.all([
      page.waitForNavigation({ waitUntil: "networkidle" }),
      page.getByRole("button", { name: "Ghi nhận thu" }).click()
    ]);
    const afterPayment = await page.locator(".billing-ledger-summary .balance strong").innerText();
    if (!afterPayment.includes("1.348.000")) issues.push(`payment did not change balance: ${afterPayment}`);
    const historyText = await page.locator(".billing-history").innerText();
    const receiptCode = historyText.match(/THU-\d{8}-\d{6}/)?.[0];
    if (!receiptCode || !historyText.includes(marker)) issues.push("payment audit entry is incomplete");

    await page.request.post(`${baseUrl}/Billing/RecordPayment`, { form: paymentPayload });
    await page.reload({ waitUntil: "networkidle" });
    const duplicateCount = runSql(`SET NOCOUNT ON; SELECT COUNT(*) FROM PaymentTransactions WHERE IdempotencyKey=N'${paymentPayload.IdempotencyKey}';`).trim();
    if (!duplicateCount.endsWith("1")) issues.push(`idempotent replay created duplicate rows: ${duplicateCount}`);
    const afterReplay = await page.locator(".billing-ledger-summary .balance strong").innerText();
    if (!afterReplay.includes("1.348.000")) issues.push(`idempotent replay changed balance: ${afterReplay}`);

    const overpaymentPayload = await page.locator(".collection-form").evaluate(form => Object.fromEntries(new FormData(form)));
    overpaymentPayload.Amount = "999999999";
    overpaymentPayload.IdempotencyKey = `e2e-overpay-${Date.now()}`;
    await page.request.post(`${baseUrl}/Billing/RecordPayment`, { form: overpaymentPayload });
    const overpaymentCount = runSql(`SET NOCOUNT ON; SELECT COUNT(*) FROM PaymentTransactions WHERE IdempotencyKey=N'${overpaymentPayload.IdempotencyKey}';`).trim();
    if (!overpaymentCount.endsWith("0")) issues.push(`overpayment was persisted: ${overpaymentCount}`);
    await page.reload({ waitUntil: "networkidle" });
    const afterOverpayment = await page.locator(".billing-ledger-summary .balance strong").innerText();
    if (!afterOverpayment.includes("1.348.000")) issues.push(`rejected overpayment changed balance: ${afterOverpayment}`);

    if (!receiptCode) throw new Error("Receipt code was not rendered after payment.");
    await page.locator(".billing-history-list article").filter({ hasText: receiptCode }).getByRole("button", { name: "Hoàn tác" }).click();
    const dialog = page.locator("dialog[open]");
    await dialog.locator('textarea[name="Reason"]').fill(`${marker} - nhập sai số tiền`);
    await Promise.all([
      page.waitForNavigation({ waitUntil: "networkidle" }),
      dialog.getByRole("button", { name: "Xác nhận hoàn tác" }).click()
    ]);
    const afterReversal = await page.locator(".billing-ledger-summary .balance strong").innerText();
    if (!afterReversal.includes("1.471.000")) issues.push(`reversal did not restore balance: ${afterReversal}`);
    const finalHistory = await page.locator(".billing-history").innerText();
    if (!finalHistory.includes("Đã hoàn tác") || !finalHistory.includes("Điều chỉnh")) issues.push("reversal audit relationship is missing");

    const effective = runSql(`SET NOCOUNT ON; SELECT COUNT(*) FROM PaymentTransactions WHERE InvoiceId=(SELECT Id FROM Invoices WHERE Code=N'HD-MGR-0001') AND CHARINDEX(N'${marker}', Note) > 0;`).trim();
    if (!effective.endsWith("2")) issues.push(`expected one receipt and one reversal, SQL returned: ${effective}`);

    await context.close();
    process.stdout.write(JSON.stringify({ receiptCode, issues }, null, 2));
    if (issues.length) process.exitCode = 1;
  } finally {
    await browser.close();
    cleanup();
  }
})().catch(error => {
  process.stderr.write(error.stack);
  process.exitCode = 1;
});
