const assert = require("node:assert/strict");
const { execFileSync } = require("node:child_process");
const { chromium } = require("playwright");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const db = process.env.DORM_BROWSER_FIXTURE_DB;
assert.match(db || "", /^DormitoryManagementSystem_Test_[a-f0-9]{32}$/);
assert.equal(new URL(baseUrl).hostname, "127.0.0.1");
function sql(query) {
    return execFileSync("sqlcmd", ["-S", "DESKTOP-OSSBOT1\\TUANANH", "-d", db, "-E", "-C", "-b", "-W", "-h", "-1",
        "-Q", "SET QUOTED_IDENTIFIER ON; SET NOCOUNT ON; " + query], { encoding: "utf8", windowsHide: true }).trim();
}
const person = "(SELECT p.Id FROM StudentProfiles p JOIN AspNetUsers u ON u.Id=p.UserId WHERE u.Email='student@nepktx.local')";
const room = "(SELECT Id FROM Rooms WHERE Code='C-503')";
const date = days => new Date(Date.now() + (days * 24 + 7) * 3600000).toISOString().slice(0,10);
async function login(browser, email) {
    const page = await browser.newPage();
    page.setDefaultTimeout(9000);
    page.on("dialog", dialog => dialog.accept());
    await page.goto(baseUrl + "/Account/Login");
    await page.getByLabel("Email", {exact:true}).fill(email);
    await page.getByLabel("Mật khẩu", {exact:true}).fill(e2ePassword);
    await Promise.all([page.waitForURL(url => !url.pathname.endsWith("/Login")),page.getByRole("button",{name:"Đăng nhập"}).click()]);
    return page;
}
async function post(page, action, fields = {}, button) {
    const form = page.locator('form[action$="/' + action + '"]').first();
    await form.evaluate(node => { const details = node.closest("details"); if (details) details.open = true; });
    for (const [name, value] of Object.entries(fields)) {
        const field = form.locator('[name="' + name + '"]');
        if (await field.evaluate(el => el.tagName === "SELECT")) await field.selectOption(value);
        else await field.fill(value);
    }
    const submit = button ? form.getByRole("button",{name:button,exact:true}) : form.locator("button").last();
    await Promise.all([page.waitForNavigation(),submit.click()]);
}

async function meterPhoto(page, lines, redSuffix = "") {
    const encoded = await page.evaluate(({ rows, redSuffix }) => {
        const canvas = document.createElement("canvas");
        canvas.width = 640;
        canvas.height = rows.length * 100;
        const ctx = canvas.getContext("2d");
        ctx.fillStyle = "white";
        ctx.fillRect(0, 0, canvas.width, canvas.height);
        ctx.fillStyle = "black";
        ctx.font = "56px Arial";
        rows.forEach((line, i) => ctx.fillText(line, 42, 70 + i * 100));
        ctx.fillStyle = "#ad2734";
        ctx.fillText(redSuffix, 42 + ctx.measureText(rows[0]).width, 70);
        return canvas.toDataURL().split(",")[1];
    }, {rows: lines, redSuffix});
    return { name: "meter.png", mimeType: "image/png", buffer: Buffer.from(encoded, "base64") };
}

async function waterRegisterPhoto(page) {
    const encoded = await page.evaluate(() => {
        const canvas = document.createElement("canvas");
        canvas.width = 480;
        canvas.height = 400;
        const ctx = canvas.getContext("2d");
        ctx.fillStyle = "#d6d3c9";
        ctx.fillRect(0, 0, 480, 400);
        ctx.translate(65, 170);
        ctx.rotate(-.12);
        ctx.fillStyle = "#191c1c";
        ctx.font = "22px Arial";
        ctx.fillText("SN 789341", 25, -24);
        ctx.fillText("m³", 284, 24);
        ctx.fillRect(0, 0, 272, 55);
        ctx.fillStyle = "#881c22";
        ctx.fillRect(170, 0, 102, 55);
        ctx.fillStyle = "#eee";
        ctx.font = "38px Arial";
        [..."00103056"].forEach((digit, i) => ctx.fillText(digit, i * 34 + 5, 40));
        return canvas.toDataURL().split(",")[1];
    });
    return { name:"water-register.png", mimeType:"image/png", buffer:Buffer.from(encoded, "base64") };
}

