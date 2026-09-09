const { chromium } = require("C:\\Users\\Admin\\.cache\\codex-runtimes\\codex-primary-runtime\\dependencies\\node\\node_modules\\playwright");
const { spawnSync } = require("child_process");

const baseUrl = "http://127.0.0.1:5189";
const testStudent = "SV000001";
const testEmail = "e2e.student@student.nepktx.local";
const initialPassword = "Temporary@123";
const resetPassword = "Changed@123";
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
DECLARE @UserId nvarchar(450) = (SELECT Id FROM AspNetUsers WHERE NormalizedEmail = UPPER(N'${testEmail}'));
IF @UserId IS NOT NULL
BEGIN
    UPDATE StudentProfiles SET UserId = NULL WHERE UserId = @UserId AND StudentCode = N'${testStudent}';
    DELETE FROM AspNetUserClaims WHERE UserId = @UserId;
    DELETE FROM AspNetUserLogins WHERE UserId = @UserId;
    DELETE FROM AspNetUserTokens WHERE UserId = @UserId;
    DELETE FROM AspNetUserRoles WHERE UserId = @UserId;
    DELETE FROM AspNetUsers WHERE Id = @UserId;
END;
COMMIT TRANSACTION;`;

function cleanup() {
  const result = spawnSync(sqlcmd, ["-S", "DESKTOP-OSSBOT1\\TUANANH", "-d", "DormitoryManagementSystem", "-E", "-b", "-Q", cleanupSql], { encoding: "utf8" });
  if (result.status !== 0) throw new Error(`Cleanup failed: ${result.stderr || result.stdout}`);
}

async function fillLogin(page, email, password) {
  await page.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Mật khẩu").fill(password);
  await page.getByRole("button", { name: "Đăng nhập" }).click();
}

(async () => {
  cleanup();
  const browser = await chromium.launch({ executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", headless: true });
  const issues = [];
  try {
    const managerContext = await browser.newContext({ viewport: { width: 1400, height: 900 } });
    const manager = await managerContext.newPage();
    manager.setDefaultNavigationTimeout(60000);
    await fillLogin(manager, "manager@nepktx.local", "Demo@123");
    await manager.waitForURL(url => !url.pathname.includes("/Account/Login"));
    await manager.goto(`${baseUrl}/Students/Index?Search=${testStudent}`, { waitUntil: "networkidle" });
    await manager.getByRole("button", { name: "Cấp tài khoản" }).click();
    await manager.locator("#create-account-dialog input[name=Email]").fill(testEmail);
    await manager.locator("#create-account-dialog input[name=TemporaryPassword]").fill(initialPassword);
    await Promise.all([
      manager.waitForURL(/\/Students\/Index\?SelectedId=/),
      manager.getByRole("button", { name: "Cấp tài khoản", exact: true }).last().click()
    ]);
    const createdText = await manager.locator(".students-page").innerText();
    if (!createdText.includes(testEmail) || !createdText.includes("Đang hoạt động")) issues.push("created account was not linked to the student profile");

    await manager.getByRole("button", { name: "Đặt lại mật khẩu" }).click();
    await manager.locator("#reset-password-dialog input[name=TemporaryPassword]").fill(resetPassword);
    await Promise.all([
      manager.waitForURL(/\/Students\/Index\?SelectedId=/),
      manager.getByRole("button", { name: "Cập nhật mật khẩu" }).click()
    ]);

    manager.once("dialog", dialog => dialog.accept());
    await Promise.all([
      manager.waitForURL(/\/Students\/Index\?SelectedId=/),
      manager.getByRole("button", { name: "Khóa tài khoản" }).click()
    ]);
    if (!(await manager.locator(".students-page").innerText()).includes("Đang khóa")) issues.push("account lock state was not persisted");

    const lockedContext = await browser.newContext();
    const lockedLogin = await lockedContext.newPage();
    await fillLogin(lockedLogin, testEmail, resetPassword);
    await lockedLogin.waitForLoadState("networkidle");
    if (!lockedLogin.url().includes("/Account/Login")) issues.push("locked student account was able to sign in");
    await lockedContext.close();

    const unlockButton = manager.locator(".account-actions button.unlock");
    if ((await unlockButton.count()) !== 1) {
      throw new Error(`Unlock control missing after lock. Page state: ${await manager.locator(".account-control").innerText()}`);
    }
    manager.once("dialog", dialog => dialog.accept());
    await Promise.all([
      manager.waitForURL(/\/Students\/Index\?SelectedId=/),
      unlockButton.click()
    ]);
    if (!(await manager.locator(".students-page").innerText()).includes("Đang hoạt động")) issues.push("account unlock state was not persisted");

    const studentContext = await browser.newContext();
    const student = await studentContext.newPage();
    await fillLogin(student, testEmail, resetPassword);
    await student.waitForURL(/\/Student\/Dashboard/);
    if (!(await student.locator("main").innerText()).includes("Chào 0001")) issues.push("reset credentials did not open the correct student portal");
    await studentContext.close();
    await managerContext.close();

    process.stdout.write(JSON.stringify({ student: testStudent, issues }, null, 2));
    if (issues.length > 0) process.exitCode = 1;
  } finally {
    await browser.close();
    cleanup();
  }
})().catch(error => {
  process.stderr.write(error.stack);
  process.exitCode = 1;
});
