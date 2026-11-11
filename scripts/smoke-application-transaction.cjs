const { chromium } = require("playwright");
const { spawnSync } = require("child_process");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const marker = "[E2E] Kiểm thử transaction xếp phòng tự động.";
const sqlcmd = "C:\\Program Files\\Microsoft SQL Server\\Client SDK\\ODBC\\170\\Tools\\Binn\\SQLCMD.EXE";

const cleanupSql = `
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
    DECLARE @TargetRoomId int = (SELECT TargetRoomId FROM HousingRequests WHERE Id = @RequestId);
    DECLARE @HoldId int = (SELECT ApprovedRoomHoldId FROM HousingRequests WHERE Id = @RequestId);
    DECLARE @SubmittedAt datetime2 = (SELECT SubmittedAt FROM HousingRequests WHERE Id = @RequestId);
    DELETE FROM Notifications
      WHERE UserId = (SELECT UserId FROM StudentProfiles WHERE Id = @StudentId)
        AND Type = N'Application'
        AND (Title LIKE N'%' + @RequestCode + N'%' OR Body LIKE N'%' + @RequestCode + N'%');
    DELETE FROM HousingRequestEvents WHERE HousingRequestId = @RequestId;
    DELETE FROM Stays WHERE HousingRequestId = @RequestId;
    DELETE FROM HousingRequests WHERE Id = @RequestId;
    DELETE FROM RoomHolds WHERE Id = @HoldId;
    UPDATE Stays SET EndedAt = NULL
      WHERE Id = (
        SELECT TOP (1) Id FROM Stays
        WHERE StudentProfileId = @StudentId AND RoomId <> @TargetRoomId
          AND EndedAt IS NOT NULL AND EndedAt >= @SubmittedAt
        ORDER BY EndedAt DESC);
END;
COMMIT TRANSACTION;`;

function cleanup() {
  const result = spawnSync(sqlcmd, ["-S", "DESKTOP-OSSBOT1\\TUANANH", "-d", "DormitoryManagementSystem", "-E", "-b", "-Q", cleanupSql], { encoding: "utf8" });
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

(async () => {
  cleanup();
  const browser = await chromium.launch({ executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", headless: true });
  const issues = [];
  try {
    const studentContext = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const student = await studentContext.newPage();
    await signIn(student, "student@nepktx.local");
    await student.goto(`${baseUrl}/Student/Rooms?Building=C`, { waitUntil: "networkidle" });
    const targetCard = student.locator(".student-room-card").filter({ hasText: "C-503" }).first();
    if (!(await targetCard.isVisible())) throw new Error("Room C-503 is unavailable for the student smoke test.");
    await targetCard.getByRole("button", { name: "Chọn phòng" }).click();
    await student.locator("[data-application-dialog] textarea[name=Reason]").fill(marker);
    await Promise.all([
      student.waitForURL(/\/Student\/Applications/),
      student.getByRole("button", { name: "Gửi đơn chuyển phòng" }).click()
    ]);
    const currentApplication = student.locator(".application-card.current").first();
    const requestText = await currentApplication.innerText();
    const requestCode = requestText.match(/DK-\d{4}-\d{4}/)?.[0];
    if (!requestCode) throw new Error("Could not read the submitted request code.");
    if (!requestText.includes("Phòng C-503") || !requestText.includes("Đã gửi")) issues.push("student request confirmation is incomplete");

    const managerContext = await browser.newContext({ viewport: { width: 1440, height: 950 } });
    const manager = await managerContext.newPage();
    await signIn(manager, "manager@nepktx.local");
    await manager.goto(`${baseUrl}/Applications/Index?Search=${encodeURIComponent(requestCode)}`, { waitUntil: "networkidle" });
    const managerText = await manager.locator(".applications-page").innerText();
    if (!managerText.includes(requestCode) || !managerText.includes("C-503") || !managerText.includes("Còn 8 chỗ")) {
      issues.push("manager did not receive the submitted request with live capacity");
    }
    await Promise.all([
      manager.waitForLoadState("networkidle"),
      manager.getByRole("button", { name: "Nhận xử lý đơn" }).click()
    ]);
    if (!(await manager.getByRole("button", { name: "Duyệt và giữ chỗ" }).isVisible())) {
      issues.push("review claim did not unlock the approval action");
    }
    manager.once("dialog", dialog => dialog.accept());
    await Promise.all([
      manager.waitForURL(/Scope=attention/),
      manager.getByRole("button", { name: "Duyệt và giữ chỗ" }).click()
    ]);
    const reservedText = await manager.locator(".applications-page").innerText();
    if (!reservedText.includes("Chờ nhận phòng") || !reservedText.includes("Xác nhận nhận phòng")) issues.push("approval did not create the move-in handoff");

    await student.goto(`${baseUrl}/Student/Dashboard`, { waitUntil: "networkidle" });
    const beforeMoveIn = await student.locator("main").innerText();
    if (!beforeMoveIn.includes("A-302") || beforeMoveIn.includes("Phòng C-503\nTòa C")) issues.push("student moved rooms before handover confirmation");

    manager.once("dialog", dialog => dialog.accept());
    await Promise.all([
      manager.waitForURL(/Scope=completed/),
      manager.getByRole("button", { name: "Xác nhận nhận phòng" }).click()
    ]);
    const decisionText = await manager.locator(".applications-page").innerText();
    if (!decisionText.includes("Đã duyệt") || !decisionText.includes("Đơn đã hoàn tất")) issues.push("move-in confirmation did not reach the completed state");

    await student.goto(`${baseUrl}/Student/Dashboard`, { waitUntil: "networkidle" });
    const dashboardText = await student.locator("main").innerText();
    if (!dashboardText.includes("C-503")) issues.push("approved room was not reflected on the student dashboard");
    await student.goto(`${baseUrl}/Student/Applications`, { waitUntil: "networkidle" });
    const approvedText = await student.locator(".application-list").innerText();
    if (!approvedText.includes(requestCode) || !approvedText.includes("Đã duyệt")) issues.push("student did not receive the approved application state");

    await managerContext.close();
    await studentContext.close();
    process.stdout.write(JSON.stringify({ requestCode, issues }, null, 2));
    if (issues.length > 0) process.exitCode = 1;
  } finally {
    await browser.close();
    cleanup();
  }
})().catch(error => {
  process.stderr.write(error.stack);
  process.exitCode = 1;
});
