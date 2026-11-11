const fs = require("fs");
const path = require("path");
const { spawnSync } = require("child_process");
const { chromium } = require("playwright");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const sqlcmd = "C:\\Program Files\\Microsoft SQL Server\\Client SDK\\ODBC\\170\\Tools\\Binn\\SQLCMD.EXE";
const server = "DESKTOP-OSSBOT1\\TUANANH";
const studentCode = "SV-E2E-9001";
const buildingCode = "QA";
const periodCode = "E2E-2609";
const repairTitle = "[E2E] Ảnh hiện trường vòi nước";
const uploadPath = path.resolve(__dirname, "..", "artifacts", "e2e-tiny.png");

function runSql(sql, scalar = false) {
  const settings = "SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;";
  const args = ["-S", server, "-d", "DormitoryManagementSystem", "-E", "-b"];
  if (scalar) args.push("-h", "-1", "-W");
  args.push("-Q", `${settings} ${sql}`);
  const result = spawnSync(sqlcmd, args, { encoding: "utf8" });
  if (result.status !== 0) throw new Error(`SQL failed: ${result.stderr || result.stdout}`);
  return result.stdout.trim();
}

function cleanup() {
  const storedName = runSql(`SET NOCOUNT ON; SELECT TOP 1 a.StoredName FROM MaintenanceAttachments a JOIN MaintenanceRequests r ON r.Id=a.MaintenanceRequestId WHERE r.Title=N'${repairTitle}';`, true).split(/\r?\n/)[0]?.trim();
  runSql(`
SET NOCOUNT ON;
DECLARE @RepairId int=(SELECT TOP 1 Id FROM MaintenanceRequests WHERE Title=N'${repairTitle}' ORDER BY Id DESC);
DELETE FROM MaintenanceAttachments WHERE MaintenanceRequestId=@RepairId;
DELETE FROM MaintenanceUpdates WHERE MaintenanceRequestId=@RepairId;
DELETE FROM MaintenanceRequests WHERE Id=@RepairId;
DECLARE @PeriodId int=(SELECT Id FROM BillingPeriods WHERE Code=N'${periodCode}');
DELETE FROM Notifications WHERE Type=N'InvoiceIssued' AND Title LIKE N'%E2E%';
DELETE FROM PaymentTransactions WHERE InvoiceId IN (SELECT Id FROM Invoices WHERE BillingPeriodId=@PeriodId);
DELETE FROM InvoiceLines WHERE InvoiceId IN (SELECT Id FROM Invoices WHERE BillingPeriodId=@PeriodId);
DELETE FROM Invoices WHERE BillingPeriodId=@PeriodId;
DELETE FROM BillingPeriods WHERE Id=@PeriodId;
DECLARE @StudentId int=(SELECT Id FROM StudentProfiles WHERE StudentCode=N'${studentCode}');
DELETE FROM Stays WHERE StudentProfileId=@StudentId;
DELETE FROM StudentProfiles WHERE Id=@StudentId;
DECLARE @BuildingId int=(SELECT Id FROM Buildings WHERE Code=N'${buildingCode}');
DELETE FROM Rooms WHERE FloorId IN (SELECT Id FROM Floors WHERE BuildingId=@BuildingId);
DELETE FROM Floors WHERE BuildingId=@BuildingId;
DELETE FROM Buildings WHERE Id=@BuildingId;`);
  if (storedName && !storedName.startsWith("(") && !storedName.includes("rows affected")) {
    const storedPath = path.resolve(__dirname, "..", "DormitoryManagementSystem", "App_Data", "maintenance-attachments", storedName);
    if (fs.existsSync(storedPath)) fs.unlinkSync(storedPath);
  }
  if (fs.existsSync(uploadPath)) fs.unlinkSync(uploadPath);
}

async function signIn(page, email) {
  await page.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  await page.getByLabel("Email").fill(email);
  await page.getByLabel("Mật khẩu").fill(e2ePassword);
  await Promise.all([page.waitForLoadState("networkidle"), page.getByRole("button", { name: "Đăng nhập" }).click()]);
}

