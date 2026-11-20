const fs = require("fs");
const assert = require("node:assert/strict");
const path = require("path");
const { spawnSync } = require("child_process");
const { chromium } = require("playwright");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");
const database = process.env.DORM_BROWSER_FIXTURE_DB || "DormitoryManagementSystem";
if (process.env.DORM_BROWSER_FIXTURE_DB && !/^DormitoryManagementSystem_Test_[a-f0-9]{32}$/.test(database))
  throw new Error("Expected a disposable SQL fixture database.");

const sqlcmd = "C:\\Program Files\\Microsoft SQL Server\\Client SDK\\ODBC\\170\\Tools\\Binn\\SQLCMD.EXE";
const server = "DESKTOP-OSSBOT1\\TUANANH";
const studentCode = "SV-E2E-9001";
const buildingCode = "QA";
const periodCode = "E2E-2609";
const repairTitle = "[E2E] Ảnh hiện trường vòi nước";
const uploadPath = path.resolve(__dirname, "..", "artifacts", "e2e-tiny.png");

function runSql(sql, scalar = false) {
  const settings = "SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;";
  const args = ["-S", server, "-d", database, "-E", "-C", "-b"];
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
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(e2ePassword);
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
    const createRoom = manager.locator("#create-room-dialog");
    assert.equal(await createRoom.getByRole("checkbox").count(), 9);
    assert.equal(await createRoom.locator('[name="OtherAmenities"]').count(), 0);
    await createRoom.getByRole("checkbox", { name: "Wi-Fi", exact: true }).check();
    await createRoom.getByRole("checkbox", { name: "Bàn học", exact: true }).check();
    await submitDialog(manager, "create-room-dialog", { Code: "QA-101", RoomType: "Nam", Capacity: "8", MonthlyPrice: "990000.00" }, "Tạo phòng");
    const roomRow = manager.locator("tbody tr").filter({ hasText: "QA-101" });
    await roomRow.waitFor();
    const roomAmenities = roomRow.locator("td").first().locator("small");
    assert.equal(await roomAmenities.innerText(), "WC khép kín · Wi-Fi · Bàn học");

    await roomRow.getByRole("button", { name: "Sửa", exact: true }).click();
    const editRoom = manager.locator("dialog[open]");
    assert.equal(Number(await editRoom.locator('[name="MonthlyPrice"]').inputValue()), 990000);
    for (const name of ["Wi-Fi", "Bàn học"])
      assert.ok(await editRoom.getByRole("checkbox", { name, exact: true }).isChecked());
    await editRoom.getByRole("checkbox", { name: "Bàn học", exact: true }).uncheck();
    await editRoom.getByRole("checkbox", { name: "Quạt trần", exact: true }).check();
    assert.equal(await editRoom.locator('[name="OtherAmenities"]').count(), 0);
    if (process.env.DORM_BROWSER_FIXTURE_ARTIFACTS) {
      const artifacts = process.env.DORM_BROWSER_FIXTURE_ARTIFACTS;
      await editRoom.screenshot({ path: path.join(artifacts, "room-amenities-desktop.png") });
      await manager.setViewportSize({ width: 390, height: 844 });
      assert.ok(await editRoom.evaluate(dialog => dialog.scrollWidth <= dialog.clientWidth));
      await editRoom.screenshot({ path: path.join(artifacts, "room-amenities-mobile.png") });
      await manager.setViewportSize({ width: 1500, height: 960 });
    }
    await editRoom.getByRole("button", { name: "Lưu phòng", exact: true }).click();
    await roomRow.getByText("WC khép kín · Wi-Fi · Quạt trần", { exact: true }).waitFor();

    await roomRow.getByRole("button", { name: "Sửa", exact: true }).click();
    const checked = editRoom.getByRole("checkbox", { checked: true });
    while (await checked.count()) await checked.first().uncheck();
    await Promise.all([manager.waitForNavigation(), editRoom.getByRole("button", { name: "Lưu phòng", exact: true }).click()]);
    assert.equal(await roomRow.count(), 1, await manager.locator(".workspace-flash").first().textContent());
    assert.equal(await roomAmenities.innerText(), "WC khép kín");
    assert.equal(await roomRow.getByText("990.000 đ", { exact: true }).count(), 1);

    await manager.goto(`${baseUrl}/Students`, { waitUntil: "networkidle" });
    await manager.getByRole("button", { name: "Thêm sinh viên" }).click();
    await submitDialog(manager, "create-profile-dialog", { StudentCode: studentCode, FullName: "Sinh Viên Kiểm Thử", Gender: "Nam", Faculty: "Công nghệ thông tin", PhoneNumber: "0900000001" }, "Tạo hồ sơ");
    if (!(await manager.locator(".main-content").innerText()).includes("Sinh Viên Kiểm Thử")) issues.push("student profile create was not reflected");
    await manager.getByRole("button", { name: "Sửa hồ sơ" }).click();
    await submitDialog(manager, "edit-profile-dialog", { Faculty: "Khoa Kiểm thử phần mềm" }, "Lưu hồ sơ");
    if (!(await manager.locator(".main-content").innerText()).includes("Khoa Kiểm thử phần mềm")) issues.push("student profile update was not reflected");

    runSql(`SET NOCOUNT ON; INSERT INTO Stays(StudentProfileId,RoomId,StartedAt) SELECT s.Id,r.Id,'20260901' FROM StudentProfiles s CROSS JOIN Rooms r WHERE s.StudentCode=N'${studentCode}' AND r.Code=N'QA-101';`);
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
    await repairDialog.getByLabel("Chia sẻ sự cố với bạn cùng phòng").check();
    await repairDialog.locator('[name="Attachments"]').setInputFiles(uploadPath);
    await Promise.all([student.waitForLoadState("networkidle"), repairDialog.getByRole("button", { name: "Gửi yêu cầu" }).click()]);
    const repairCard = student.locator(".repair-card").filter({ hasText: repairTitle }).first();
    const attachmentCount = Number(runSql(`SET NOCOUNT ON; SELECT COUNT(*) FROM MaintenanceAttachments a JOIN MaintenanceRequests r ON r.Id=a.MaintenanceRequestId WHERE r.Title=N'${repairTitle}';`, true).split(/\r?\n/).find(line => /^\d+$/.test(line.trim())) ?? -1);
    if (!(await repairCard.isVisible()) || (await repairCard.locator(".repair-attachments img").count()) !== 1) {
      const feedback = await student.locator("body").innerText();
      issues.push(`repair attachment was not displayed (database=${attachmentCount}, feedback=${feedback.includes("không phải ảnh") ? "invalid-image" : feedback.includes("Đã gửi yêu cầu") ? "saved" : "none"})`);
    }
    else {
      if (!await repairCard.getByText("Đã chia sẻ tiêu đề và tiến độ với phòng.").isVisible())
        issues.push("repair sharing checkbox was not saved");
      const attachmentUrl = new URL(await repairCard.locator(".repair-attachments img").getAttribute("src"), baseUrl).href;
      const response = await student.request.get(attachmentUrl);
      if (!response.ok() || response.headers()["content-type"] !== "image/png") issues.push(`protected attachment response invalid: ${response.status()}`);
    }

    if (process.env.DORM_BROWSER_FIXTURE_DB) {
      runSql(`
INSERT MaintenanceRequests (RoomId, ReporterStudentId, Code, Title, Category, Description, Priority, Status, BlockedBeds, CreatedAt, UpdatedAt, ShareWithRoom)
SELECT TOP (1) currentStay.RoomId, roommate.StudentProfileId, N'SC-E2E-FOLLOW', N'Điều hòa ngừng hoạt động', N'Thiết bị phòng',
       N'Ghi chú riêng của người báo', N'Medium', N'New', 0, SYSUTCDATETIME(), SYSUTCDATETIME(), 1
FROM AspNetUsers u
JOIN StudentProfiles studentProfile ON studentProfile.UserId=u.Id
JOIN Stays currentStay ON currentStay.StudentProfileId=studentProfile.Id AND currentStay.EndedAt IS NULL
JOIN Stays roommate ON roommate.RoomId=currentStay.RoomId AND roommate.EndedAt IS NULL AND roommate.StudentProfileId<>studentProfile.Id
WHERE u.Email=N'student@nepktx.local'
ORDER BY roommate.Id;`);
      await student.reload({ waitUntil: "networkidle" });
      const shared = student.locator(".shared-repair").filter({ hasText: "SC-E2E-FOLLOW" });
      await shared.getByRole("button", { name: "Theo dõi sự cố" }).click();
      await shared.getByRole("button", { name: "Ngừng theo dõi" }).waitFor();
      if ((await shared.innerText()).includes("Ghi chú riêng của người báo")) issues.push("shared repair exposed the description");
      await student.goto(`${baseUrl}/Student/Notifications`, { waitUntil: "networkidle" });

      await manager.goto(`${baseUrl}/Maintenance?Search=SC-E2E-FOLLOW`, { waitUntil: "networkidle" });
      const update = manager.locator(".maintenance-action-form");
      await update.locator('[name="Status"]').selectOption("InProgress");
      await update.locator('[name="AssignedTeam"]').selectOption("Tổ Thiết bị");
      await update.locator('[name="Note"]').fill("Ghi chú cập nhật riêng của Ban quản lý.");
      await update.getByRole("button", { name: "Lưu cập nhật" }).click();
      const notice = student.getByRole("link").filter({ hasText: /SC-E2E-FOLLOW · Đang xử lý/ });
      await notice.waitFor({ timeout: 12000 });
      await notice.click();
      await shared.getByText("Đang xử lý", { exact: true }).waitFor();
      await shared.getByRole("button", { name: "Ngừng theo dõi" }).click();
      await shared.getByRole("button", { name: "Theo dõi sự cố" }).waitFor();

      await student.goto(`${baseUrl}/Student/Invoices`, { waitUntil: "networkidle" });
      const comparison = student.locator(".student-invoice-card").filter({ hasText: "HD-2026-09-02106" }).locator(".invoice-comparison");
      await comparison.locator("summary").click();
      if (!(await comparison.innerText()).includes("Tăng 47.000 đ") || !await comparison.getByText("Tiền điện", { exact: true }).isVisible())
        issues.push("invoice did not show the expected previous-period total and charge changes");
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
