const fs = require("fs");
const path = require("path");
const { spawnSync } = require("child_process");
const { chromium } = require("playwright");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const marker = "[CAPTURE] Minh họa bàn giao bổ sung hồ sơ.";
const outputDir = path.resolve(__dirname, "..", "artifacts", "screenshots");
const sqlcmd = "C:\\Program Files\\Microsoft SQL Server\\Client SDK\\ODBC\\170\\Tools\\Binn\\SQLCMD.EXE";
fs.mkdirSync(outputDir, { recursive: true });

function cleanup() {
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
    DELETE FROM Notifications WHERE UserId = (SELECT UserId FROM StudentProfiles WHERE Id = @StudentId) AND Type = N'Application' AND (Title LIKE N'%' + @RequestCode + N'%' OR Body LIKE N'%' + @RequestCode + N'%');
    DELETE FROM HousingRequestEvents WHERE HousingRequestId = @RequestId;
    DELETE FROM Stays WHERE HousingRequestId = @RequestId;
    DELETE FROM HousingRequests WHERE Id = @RequestId;
END;
COMMIT TRANSACTION;`;
  const result = spawnSync(sqlcmd, ["-S", "DESKTOP-OSSBOT1\\TUANANH", "-d", "DormitoryManagementSystem", "-E", "-b", "-Q", sql], { encoding: "utf8" });
  if (result.status !== 0) throw new Error(result.stderr || result.stdout);
}

async function signIn(page, email) {
  await page.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Mật khẩu").fill(e2ePassword);
  await Promise.all([page.waitForLoadState("networkidle"), page.getByRole("button", { name: "Đăng nhập" }).click()]);
}

function watch(page, prefix, issues) {
  page.on("pageerror", error => issues.push(`${prefix} pageerror: ${error.message}`));
  page.on("console", message => { if (message.type() === "error") issues.push(`${prefix} console: ${message.text()}`); });
  page.on("requestfailed", request => issues.push(`${prefix} request failed: ${request.url()}`));
}

(async () => {
  cleanup();
  const browser = await chromium.launch({ executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", headless: true });
  const issues = [];
  try {
    const studentContext = await browser.newContext({ viewport: { width: 1440, height: 1000 }, deviceScaleFactor: 1 });
    const managerContext = await browser.newContext({ viewport: { width: 1586, height: 992 }, deviceScaleFactor: 1 });
    const student = await studentContext.newPage();
    const manager = await managerContext.newPage();
    watch(student, "student desktop", issues);
    watch(manager, "manager desktop", issues);
    await signIn(student, "student@nepktx.local");
    await signIn(manager, "manager@nepktx.local");

    await student.goto(`${baseUrl}/Student/Rooms?Building=C`, { waitUntil: "networkidle" });
    const room = student.locator(".student-room-card").filter({ hasText: "C-503" }).first();
    await room.getByRole("button", { name: "Chọn phòng" }).click();
    await student.locator("[data-application-dialog] textarea[name=Reason]").fill(marker);
    await Promise.all([student.waitForURL(/\/Student\/Applications/), student.getByRole("button", { name: "Gửi đơn chuyển phòng" }).click()]);
    const code = (await student.locator(".application-card.current").first().innerText()).match(/DK-\d{4}-\d{4,}/)?.[0];
    if (!code) throw new Error("Không đọc được mã đơn chụp ảnh.");

    await manager.goto(`${baseUrl}/Applications/Index?Search=${encodeURIComponent(code)}`, { waitUntil: "networkidle" });
    await Promise.all([manager.waitForLoadState("networkidle"), manager.getByRole("button", { name: "Nhận xử lý đơn" }).click()]);
    await manager.getByRole("button", { name: "Yêu cầu bổ sung" }).click();
    await manager.locator("#information-dialog textarea").fill("Vui lòng bổ sung ngày chuyển dự kiến và thời gian có thể bàn giao phòng A-302.");
    await Promise.all([manager.waitForLoadState("networkidle"), manager.locator("#information-dialog button[type=submit]").click()]);

    await manager.screenshot({ path: path.join(outputDir, "housing-request-handoff-manager-desktop.png"), fullPage: true });
    const managerText = await manager.locator(".applications-page").innerText();
    if (!managerText.includes("Lượt xử lý hiện tại") || !managerText.includes("Sinh viên") || !managerText.includes("Chưa thể duyệt")) {
      issues.push("manager handoff state is incomplete");
    }

    await student.goto(`${baseUrl}/Student/Applications`, { waitUntil: "networkidle" });
    await student.screenshot({ path: path.join(outputDir, "housing-request-handoff-student-desktop.png"), fullPage: true });
    const studentText = await student.locator("main").innerText();
    if (!studentText.includes("Cần bổ sung") || !studentText.includes("Lượt xử lý hiện tại") || !studentText.includes("Gửi bổ sung và chuyển lại BQL")) {
      issues.push("student handoff state is incomplete");
    }

    const mobileContext = await browser.newContext({ viewport: { width: 430, height: 932 }, deviceScaleFactor: 1 });
    const mobile = await mobileContext.newPage();
    watch(mobile, "student mobile", issues);
    await signIn(mobile, "student@nepktx.local");
    await mobile.goto(`${baseUrl}/Student/Applications`, { waitUntil: "networkidle" });
    await mobile.screenshot({ path: path.join(outputDir, "housing-request-handoff-student-mobile.png"), fullPage: true });
    await mobileContext.close();
    await managerContext.close();
    await studentContext.close();

    process.stdout.write(JSON.stringify({ code, outputDir, issues }, null, 2));
    if (issues.length > 0) process.exitCode = 1;
  } finally {
    await browser.close();
    cleanup();
  }
})().catch(error => {
  process.stderr.write(error.stack);
  process.exitCode = 1;
});