async function submitDialog(page, id, fields, submitName) {
  const dialog = page.locator(`#${id}`);
  for (const [name, value] of Object.entries(fields)) {
    const input = dialog.locator(`[name="${name}"]`);
    if (await input.evaluate(node => node.tagName === "SELECT")) await input.selectOption(value);
    else await input.fill(String(value));
  }
  await Promise.all([page.waitForLoadState("networkidle"), dialog.getByRole("button", { name: submitName }).click()]);
}

(async () => {
  cleanup();
  fs.mkdirSync(path.dirname(uploadPath), { recursive: true });
  fs.writeFileSync(uploadPath, Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=", "base64"));
  const browser = await chromium.launch({ executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", headless: true });
  const issues = [];
  try {
    const managerContext = await browser.newContext({ viewport: { width: 1500, height: 960 } });
    const manager = await managerContext.newPage();
    await signIn(manager, "manager@nepktx.local");

    await manager.goto(`${baseUrl}/Rooms/Catalog`, { waitUntil: "networkidle" });
    await manager.getByRole("button", { name: "Thêm tòa" }).click();
    await submitDialog(manager, "create-building-dialog", { Code: buildingCode, Name: "Tòa kiểm thử QA", SiteName: "Cơ sở kiểm thử", SortOrder: "99" }, "Tạo tòa");
    if (!(await manager.locator(".main-content").innerText()).includes("Tòa QA")) issues.push("building create was not reflected");
    await manager.getByRole("button", { name: "Thêm tầng" }).click();
    await submitDialog(manager, "create-floor-dialog", { Number: "1" }, "Thêm tầng");
    await manager.getByRole("button", { name: "Thêm phòng", exact: true }).click();
    await submitDialog(manager, "create-room-dialog", { Code: "QA-101", RoomType: "Nam", Capacity: "8", MonthlyPrice: "990000", Amenities: "Wi-Fi|Bàn học" }, "Tạo phòng");
    const roomRow = manager.locator("tbody tr").filter({ hasText: "QA-101" });
    if (!(await roomRow.isVisible())) issues.push("room create was not reflected");

    await manager.goto(`${baseUrl}/Students`, { waitUntil: "networkidle" });
    await manager.getByRole("button", { name: "Thêm sinh viên" }).click();
    await submitDialog(manager, "create-profile-dialog", { StudentCode: studentCode, FullName: "Sinh Viên Kiểm Thử", Gender: "Nam", Faculty: "Công nghệ thông tin", PhoneNumber: "0900000001" }, "Tạo hồ sơ");
    if (!(await manager.locator(".main-content").innerText()).includes("Sinh Viên Kiểm Thử")) issues.push("student profile create was not reflected");
    await manager.getByRole("button", { name: "Sửa hồ sơ" }).click();
    await submitDialog(manager, "edit-profile-dialog", { Faculty: "Khoa Kiểm thử phần mềm" }, "Lưu hồ sơ");
    if (!(await manager.locator(".main-content").innerText()).includes("Khoa Kiểm thử phần mềm")) issues.push("student profile update was not reflected");

    runSql(`SET NOCOUNT ON; INSERT INTO Stays(StudentProfileId,RoomId,StartedAt) SELECT s.Id,r.Id,SYSUTCDATETIME() FROM StudentProfiles s CROSS JOIN Rooms r WHERE s.StudentCode=N'${studentCode}' AND r.Code=N'QA-101';`);
    await manager.goto(`${baseUrl}/Billing`, { waitUntil: "networkidle" });
    await manager.getByRole("button", { name: "Phát hành kỳ thu" }).click();
    await submitDialog(manager, "issue-invoices-dialog", { PeriodCode: periodCode, PeriodName: "Kỳ kiểm thử E2E", StartDate: "2026-09-01", EndDate: "2026-09-30", DueDate: "2026-09-15", BuildingCode: buildingCode, ElectricityAmount: "120000", WaterAmount: "80000", ServiceAmount: "50000" }, "Phát hành hóa đơn");
    const billingText = await manager.locator(".main-content").innerText();
    if (!billingText.includes("Sinh Viên Kiểm Thử") || !billingText.includes("Kỳ kiểm thử E2E") || !billingText.includes("1.240.000")) issues.push("issued invoice is incomplete");
    const invoiceCount = runSql(`SET NOCOUNT ON; SELECT COUNT(*) FROM Invoices i JOIN BillingPeriods p ON p.Id=i.BillingPeriodId WHERE p.Code=N'${periodCode}';`, true);
    if (!invoiceCount.split(/\r?\n/).some(line => line.trim() === "1")) issues.push(`expected one issued invoice, got ${invoiceCount}`);

    await manager.goto(`${baseUrl}/Rooms/Catalog?BuildingId=${runSql(`SET NOCOUNT ON; SELECT Id FROM Buildings WHERE Code=N'${buildingCode}';`, true).split(/\r?\n/)[0].trim()}`, { waitUntil: "networkidle" });
    await manager.locator("tbody tr").filter({ hasText: "QA-101" }).getByRole("button", { name: "Sửa" }).click();
    manager.once("dialog", dialog => dialog.accept());
    await Promise.all([manager.waitForLoadState("networkidle"), manager.locator(".manager-dialog[open] .catalog-delete-form button").click()]);
    if (!(await manager.locator("body").innerText()).includes("Không thể xóa phòng đã có lượt ở")) issues.push("historical room deletion was not blocked");

    const studentContext = await browser.newContext({ viewport: { width: 1280, height: 900 } });
    const student = await studentContext.newPage();
    await signIn(student, "student@nepktx.local");
    await student.goto(`${baseUrl}/Student/Repairs`, { waitUntil: "networkidle" });
    await student.getByRole("button", { name: "Gửi yêu cầu" }).click();
    const repairDialog = student.locator("[data-repair-dialog]");
    await repairDialog.locator('[name="Category"]').selectOption({ label: "Điện nước" });
    await repairDialog.locator('[name="Priority"]').selectOption("Medium");
    await repairDialog.locator('[name="Title"]').fill(repairTitle);
    await repairDialog.locator('[name="Description"]').fill("Vòi nước rò liên tục tại bồn rửa, cần kiểm tra trong ngày.");
    await repairDialog.locator('[name="Attachments"]').setInputFiles(uploadPath);
    await Promise.all([student.waitForLoadState("networkidle"), repairDialog.getByRole("button", { name: "Gửi yêu cầu" }).click()]);
    const repairCard = student.locator(".repair-card").filter({ hasText: repairTitle }).first();
    const attachmentCount = Number(runSql(`SET NOCOUNT ON; SELECT COUNT(*) FROM MaintenanceAttachments a JOIN MaintenanceRequests r ON r.Id=a.MaintenanceRequestId WHERE r.Title=N'${repairTitle}';`, true).split(/\r?\n/).find(line => /^\d+$/.test(line.trim())) ?? -1);
    if (!(await repairCard.isVisible()) || (await repairCard.locator(".repair-attachments img").count()) !== 1) {
      const feedback = await student.locator("body").innerText();
      issues.push(`repair attachment was not displayed (database=${attachmentCount}, feedback=${feedback.includes("không phải ảnh") ? "invalid-image" : feedback.includes("Đã gửi yêu cầu") ? "saved" : "none"})`);
    }
    else {
      const attachmentUrl = new URL(await repairCard.locator(".repair-attachments img").getAttribute("src"), baseUrl).href;
      const response = await student.request.get(attachmentUrl);
      if (!response.ok() || response.headers()["content-type"] !== "image/png") issues.push(`protected attachment response invalid: ${response.status()}`);
    }

    await studentContext.close();
    await managerContext.close();
    process.stdout.write(JSON.stringify({ invoiceCount: 1, attachmentCount, issues }, null, 2));
    if (issues.length) process.exitCode = 1;
  } finally {
    await browser.close();
    cleanup();
  }
})().catch(error => {
  process.stderr.write(`${error.stack}\n`);
  process.exitCode = 1;
});
