const { chromium } = require("playwright");
const { spawnSync } = require("child_process");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const informationMarker = "[E2E] Hồ sơ cần bổ sung quy trình.";
const supplementMarker = "[E2E] Đã bổ sung ngày chuyển dự kiến và xác nhận bàn giao.";
const checkoutMarker = "[E2E] Kết thúc học kỳ và bàn giao phòng theo lịch.";
const sqlcmd = "C:\\Program Files\\Microsoft SQL Server\\Client SDK\\ODBC\\170\\Tools\\Binn\\SQLCMD.EXE";

function cleanup(marker) {
  const sql = `
SET NOCOUNT ON;
SET XACT_ABORT ON;
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET ANSI_PADDING ON;
SET ANSI_WARNINGS ON;
SET CONCAT_NULL_YIELDS_NULL ON;
SET ARITHABORT ON;
SET NUMERIC_ROUNDABORT OFF;
BEGIN TRANSACTION;
DECLARE @StudentId int = (SELECT Id FROM StudentProfiles WHERE StudentCode = N'SV002106');
DECLARE @RequestId bigint = (SELECT TOP (1) Id FROM HousingRequests WHERE StudentProfileId = @StudentId AND StudentReason = N'${marker}' ORDER BY Id DESC);
IF @RequestId IS NOT NULL
BEGIN
    DECLARE @RequestCode nvarchar(24) = (SELECT Code FROM HousingRequests WHERE Id = @RequestId);
    DECLARE @SourceStayId int = (SELECT SourceStayId FROM HousingRequests WHERE Id = @RequestId);
    DELETE FROM Notifications
      WHERE UserId = (SELECT UserId FROM StudentProfiles WHERE Id = @StudentId)
        AND Type = N'Application'
        AND (Title LIKE N'%' + @RequestCode + N'%' OR Body LIKE N'%' + @RequestCode + N'%');
    DELETE FROM HousingRequestEvents WHERE HousingRequestId = @RequestId;
    DELETE FROM Stays WHERE HousingRequestId = @RequestId;
    DELETE FROM HousingRequests WHERE Id = @RequestId;
    IF @SourceStayId IS NOT NULL UPDATE Stays SET EndedAt = NULL WHERE Id = @SourceStayId;
END;
COMMIT TRANSACTION;`;
  const result = spawnSync(sqlcmd, ["-S", "DESKTOP-OSSBOT1\\TUANANH", "-d", "DormitoryManagementSystem", "-E", "-b", "-Q", sql], { encoding: "utf8" });
  if (result.status !== 0) throw new Error(`Cleanup failed: ${result.stderr || result.stdout}`);
}

async function signIn(page, email) {
  await page.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Mật khẩu").fill(e2ePassword);
  await Promise.all([
    page.waitForLoadState("networkidle"),
    page.getByRole("button", { name: "Đăng nhập" }).click()
  ]);
}

async function submitTransfer(student) {
  await student.goto(`${baseUrl}/Student/Rooms?Building=C`, { waitUntil: "networkidle" });
  const targetCard = student.locator(".student-room-card").filter({ hasText: "C-503" }).first();
  await targetCard.getByRole("button", { name: "Chọn phòng" }).click();
  await student.locator("[data-application-dialog] textarea[name=Reason]").fill(informationMarker);
  await Promise.all([
    student.waitForURL(/\/Student\/Applications/),
    student.getByRole("button", { name: "Gửi đơn chuyển phòng" }).click()
  ]);
  const card = student.locator(".application-card.current").filter({ hasText: informationMarker }).first();
  const text = await card.innerText();
  const code = text.match(/DK-\d{4}-\d{4,}/)?.[0];
  if (!code) throw new Error("Không đọc được mã đơn chuyển phòng.");
  return code;
}

async function managerOpenRequest(manager, requestCode) {
  await manager.goto(`${baseUrl}/Applications/Index?Search=${encodeURIComponent(requestCode)}`, { waitUntil: "networkidle" });
  if (!(await manager.locator(".application-case").isVisible())) throw new Error(`Quản lý không thấy ${requestCode}.`);
}

