const assert = require("node:assert/strict");
const fs = require("node:fs");
const os = require("node:os");
const path = require("node:path");
const { chromium } = require("playwright");

const base = "http://localhost:5001";
const session = JSON.parse(fs.readFileSync(path.join(os.tmpdir(), "nepktx-ux-session.secrets.json"), "utf8"))[0];
const screenshots = path.join(__dirname, "forms");
fs.mkdirSync(screenshots, { recursive: true });
const checks = [];
const pageErrors = [];

async function checkPage(page, route) {
    const response = await page.goto(base + route, { waitUntil: "domcontentloaded" });
    assert.equal(response.status(), 200, route);
    assert.equal(new URL(page.url()).pathname, route);
    assert.equal(await page.locator("main").count(), 1, route);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false, route);
}

async function run() {
    const browser = await chromium.launch({
        executablePath: "C:/Program Files (x86)/Microsoft/Edge/Application/msedge.exe",
        headless: true
    });
    try {
        const student = await browser.newContext({ viewport: { width: 430, height: 932 }, locale: "vi-VN" });
        await student.addCookies(session.student);
        const page = await student.newPage();
        page.on("pageerror", error => pageErrors.push(error.message));
        await checkPage(page, "/Student/Repairs");
        const initialCount = await page.locator(".repair-card").count();
        const opener = page.locator("[data-repair-open]");
        await opener.click();
        const dialog = page.locator("[data-repair-dialog]");
        assert.equal(await dialog.isVisible(), true);
        const category = dialog.locator("[name=Category]");
        assert.equal(await category.evaluate(element => element === document.activeElement), true);
        const title = "Kiểm tra biểu mẫu sửa chữa";
        const description = "Đèn bàn cạnh cửa sổ không sáng kể từ tối hôm qua.";
        await category.selectOption({ label: "Điện nước" });
        await dialog.locator("[name=Priority]").selectOption("High");
        await dialog.locator("[name=Title]").fill(title);
        await dialog.locator("[name=Description]").fill(description);

        const image = await page.evaluate(() => {
            const canvas = document.createElement("canvas");
            canvas.width = 48;
            canvas.height = 48;
            const context = canvas.getContext("2d");
            context.fillStyle = "#638278";
            context.fillRect(0, 0, 48, 48);
            context.fillStyle = "#f0d2a0";
            context.fillRect(12, 12, 24, 24);
            return canvas.toDataURL("image/png").split(",")[1];
        });
        const png = name => ({ name, mimeType: "image/png", buffer: Buffer.from(image, "base64") });
        const attachment = dialog.locator("[data-attachment-input]");
        await attachment.focus();
        const picker = page.waitForEvent("filechooser");
        await page.keyboard.press("Enter");
        await (await picker).setFiles([png("den-ban.png"), png("cong-tac.png")]);
        await page.waitForFunction(() => [...document.querySelectorAll(".attachment-previews img")].every(img => img.naturalWidth > 0));
        assert.equal(await dialog.locator("[data-attachment-previews] li").count(), 2);
        assert.match(await dialog.locator("[data-attachment-count]").innerText(), /2\/3/);
        await dialog.getByRole("button", { name: "Bỏ ảnh den-ban.png" }).click();
        assert.deepEqual(await attachment.evaluate(input => Array.from(input.files, file => file.name)), ["cong-tac.png"]);
        assert.equal(await dialog.getByRole("button", { name: "Bỏ ảnh cong-tac.png" }).evaluate(button => button === document.activeElement), true);
        await page.screenshot({ path: path.join(screenshots, "repair-preview-430.png") });
        checks.push("Keyboard file picker, decoded thumbnails, removal and focus");

        const attachmentError = dialog.locator("[data-attachment-error]");
        const clear = dialog.locator("[data-attachment-clear]");
        await attachment.setInputFiles([1, 2, 3, 4].map(number => png(`anh-${number}.png`)));
        assert.match(await attachmentError.innerText(), /tối đa 3 ảnh/);
        assert.equal(await attachment.evaluate(input => input.form.checkValidity()), false);
        await clear.click();
        assert.equal(await attachment.evaluate(input => input.files.length), 0);
        assert.equal(await attachmentError.isVisible(), false);
        await attachment.setInputFiles({ name: "anh-lon.png", mimeType: "image/png", buffer: Buffer.alloc(5 * 1024 * 1024 + 1) });
        assert.match(await attachmentError.innerText(), /vượt quá 5 MB/);
        await attachment.setInputFiles({ name: "van-ban.txt", mimeType: "text/plain", buffer: Buffer.from("Tệp thử") });
        assert.match(await attachmentError.innerText(), /định dạng JPG, PNG hoặc WebP/);
        await clear.click();
        assert.equal(await attachment.evaluate(input => input.form.checkValidity()), true);
        checks.push("Too many, oversized and unsupported files block submission and can be cleared");

        await attachment.setInputFiles({ name: "anh-loi.png", mimeType: "image/png", buffer: Buffer.from("Ảnh không hợp lệ") });
        const [rejected] = await Promise.all([
            page.waitForResponse(response => response.url().endsWith("/Student/CreateRepair") && response.request().method() === "POST"),
            dialog.getByRole("button", { name: "Gửi yêu cầu", exact: true }).click()
        ]);
        assert.equal(rejected.status(), 200);
        await page.waitForLoadState("domcontentloaded");
        const summary = dialog.locator(".validation-summary-errors");
        await summary.waitFor();
        assert.match(await summary.innerText(), /không phải ảnh JPG, PNG hoặc WebP hợp lệ/);
        assert.equal(await summary.evaluate(element => element === document.activeElement), true);
        assert.equal(await category.inputValue(), "Điện nước");
        assert.equal(await dialog.locator("[name=Priority]").inputValue(), "High");
        assert.equal(await dialog.locator("[name=Title]").inputValue(), title);
        assert.equal(await dialog.locator("[name=Description]").inputValue(), description);
        assert.equal(await attachment.evaluate(input => input.files.length), 0);
        assert.match(await dialog.locator(".form-hint").innerText(), /chọn lại ảnh/);
        assert.equal(await page.locator(".repair-card").count(), initialCount);
        assert.equal(await dialog.locator("[name=__RequestVerificationToken]").count(), 1);
        await page.screenshot({ path: path.join(screenshots, "repair-server-error-430.png") });
        await page.keyboard.press("Escape");
        assert.equal(await dialog.isVisible(), false);
        checks.push("Real server rejection restores four fields, focuses error and preserves repair history");

        await page.setViewportSize({ width: 360, height: 780 });
        await checkPage(page, "/Student/Repairs");
        await opener.click();
        assert.equal(await dialog.evaluate(element => element.scrollWidth > element.clientWidth), false);
        await page.keyboard.press("Escape");
        await checkPage(page, "/Student/Invoices");
        await page.locator(".payment-guide summary").click();
        assert.match(await page.locator(".payment-guide p").innerText(), /văn phòng KTX/);
        assert.equal(await page.locator("[data-demo-message]").count(), 0);
        assert.equal(await page.evaluate(() => document.documentElement.scrollWidth > innerWidth), false);
        checks.push("Payment instructions expand in place and fit a 360 px screen");

        await checkPage(page, "/Student/Applications");
        for (const card of await page.locator(".application-card").all()) {
            assert.equal(await card.locator(".student-request-handoff").count(), await card.locator("form[action$=CancelHousingRequest]").count());
        }
        checks.push("Only active housing requests show a next step");

        await checkPage(page, "/Account/ChangePassword");
        const currentPassword = page.locator("#CurrentPassword");
        const newPassword = page.locator("#NewPassword");
        const confirmPassword = page.locator("#ConfirmPassword");
        await currentPassword.fill("NotARealPassword9");
        await newPassword.fill("TestPassword9");
        await confirmPassword.fill("TestPassword8");
        assert.match(await page.locator("[data-password-match]").innerText(), /chưa khớp/);
        assert.equal(await confirmPassword.evaluate(input => input.validity.customError), true);
        await confirmPassword.fill("TestPassword9");
        assert.equal(await confirmPassword.evaluate(input => input.validity.customError), false);
        await page.getByRole("button", { name: "Hiện mật khẩu mới", exact: true }).click();
        assert.equal(await newPassword.getAttribute("type"), "text");
        await page.getByRole("button", { name: "Ẩn mật khẩu mới", exact: true }).click();
        assert.equal(await newPassword.getAttribute("type"), "password");
        await page.reload();
        await page.screenshot({ path: path.join(screenshots, "password-360.png") });
        checks.push("Password mismatch validation and show/hide work on mobile");

        const anonymous = await browser.newContext({ viewport: { width: 1440, height: 900 } });
        const entry = await anonymous.newPage();
        entry.on("pageerror", error => pageErrors.push(error.message));
        await checkPage(entry, "/Account/Login");
        await entry.getByRole("link", { name: "Thông tin dữ liệu và hỗ trợ" }).click();
        assert.match(await entry.locator("h1").innerText(), /Thông tin dữ liệu và hỗ trợ/);
        assert.match(await entry.locator("main").innerText(), /Google Gemini/);
        await entry.getByRole("link", { name: "Về trang đăng nhập" }).click();
        assert.equal(new URL(entry.url()).pathname, "/Account/Login");
        await checkPage(entry, "/Account/AccessDenied");
        assert.match(await entry.locator("h1").innerText(), /Không có quyền truy cập/);
        await checkPage(entry, "/Home/Error");
        assert.equal(await entry.locator(".info-trace code").isVisible(), true);
        assert.equal(await entry.locator("nav").count(), 0);
        checks.push("Anonymous data information and standalone access/error pages");

        await page.setViewportSize({ width: 1440, height: 1000 });
        for (const route of ["/Student/Repairs", "/Student/Invoices", "/Student/Applications", "/Account/ChangePassword"]) {
            await checkPage(page, route);
        }
        await checkPage(page, "/Student/Repairs");
        await opener.click();
        assert.equal(await dialog.evaluate(element => element.scrollWidth > element.clientWidth), false);
        checks.push("Desktop form and records have a single main landmark and fit the viewport");

        assert.deepEqual(pageErrors, []);
        fs.writeFileSync(path.join(screenshots, "checks.json"), JSON.stringify({ checks, pageErrors }, null, 2));
        console.log(JSON.stringify({ checks, pageErrors }));
    } finally {
        await browser.close();
    }
}

run().catch(error => {
    console.error(error);
    process.exitCode = 1;
});
