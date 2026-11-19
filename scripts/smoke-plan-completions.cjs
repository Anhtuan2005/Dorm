const assert = require("node:assert/strict");
const { spawnSync } = require("node:child_process");
const { chromium } = require("playwright");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const database = process.env.DORM_BROWSER_FIXTURE_DB ?? "";
assert.equal(new URL(baseUrl).hostname, "127.0.0.1");
assert.match(database, /^DormitoryManagementSystem_Test_[a-f0-9]{32}$/);

function sql(statement) {
  const result = spawnSync("sqlcmd", ["-S", "DESKTOP-OSSBOT1\\TUANANH", "-d", database, "-E", "-C", "-b", "-W", "-h", "-1", "-Q",
    `SET NOCOUNT ON; SET QUOTED_IDENTIFIER ON; ${statement}`], { encoding: "utf8", windowsHide: true });
  assert.equal(result.status, 0, result.stderr || result.stdout);
  return result.stdout.trim();
}

function seed() {
  sql(`
    INSERT INTO Buildings(Code, Name, SiteName, SortOrder) VALUES (N'QA', N'Toa QA', N'Fixture', 90), (N'QB', N'Toa QB', N'Fixture', 91);
    DECLARE @building int = (SELECT Id FROM Buildings WHERE Code = N'QA');
    INSERT INTO Floors(BuildingId, Number) VALUES (@building, 6), (@building, 12);
    DECLARE @floor int = (SELECT Id FROM Floors WHERE BuildingId = @building AND Number = 6);
    DECLARE @upper int = (SELECT Id FROM Floors WHERE BuildingId = @building AND Number = 12);
    DECLARE @student nvarchar(450) = (SELECT Id FROM AspNetUsers WHERE NormalizedEmail = N'STUDENT@NEPKTX.LOCAL');
    DECLARE @i int = 1;
    WHILE @i <= 81 BEGIN
      IF @i <= 26
        INSERT INTO Rooms(FloorId, Code, RoomType, Amenities, Capacity, MonthlyPrice)
        VALUES (@floor, CONCAT('QA-6', RIGHT(CONCAT('0', @i), 2)), N'Nam', N'Wi-Fi', 8, 1200000);
      INSERT INTO Notifications(UserId, Type, Title, Body, TargetUrl, CreatedAt)
      VALUES (@student, N'Fixture', CONCAT('QA notice ', RIGHT(CONCAT('00', @i), 3)), N'Fixture', N'/Student/Dashboard', DATEADD(minute, @i, '20200101'));
      SET @i += 1;
    END;
    INSERT INTO Rooms(FloorId, Code, RoomType, Amenities, Capacity, MonthlyPrice)
    VALUES (@upper, N'QA-1201', N'Nam', N'Wi-Fi', 8, 1200000);
    INSERT INTO MaintenanceRequests(RoomId, Code, Title, Category, Description, Priority, Status, BlockedBeds, BlockedBedNumbers, CreatedAt, UpdatedAt)
    SELECT Id, N'QA-RP-01', N'Fixture', N'Fixture', N'Fixture', N'Medium', N'New', 1, '1', SYSUTCDATETIME(), SYSUTCDATETIME() FROM Rooms WHERE Code = N'QA-601';`);
}