(async () => {
  cleanup(informationMarker);
  cleanup(checkoutMarker);
  const browser = await chromium.launch({ executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", headless: true });
  const issues = [];
  try {
    const studentContext = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const managerContext = await browser.newContext({ viewport: { width: 1440, height: 950 } });
    const student = await studentContext.newPage();
    const manager = await managerContext.newPage();
    await signIn(student, "student@nepktx.local");
    await signIn(manager, "manager@nepktx.local");

    const informationCode = await submitTransfer(student);
    await managerOpenRequest(manager, informationCode);
    await Promise.all([
      manager.waitForLoadState("networkidle"),
      manager.getByRole("button", { name: "Nhận xử lý đơn" }).click()
    ]);
    await manager.getByRole("button", { name: "Yêu cầu bổ sung" }).click();
    await manager.locator("#information-dialog textarea").fill("Vui lòng bổ sung ngày chuyển dự kiến và xác nhận thời gian bàn giao phòng cũ.");
    await Promise.all([
      manager.waitForLoadState("networkidle"),
      manager.locator("#information-dialog button[type=submit]").click()
    ]);

    await student.goto(`${baseUrl}/Student/Applications`, { waitUntil: "networkidle" });
    let activeCard = student.locator(".application-card.current").filter({ hasText: informationCode }).first();
    let cardText = await activeCard.innerText();
    if (!cardText.includes("Cần bổ sung") || !cardText.includes("Sinh viên") || !cardText.includes("ngày chuyển dự kiến")) {
      issues.push("student did not receive the information handoff with manager guidance");
    }
    await activeCard.locator(".student-supplement-form textarea").fill(supplementMarker);
    await Promise.all([
      student.waitForLoadState("networkidle"),
      activeCard.getByRole("button", { name: "Gửi bổ sung và chuyển lại BQL" }).click()
    ]);
    activeCard = student.locator(".application-card.current").filter({ hasText: informationCode }).first();
    cardText = await activeCard.innerText();
    if (!cardText.includes("Đã gửi") || !cardText.includes("Ban quản lý")) {
      issues.push("student supplement did not return responsibility to management");
    }
    await activeCard.locator("details").evaluate(element => { element.open = true; });
    if (!(await activeCard.locator(".student-request-events").innerText()).includes(supplementMarker)) {
      issues.push("student supplement is missing from the application audit trail");
    }
    await managerOpenRequest(manager, informationCode);
    const managerText = await manager.locator(".application-case").innerText();
    if (!managerText.includes("Ban quản lý") || !managerText.includes(supplementMarker)) {
      issues.push("manager did not receive the supplemented application audit trail");
    }

    cleanup(informationMarker);
    await student.goto(`${baseUrl}/Student/Applications`, { waitUntil: "networkidle" });
    await student.getByRole("button", { name: "Trả phòng" }).click();
    await student.locator("#checkout-dialog textarea").fill(checkoutMarker);
    await Promise.all([
      student.waitForLoadState("networkidle"),
      student.getByRole("button", { name: "Gửi yêu cầu trả phòng" }).click()
    ]);
    const checkoutCard = student.locator(".application-card.current").filter({ hasText: checkoutMarker }).first();
    const checkoutText = await checkoutCard.innerText();
    const checkoutCode = checkoutText.match(/DK-\d{4}-\d{4,}/)?.[0];
    if (!checkoutCode || !checkoutText.includes("Trả phòng")) throw new Error("Không tạo được đơn trả phòng.");

    await managerOpenRequest(manager, checkoutCode);
    await Promise.all([
      manager.waitForLoadState("networkidle"),
      manager.getByRole("button", { name: "Nhận xử lý đơn" }).click()
    ]);
    manager.once("dialog", dialog => dialog.accept());
    await Promise.all([
      manager.waitForURL(/Scope=completed/),
      manager.getByRole("button", { name: "Duyệt trả phòng" }).click()
    ]);
    await student.goto(`${baseUrl}/Student/Dashboard`, { waitUntil: "networkidle" });
    if (!(await student.locator("main").innerText()).includes("Chưa có phòng hiện tại")) {
      issues.push("approved checkout did not end the source stay on the student dashboard");
    }

    process.stdout.write(JSON.stringify({ informationCode, checkoutCode, issues }, null, 2));
    if (issues.length > 0) process.exitCode = 1;
    await managerContext.close();
    await studentContext.close();
  } finally {
    await browser.close();
    cleanup(informationMarker);
    cleanup(checkoutMarker);
  }
})().catch(error => {
  process.stderr.write(error.stack);
  process.exitCode = 1;
});
