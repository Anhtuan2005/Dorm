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
        await post(staff,"CreatePeriod",{PeriodCode:"QA-METERS",PeriodName:"Kỳ chỉ số QA",StartDate:"2001-01-01",EndDate:"2001-01-31",DueDate:"2001-02-10"});
        await post(staff,"Reading",{roomCode:"C-503",powerStart:"101.5",powerEnd:"121.5",powerRate:"5000",waterStart:"100",waterEnd:"103",waterRate:"3000"});
        await post(staff,"IssueInvoices");
        assert.equal(sql("SELECT COUNT(*) FROM Invoices i JOIN BillingPeriods p ON p.Id=i.BillingPeriodId WHERE p.Code='QA-METERS'"),"2");
        assert.equal(sql("SELECT CAST(SUM(l.Amount) AS int) FROM InvoiceLines l JOIN Invoices i ON i.Id=l.InvoiceId JOIN BillingPeriods p ON p.Id=i.BillingPeriodId WHERE p.Code='QA-METERS' AND l.ChargeType IN(N'Điện',N'Nước')"),"109000");
        await student.goto(baseUrl + "/Student/Invoices");
        assert.match(await student.locator("main").innerText(), /121[.,]5/);

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
        const card = student.locator(".student-room-card").filter({hasText:"Ảnh minh họa AI"}).first();
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