async function signIn(browser, email, width) {
  const page = await browser.newPage({ viewport: { width, height: 900 } });
  await page.goto(baseUrl + "/Account/Login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(e2ePassword);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.waitForURL(url => !url.pathname.startsWith("/Account/Login"));
  return page;
}

async function run() {
  seed();
  const browser = await chromium.launch({ channel: "msedge" });
  try {
    const staff = await signIn(browser, "manager@nepktx.local", 1440);
    const student = await signIn(browser, "student@nepktx.local", 390);
    await staff.goto(baseUrl + "/Rooms/Catalog");
    await staff.getByRole("link", { name: /^Tòa QA/ }).click();
    await staff.getByRole("link", { name: /^Tầng 12/ }).click();
    async function editFloor(number) {
      await staff.getByRole("button", { name: "Sửa tầng 12", exact: true }).click();
      const dialog = staff.locator("dialog[open]");
      await dialog.getByLabel("Số tầng mới").fill(number);
      await dialog.getByRole("button", { name: "Lưu tầng" }).click();
    }
    await editFloor("6");
    await staff.getByText("Tầng 6 đã tồn tại trong tòa này.", { exact: true }).waitFor();
    await editFloor("14");
    await staff.getByRole("link", { name: /^Tầng 14/ }).waitFor();
    await staff.locator("tbody").getByText("QA-1201", { exact: true }).waitFor();

    await staff.setViewportSize({ width: 390, height: 900 });
    await staff.goto(baseUrl + "/Rooms/Map?Building=QA&Floor=6&OnlyAvailable=true");
    assert.equal(await staff.locator(".building-tabs a").count(), 6);
    assert.match(await staff.locator(".room-card.maintenance").innerText(), /còn 7 chỗ/);
    await staff.getByRole("link", { name: "Tầng sau", exact: true }).click();
    assert.equal(new URL(staff.url()).searchParams.get("Floor"), "14");
    assert.equal(new URL(staff.url()).searchParams.get("OnlyAvailable")?.toLowerCase(), "true");
    await staff.locator(".floor-row.mobile-active").getByText("QA-1201", { exact: true }).waitFor();
    assert.ok(await staff.evaluate(() => document.documentElement.scrollWidth <= innerWidth + 1));
    await staff.getByRole("link", { name: /^Tòa QB/ }).click();
    assert.equal(await staff.locator(".building-tabs a.active strong").innerText(), "Tòa QB");
    assert.equal(await staff.locator(".room-detail").count(), 0);

    await student.goto(baseUrl + "/Student/Rooms?Building=QA&Sort=building");
    await student.getByText("27 phòng phù hợp", { exact: true }).waitFor();
    const firstPage = await student.locator(".student-room-card h3").allTextContents();
    assert.equal(firstPage.length, 24);
    await student.getByRole("link", { name: "Trang sau", exact: true }).click();
    assert.equal(new URL(student.url()).searchParams.get("Building"), "QA");
    const secondPage = await student.locator(".student-room-card h3").allTextContents();
    assert.equal(secondPage.length, 3);
    assert.ok(secondPage.every(room => !firstPage.includes(room)));

    await student.goto(baseUrl + "/Student/Notifications?page=4");
    await student.getByText("QA notice 001", { exact: true }).waitFor();
    const snapshot = await student.request.get(baseUrl + "/Student/NotificationSnapshot?panel=inbox&page=4");
    assert.ok(snapshot.ok());
    assert.match(await snapshot.text(), /data-notice-page="4"/);

    await student.goto(baseUrl + "/Student/Invoices");
    const unavailableCheckout = student.getByRole("button", { name: "Thanh toán qua VNPAY", exact: true }).first();
    assert.ok(await unavailableCheckout.isVisible());
    assert.ok(await unavailableCheckout.isDisabled());
    const guide = student.locator(".payment-guide");
    await guide.locator("summary").click();
    assert.equal(await guide.getAttribute("open"), null);
    await student.getByRole("link", { name: "Cách thanh toán", exact: true }).first().click();
    assert.ok(await guide.locator("#payment-guide").isVisible());
    assert.equal(await student.locator('form[action="/Student/Payments/Vnpay/Start"]').count(), 0);
    await student.locator(".invoice-transactions summary").first().click();
    await student.getByRole("link", { name: /^Biên nhận / }).first().click();
    await student.getByRole("heading", { name: "Biên nhận khoản thu" }).waitFor();
    await student.getByRole("button", { name: "In hoặc lưu PDF" }).waitFor();
    const receiptUrl = student.url();
    assert.match(receiptUrl, /\/Receipts\/\d+$/i);
    const otherReceipt = sql(`SELECT TOP 1 t.Id FROM PaymentTransactions t JOIN Invoices i ON i.Id = t.InvoiceId
      JOIN StudentProfiles p ON p.Id = i.StudentProfileId LEFT JOIN AspNetUsers u ON u.Id = p.UserId
      WHERE (u.Id IS NULL OR u.NormalizedEmail <> N'STUDENT@NEPKTX.LOCAL') AND t.Amount > 0 ORDER BY t.Id;`);
    assert.match(otherReceipt, /^\d+$/);
    assert.equal((await student.request.get(baseUrl + `/Receipts/${otherReceipt}`)).status(), 404);
    const privateReceipt = await staff.request.get(receiptUrl);
    assert.ok(privateReceipt.ok());
    assert.match(privateReceipt.headers()["cache-control"], /no-store/);
    await student.getByRole("link", { name: "Về hóa đơn" }).click();
    assert.equal(await student.locator(".invoice-transactions[open]").count(), 1);

    const periodExport = await staff.request.get(baseUrl + "/Reports/ExportCsv?Building=QA");
    assert.ok(periodExport.ok());
    const csv = await periodExport.text();
    assert.match(csv, /Toa QA/);
    assert.doesNotMatch(csv, /Tòa A/);
    assert.match(csv, /Đơn lưu trú trong kỳ/);
    assert.match(csv, /Sự cố trong kỳ/);

    sql("DELETE FROM VnpayCheckouts; DELETE FROM PaymentTransactions WHERE ReversesTransactionId IS NOT NULL; DELETE FROM PaymentTransactions; DELETE FROM InvoiceLines; DELETE FROM Invoices; DELETE FROM BillingPeriods;");
    const report = await staff.goto(baseUrl + "/Reports?Building=QA");
    assert.ok(report.ok());
    await staff.getByText("Chưa có kỳ thu để đối soát", { exact: true }).waitFor();
    const capacityExport = await staff.request.get(baseUrl + "/Reports/ExportCsv?Building=QA");
    assert.ok(capacityExport.ok());
    assert.match(capacityExport.headers()["content-disposition"], /bao-cao-van-hanh-suc-chua-QA.csv/);
    assert.doesNotMatch(await capacityExport.text(), /Chỉ số tài chính/);
    console.log("Floor editing, building/floor navigation, room/notification paging, receipt ownership and filtered/empty reports passed.");
  } finally {
    await browser.close();
  }
}

run().catch(error => { console.error(error.stack); process.exitCode = 1; });