async function verifyMeterPhotos(page) {
    const dialog = page.locator("#meter-photo");
    const upload = dialog.locator("input[type=file]");
    const power = page.locator("[name=powerEnd]");
    await page.locator("[data-read-meter=powerEnd]").click();
    await upload.setInputFiles(await meterPhoto(page, ["00121.5 kWh", "SN 88275"]));
    await dialog.getByText(/Chưa xác định được một chỉ số/).waitFor({ timeout: 50000 });
    assert.equal(await power.inputValue(), "");
    const area = await dialog.locator(".meter-image-area").boundingBox();
    await page.mouse.move(area.x + area.width * .03, area.y + area.height * .04);
    await page.mouse.down();
    await page.mouse.move(area.x + area.width * .92, area.y + area.height * .49, {steps:4});
    await page.mouse.up();
    await page.waitForFunction(() => document.querySelector("#meter-draft").value === "121.5", null, { timeout: 50000 });
    assert.equal(await power.inputValue(), "");
    await page.setViewportSize({width:390,height:844});
    assert.ok(await dialog.evaluate(el => el.scrollWidth <= el.clientWidth && el.getBoundingClientRect().right <= innerWidth));
    if (process.env.DORM_BROWSER_FIXTURE_ARTIFACTS) await dialog.screenshot({
        path: require("path").join(process.env.DORM_BROWSER_FIXTURE_ARTIFACTS, "meter-ocr-review.png")
    });
    await page.setViewportSize({width:1440,height:960});
    await dialog.getByRole("button", {name:"Điền vào sổ"}).click();
    assert.equal(await power.inputValue(), "121.5");

    await page.locator("[data-read-meter=waterEnd]").click();
    await upload.setInputFiles({ name:"broken.png", mimeType:"image/png", buffer:Buffer.from("broken") });
    await dialog.getByText(/Không mở được ảnh/).waitFor();
    await upload.setInputFiles(await meterPhoto(page, ["100 kWh"]));
    await dialog.getByText(/Chưa xác định được một chỉ số/).waitFor({timeout:50000});
    assert.equal(await dialog.locator("[data-use-reading]").isDisabled(), true);
    await upload.setInputFiles(await meterPhoto(page, ["0012"], "34"));
    await dialog.getByText(/Dãy số đổi sang màu đỏ/).waitFor({timeout:50000});
    assert.equal(await dialog.locator("[data-use-reading]").isDisabled(), true);
    await upload.setInputFiles(await meterPhoto(page, ["0102"]));
    await page.waitForFunction(() => document.querySelector("#meter-draft").value === "102", null, { timeout: 50000 });
    await upload.setInputFiles(await waterRegisterPhoto(page));
    await page.waitForFunction(() => document.querySelector("#meter-draft").value === "103.056", null, {timeout:50000});
    await dialog.getByRole("button", {name:"Điền vào sổ"}).click();
    assert.equal(await page.locator("[name=waterEnd]").inputValue(), "103.056");
    assert.equal(await power.inputValue(), "121.5");
    await page.locator("[data-read-meter=powerStart]").click();
    await upload.setInputFiles(await meterPhoto(page, ["344"]));
    await dialog.getByRole("button", {name:"Đóng"}).click();
    assert.equal(await page.locator("[name=powerStart]").inputValue(), "");
    assert.equal(sql("SELECT COUNT(*) FROM RoomMeterReadings r JOIN BillingPeriods p ON p.Id=r.BillingPeriodId WHERE p.Code='QA-METERS'"), "0");
}
(async () => {
    const browser = await chromium.launch({executablePath:"C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe",headless:true});
    try {
        const staff = await login(browser,"manager@nepktx.local");
        const student = await login(browser,"student@nepktx.local");
        await staff.setViewportSize({width:1440,height:960});
        await staff.goto(baseUrl + "/Applications");
        const actions = staff.locator(".intake-actions a");
        const bounds = await actions.evaluateAll(links => links.map(link => {
            const { left, right, top } = link.getBoundingClientRect();
            return { left, right, top };
        }));
        assert.equal(bounds.length, 3);
        assert.ok(bounds.every(action => Math.abs(action.top - bounds[0].top) < 1));
        assert.ok(bounds[1].left - bounds[0].right < 18);
        await staff.getByRole("link",{name:"Đợt cao điểm"}).click();
        const windowFields = {starts:date(0),ends:date(3),holdHours:"24"};
        await post(staff,"OpenPeak",windowFields);
        assert.match(await staff.locator("main").innerText(),/Đang mở/);
        await post(staff,"OpenPeak",windowFields);
        assert.match(await staff.locator("body").innerText(),/trùng với một đợt/);
        await staff.goto(baseUrl + "/Renewals");
        await post(staff,"Term",{name:"Học kỳ kiểm thử",opens:date(-2),closes:date(5),starts:date(7),ends:date(120)});
        await student.goto(baseUrl + "/Renewals/Mine");
        await post(student,"Apply");
        await staff.reload();
        await post(staff,"Decide",{},"Duyệt gia hạn");
        await student.reload();
        assert.match(await student.locator("main").innerText(), /Đã duyệt/);
        assert.equal(sql("SELECT CONVERT(varchar(10), PlannedEnd,23) FROM Stays WHERE EndedAt IS NULL AND StudentProfileId="+person),date(120));

        sql("INSERT INTO Stays (RoomId,StudentProfileId,StartedAt,EndedAt) SELECT "+room+",Id,'2001-01-01','2001-01-30' FROM StudentProfiles WHERE Id="+person+" OR Id=(SELECT MIN(Id) FROM StudentProfiles WHERE Id<>"+person+")");
        await staff.goto(baseUrl + "/Meters");
        await staff.getByRole("heading", {name:"Chưa có kỳ thu theo chỉ số"}).waitFor();
        const trial = staff.getByRole("button", {name:"Đọc thử ảnh đồng hồ"});
        await trial.click();
        const trialDialog = staff.getByRole("dialog", {name:"Đọc chỉ số từ ảnh"});
        assert.equal(await trialDialog.getByRole("button", {name:"Điền vào sổ"}).count(), 0);
        await trialDialog.locator("input[type=file]").setInputFiles(await meterPhoto(staff, ["00121.5 kWh"]));
        await staff.waitForFunction(() => document.querySelector("#meter-draft").value === "121.5", null, {timeout:50000});
        await trialDialog.getByRole("button", {name:"Đóng"}).click();
        assert.equal(sql("SELECT COUNT(*) FROM BillingPeriods WHERE Metered=1"), "0");
        assert.equal(sql("SELECT COUNT(*) FROM RoomMeterReadings"), "0");
        await post(staff,"CreatePeriod",{PeriodCode:"QA-METERS",PeriodName:"Kỳ chỉ số QA",StartDate:"2001-01-01",EndDate:"2001-01-31",DueDate:"2001-02-10"});
        await verifyMeterPhotos(staff);
        await staff.locator("[name=powerStart]").fill("101.5");
        await staff.locator("[name=powerRate]").fill("5000");
        assert.match(await staff.locator("#meter-estimate").innerText(), /100\.000 đ/);
        await staff.locator("[name=waterStart]").fill("100");
        await staff.locator("[name=waterRate]").fill("3000");
        assert.match(await staff.locator("#meter-estimate").innerText(), /3,056 m³.*9\.168 đ/);
        await post(staff,"Reading",{roomCode:"C-503"});
        assert.equal(sql("SELECT WaterEnd FROM RoomMeterReadings r JOIN BillingPeriods p ON p.Id=r.BillingPeriodId WHERE p.Code='QA-METERS'"), "103.056");
        await post(staff,"IssueInvoices");
        await staff.goto(baseUrl + "/Meters?periodId=" + sql("SELECT Id FROM BillingPeriods WHERE Code='QA-METERS'"));
        assert.equal(await staff.locator("[data-read-meter]").count(), 0);
        assert.equal(sql("SELECT COUNT(*) FROM Invoices i JOIN BillingPeriods p ON p.Id=i.BillingPeriodId WHERE p.Code='QA-METERS'"),"2");
        assert.equal(sql("SELECT CAST(SUM(l.Amount) AS int) FROM InvoiceLines l JOIN Invoices i ON i.Id=l.InvoiceId JOIN BillingPeriods p ON p.Id=i.BillingPeriodId WHERE p.Code='QA-METERS' AND l.ChargeType IN(N'Điện',N'Nước')"),"109168");
        await student.goto(baseUrl + "/Student/Invoices");
        assert.match(await student.locator("main").innerText(), /121[.,]5/);
        assert.match(await student.locator("main").innerText(), /103[.,]056/);

        const ownInvoice = sql("SELECT i.Id FROM Invoices i JOIN BillingPeriods p ON p.Id=i.BillingPeriodId WHERE p.Code='QA-METERS' AND i.StudentProfileId="+person);
        sql("INSERT INTO VnpayCheckouts (Reference,MerchantCode,InvoiceId,Amount,CreatedAtUtc,ExpiresAtUtc,State) VALUES ('qa-review','TESTKTX1',"+ownInvoice+",10000,SYSUTCDATETIME(),DATEADD(minute,15,SYSUTCDATETIME()),'NeedsReview')");
        await staff.goto(baseUrl + "/Billing?SelectedId=" + ownInvoice);
        await post(staff,"ResolveReview",{outcome:"unpaid",evidence:"QA Sandbox chưa ghi nhận khoản thu"});
        assert.equal(sql("SELECT State FROM VnpayCheckouts WHERE Reference='qa-review'"),"ReviewedUnpaid");
        assert.match(await staff.locator("details").filter({hasText:"Lịch sử đối soát VNPAY"}).textContent(), /QA Sandbox chưa ghi nhận khoản thu/);

        sql("UPDATE HousingRequests SET Status='Cancelled' WHERE StudentProfileId="+person+" AND Status IN ('Submitted','UnderReview','NeedsInformation','AwaitingMoveIn'); INSERT INTO HousingRequests (Code,StudentProfileId,SourceStayId,Type,Status,StudentReason,SubmittedAt,ActionRequiredAt) SELECT 'QA-HANDOVER',StudentProfileId,Id,'Checkout','UnderReview',N'Trả phòng',SYSUTCDATETIME(),SYSUTCDATETIME() FROM Stays WHERE StudentProfileId="+person+" AND EndedAt IS NULL");
        await staff.goto(baseUrl + "/Applications?Scope=all&Search=QA-HANDOVER");
        await post(staff,"Schedule",{appointment:date(1)+"T09:30"});
        await student.goto(baseUrl + "/Student/Applications");
        assert.match(await student.locator("main").innerText(), /09:30/);
        await post(staff,"Approve",{Keys:"Đủ, tốt",Furniture:"Thiếu",Equipment:"Không áp dụng"});
        assert.equal(sql("SELECT Status FROM HousingRequests WHERE Code='QA-HANDOVER'"),"Approved");
        await student.reload();
        assert.match(await student.locator("main").innerText(), /Nội thất: Thiếu/);
        sql("INSERT INTO RoomHolds (RoomId,HeldBeds,ExpiresAt) VALUES ("+room+",1,DATEADD(minute,-1,SYSUTCDATETIME())); DECLARE @hold int=SCOPE_IDENTITY(); INSERT INTO HousingRequests (Code,StudentProfileId,TargetRoomId,ApprovedRoomHoldId,Type,Status,StudentReason,SubmittedAt) VALUES ('QA-EXPIRED',"+person+","+room+",@hold,'Registration','AwaitingMoveIn',N'Giữ chỗ',SYSUTCDATETIME())");
        for (let attempt=0; attempt<32; attempt++) {
            if(sql("SELECT Status FROM HousingRequests WHERE Code='QA-EXPIRED'")==="Expired") break;
            await new Promise(done=>setTimeout(done,1000));
        }
        await student.reload();
        assert.match(await student.locator("main").innerText(),/Hết hạn giữ giường/);
        await student.goto(baseUrl + "/Student/Rooms");
        const card = student.locator(".student-room-card").filter({hasText:"Ảnh thật · Phòng tham khảo"}).first();
        await card.locator("img").evaluate(async image => { await image.decode(); });
        await card.locator("[data-apply-room]").click();
        assert.match(await student.locator("[data-room-booking]").innerText(),/giữ tạm giường trong 24 giờ/);
        await student.locator(".room-photo-grid img").first().evaluate(async image => { await image.decode(); });
        const bed = await student.locator("[name=TargetBedNumber] option").nth(1).getAttribute("value");
        await post(student,"SubmitHousingRequest",{TargetBedNumber:bed,Reason:"Đăng ký nội trú trong đợt cao điểm."});
        assert.match(await student.locator("main").innerText(),/Giữ giường đến/);
        const ownNewRequest = "(SELECT Id FROM HousingRequests WHERE StudentProfileId="+person+" AND Status='Submitted')";
        const held = "(SELECT ApprovedRoomHoldId FROM HousingRequests WHERE Id="+ownNewRequest+")";
        assert.equal(sql("SELECT HeldBeds FROM RoomHolds WHERE Id="+held+" AND ReleasedAt IS NULL"),"1");
        await staff.goto(baseUrl + "/Applications/Peak");
        await post(staff,"ClosePeak");
        assert.equal(sql("SELECT COUNT(*) FROM RoomHolds WHERE Id="+held+" AND ReleasedAt IS NULL"),"1");
        sql("UPDATE RoomHolds SET ExpiresAt=DATEADD(minute,-1,SYSUTCDATETIME()) WHERE Id="+held);
        for (let attempt=0; attempt<32; attempt++) {
            if(sql("SELECT COUNT(*) FROM RoomHolds WHERE Id="+held+" AND ReleasedAt IS NOT NULL")==="1") break;
            await new Promise(done=>setTimeout(done,1000));
        }
        assert.equal(sql("SELECT COUNT(*) FROM RoomHolds WHERE Id="+held+" AND ReleasedAt IS NOT NULL"),"1");
        await student.reload();
        assert.match(await student.locator("main").innerText(),/Hết hạn giữ tạm/);
        assert.equal(sql("SELECT COUNT(*) FROM HousingRequests WHERE Id="+ownNewRequest),"1");
        console.log("Renewal, billing, handover, peak registration, room photos and both hold expiries passed.");
    } finally { await browser.close(); }
})().catch(error => { console.error(error); process.exitCode=1; });
