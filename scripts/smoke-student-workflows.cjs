const { execFileSync } = require("child_process");
const { chromium } = require("playwright");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const transferReason = "Kiểm thử tự động luồng chuyển phòng sinh viên.";
const repairTitle = "Kiểm thử tự động kết nối Wi-Fi";

function cleanup() {
  const query = [
    "SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; SET ANSI_NULLS ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET ARITHABORT ON; SET CONCAT_NULL_YIELDS_NULL ON; SET NUMERIC_ROUNDABORT OFF;",
    `DELETE FROM MaintenanceUpdates WHERE MaintenanceRequestId IN (SELECT Id FROM MaintenanceRequests WHERE Title=N'${repairTitle}');`,
    `DELETE FROM MaintenanceRequests WHERE Title=N'${repairTitle}';`,
    `DELETE FROM HousingRequests WHERE StudentReason=N'${transferReason}';`
  ].join(" ");
  execFileSync("sqlcmd", ["-b", "-S", "DESKTOP-OSSBOT1\\TUANANH", "-d", "DormitoryManagementSystem", "-E", "-C", "-Q", query]);
}

async function signIn(page) {
  await page.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  await page.getByLabel("Email").fill("student@nepktx.local");
  await page.getByLabel("Mật khẩu").fill(e2ePassword);
  await Promise.all([
    page.waitForLoadState("networkidle"),
    page.getByRole("button", { name: "Đăng nhập" }).click()
  ]);
}

(async () => {
  cleanup();
  const browser = await chromium.launch({
    executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",
    headless: true
  });

  try {
    const page = await browser.newPage({ viewport: { width: 1280, height: 900 } });
    await signIn(page);

    await page.goto(`${baseUrl}/Student/Applications`, { waitUntil: "networkidle" });
    if (!(await page.locator("body").innerText()).includes("DK-2026-0042")) throw new Error("Application history did not load");

    await page.goto(`${baseUrl}/Student/Invoices`, { waitUntil: "networkidle" });
    if ((await page.locator(".student-invoice-card").count()) !== 2) throw new Error("Expected two seeded invoices");

    await page.goto(`${baseUrl}/Student/Rooms`, { waitUntil: "networkidle" });
    const firstRoom = page.locator("[data-apply-room]").first();
    const selectedCode = await firstRoom.getAttribute("data-room-code");
    await firstRoom.click();
    await page.locator("[data-application-dialog] textarea").fill(transferReason);
    await Promise.all([
      page.waitForURL(/\/Student\/Applications/),
      page.locator("[data-application-dialog] button[type=submit]").click()
    ]);
    const activeCard = page.locator(".application-card.current");
    if (!(await activeCard.innerText()).includes(selectedCode)) throw new Error("Submitted room did not appear in application");
    page.once("dialog", dialog => dialog.accept());
    await Promise.all([
      page.waitForLoadState("networkidle"),
      activeCard.getByRole("button", { name: "Hủy đơn" }).click()
    ]);
    if (!(await page.locator("body").innerText()).includes("Đã hủy đơn")) throw new Error("Housing request was not cancelled");

    await page.goto(`${baseUrl}/Student/Repairs`, { waitUntil: "networkidle" });
    await page.locator("[data-repair-open]").first().click();
    await page.locator("[data-repair-dialog] select[name=Category]").selectOption({ label: "Internet" });
    await page.locator("[data-repair-dialog] select[name=Priority]").selectOption("Low");
    await page.locator("[data-repair-dialog] input[name=Title]").fill(repairTitle);
    await page.locator("[data-repair-dialog] textarea[name=Description]").fill("Mạng Wi-Fi mất kết nối tại bàn học từ 20:00, các thiết bị đều không truy cập được.");
    await Promise.all([
      page.waitForLoadState("networkidle"),
      page.locator("[data-repair-dialog] button[type=submit]").click()
    ]);
    if (!(await page.locator(".repair-list").innerText()).includes(repairTitle)) throw new Error("New repair request did not appear");

    process.stdout.write(JSON.stringify({
      applicationSubmitAndCancel: "passed",
      invoiceRead: "passed",
      repairSubmit: "passed",
      selectedCode
    }, null, 2));
  } finally {
    await browser.close();
    cleanup();
  }
})().catch(error => {
  process.stderr.write(error.stack);
  process.exitCode = 1;
});
