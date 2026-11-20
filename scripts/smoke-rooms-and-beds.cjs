const assert = require("node:assert/strict");
const path = require("node:path");
const { spawnSync } = require("node:child_process");
const { chromium } = require("playwright");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

assert.equal(new URL(baseUrl).hostname, "127.0.0.1");
assert.match(process.env.DORM_BROWSER_FIXTURE_DB ?? "", /^DormitoryManagementSystem_Test_[a-f0-9]{32}$/);

const room = "C-503";
const caption = "Góc bàn học phòng C-503";
const photo = Buffer.from("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/Z4QAAAAASUVORK5CYII=", "base64");
const currentRoommates = ["Lê Minh Hạ", "Nguyễn An Khôi", "Trần Bình An"];
const destinationRoommate = "Bùi Minh Tâm";
const formerRoommate = "Võ An Nhiên";

function addFixtureRoommates() {
  const sql = `
    SET NOCOUNT ON;
    SET XACT_ABORT ON;
    SET QUOTED_IDENTIFIER ON;
    BEGIN TRANSACTION;
    DECLARE @CurrentStay int = (SELECT s.Id FROM Stays s JOIN StudentProfiles p ON p.Id = s.StudentProfileId
      JOIN AspNetUsers u ON u.Id = p.UserId WHERE u.NormalizedEmail = N'STUDENT@NEPKTX.LOCAL' AND s.EndedAt IS NULL);
    INSERT INTO Rooms (FloorId, Code, RoomType, Amenities, Capacity, MonthlyPrice)
    SELECT r.FloorId, N'C-QA-RM', r.RoomType, r.Amenities, 8, r.MonthlyPrice
    FROM Rooms r JOIN Stays s ON s.RoomId = r.Id WHERE s.Id = @CurrentStay;
    DECLARE @Source int = CONVERT(int, SCOPE_IDENTITY());
    UPDATE Stays SET RoomId = @Source WHERE Id = @CurrentStay;
    DECLARE @Destination int = (SELECT Id FROM Rooms WHERE Code = N'C-503');
    INSERT INTO StudentProfiles (StudentCode, FullName, Gender, Faculty, PhoneNumber) VALUES
      (N'QA-RM-01', N'${currentRoommates[0]}', N'Nam', N'Khoa thử nghiệm', N'0999999999'),
      (N'QA-RM-02', N'${currentRoommates[1]}', N'Nam', N'Khoa thử nghiệm', N'0999999999'),
      (N'QA-RM-03', N'${currentRoommates[2]}', N'Nam', N'Khoa thử nghiệm', N'0999999999'),
      (N'QA-RM-04', N'${destinationRoommate}', N'Nam', N'Khoa thử nghiệm', N'0999999999'),
      (N'QA-RM-05', N'${formerRoommate}', N'Nam', N'Khoa thử nghiệm', N'0999999999');
    INSERT INTO Stays (StudentProfileId, RoomId, StartedAt, EndedAt, BedNumber)
    SELECT Id, CASE WHEN StudentCode IN (N'QA-RM-04', N'QA-RM-05') THEN @Destination ELSE @Source END,
      DATEADD(day, -2, SYSUTCDATETIME()), CASE WHEN StudentCode = N'QA-RM-05' THEN DATEADD(day,-1,SYSUTCDATETIME()) END,
      CASE WHEN StudentCode = N'QA-RM-04' THEN 2 END
    FROM StudentProfiles WHERE StudentCode LIKE N'QA-RM-%';
    UPDATE Rooms SET Amenities = N'Bàn học|Quạt|Tủ lạnh|Kệ bên máy lạnh' WHERE Code = N'C-504';
    COMMIT TRANSACTION;`;
  const result = spawnSync("sqlcmd", [
    "-S", "DESKTOP-OSSBOT1\\TUANANH", "-d", process.env.DORM_BROWSER_FIXTURE_DB, "-E", "-C", "-b", "-Q", sql
  ], { encoding: "utf8", windowsHide: true });
  assert.equal(result.status, 0, result.stderr || result.stdout);
}

async function assertRoommates(page, names) {
  const list = page.locator("#roommates .student-roommate-list li");
  assert.deepEqual((await list.allTextContents()).sort(), [...names].sort());
}

async function signIn(browser, email) {
  const page = await browser.newPage({ viewport: { width: 1440, height: 900 }, reducedMotion: "reduce" });
  await page.goto(baseUrl + "/Account/Login");
  await page.getByLabel("Email", { exact: true }).fill(email);
  await page.getByLabel("Mật khẩu", { exact: true }).fill(e2ePassword);
  await page.getByRole("button", { name: "Đăng nhập", exact: true }).click();
  await page.waitForURL(url => url.pathname !== "/Account/Login");
  return page;
}

async function chooseRoom(page, roomCode = room) {
  await page.goto(baseUrl + "/Student/Rooms?Building=C");
  await page.locator(".student-room-card").filter({ has: page.getByRole("heading", { name: "Phòng " + roomCode, exact: true }) })
    .locator("[data-apply-room]").click();
  await page.waitForURL("**/Student/RoomPreview/*");
  return page.locator("[data-room-tour]");
}

async function assertReadableBuildingTab(page, building) {
  const active = page.locator(".building-tabs a.active");
  assert.equal(await active.locator("strong").innerText(), `Tòa ${building}`);
  const contrast = await active.evaluate(tab => {
    const canvas = document.createElement("canvas");
    const ctx = canvas.getContext("2d", { willReadFrequently: true });
    const luminance = color => {
      ctx.fillStyle = color;
      ctx.fillRect(0, 0, 1, 1);
      const [red, green, blue] = Array.from(ctx.getImageData(0, 0, 1, 1).data).slice(0, 3)
        .map(value => value / 255)
        .map(value => value <= 0.04045 ? value / 12.92 : ((value + 0.055) / 1.055) ** 2.4);
      return red * 0.2126 + green * 0.7152 + blue * 0.0722;
    };
    const background = luminance(getComputedStyle(tab).backgroundColor);
    return Array.from(tab.querySelectorAll("strong, small"), label => {
      const foreground = luminance(getComputedStyle(label).color);
      return (Math.max(background, foreground) + 0.05) / (Math.min(background, foreground) + 0.05);
    });
  });
  assert.ok(contrast.every(ratio => ratio >= 4.5), `Tòa ${building} text contrast: ${contrast.map(ratio => ratio.toFixed(2)).join(", ")}`);
}

async function run() {
  const browser = await chromium.launch({
    channel: "msedge", args: ["--use-gl=angle", "--use-angle=swiftshader", "--enable-unsafe-swiftshader"]
  });
  try {
    const staff = await signIn(browser, "admin@nepktx.local");
    await staff.goto(baseUrl + "/Rooms/Map?Building=A");
    await assertReadableBuildingTab(staff, "A");
    await staff.locator(".building-tabs a").filter({ hasText: "Tòa D" }).click();
    await assertReadableBuildingTab(staff, "D");
    const student = await signIn(browser, "student@nepktx.local");
    const artifacts = process.env.DORM_BROWSER_FIXTURE_ARTIFACTS;
    await student.reload();
    await assertRoommates(student, currentRoommates);
    if (artifacts) await student.locator("#roommates").screenshot({ path: path.join(artifacts, "roommates.png") });

    await student.goto(baseUrl + "/Student/Rooms?Building=C");
    const roomList = student.locator(".student-room-list");
    const columns = () => roomList.evaluate(list => getComputedStyle(list).gridTemplateColumns.split(" ").length);
    assert.equal(await columns(), 3);
    await student.locator(".student-room-filters.has-filter-disclosure").waitFor();
    const selectTops = await student.locator(".student-room-filters select").evaluateAll(selects =>
      selects.map(select => select.getBoundingClientRect().top));
    assert.ok(selectTops.length === 4 && Math.max(...selectTops) - Math.min(...selectTops) <= 2);
    const cards = student.locator(".student-room-card");
    const firstCards = await cards.evaluateAll(items => items.slice(0, 4).map(card => card.getBoundingClientRect().top));
    assert.ok(firstCards.length === 4 && firstCards[0] === firstCards[2] && firstCards[3] > firstCards[0]);
    assert.ok(!(await student.locator("main").innerHTML()).includes(destinationRoommate));
    if (artifacts) await student.screenshot({ path: path.join(artifacts, "room-grid-desktop.png") });
    await student.setViewportSize({ width: 820, height: 900 });
    assert.equal(await columns(), 2);
    await student.setViewportSize({ width: 390, height: 844 });
    await student.reload();
    assert.equal(await columns(), 1);
    assert.ok(await student.evaluate(() => document.documentElement.scrollWidth <= window.innerWidth));
    if (artifacts) await student.screenshot({ path: path.join(artifacts, "room-grid-mobile.png") });
    await student.getByRole("link", { name: /Xem bạn cùng phòng/ }).click();
    await student.waitForURL("**/Student/Dashboard#roommates");
    await assertRoommates(student, currentRoommates);
    await student.setViewportSize({ width: 1440, height: 900 });

    await staff.goto(baseUrl + "/Rooms/Catalog");
    await staff.getByRole("link", { name: /^Tòa C/ }).click();
    await staff.getByRole("link", { name: /^Tầng 5/ }).click();
    await staff.getByRole("link", { name: "Ảnh phòng " + room, exact: true }).click();
    const galleryUrl = staff.url();
    const upload = staff.locator("input[name=photo]");
    await upload.setInputFiles({ name: "fake.png", mimeType: "image/png", buffer: Buffer.from("khong-phai-anh") });
    await staff.getByRole("button", { name: "Tải ảnh lên" }).click();
    await staff.getByRole("alert").getByText("Ảnh phải có định dạng JPG, PNG hoặc WebP.").waitFor();
    await upload.setInputFiles({ name: "room.png", mimeType: "image/png", buffer: photo });
    await staff.locator("input[name=caption]").fill(caption);
    await staff.getByRole("button", { name: "Tải ảnh lên" }).click();
    await staff.getByRole("img", { name: caption }).waitFor();

    const sparse = await chooseRoom(student, "C-504");
    await sparse.getByRole("heading", {name:"Ảnh thật · Phòng tham khảo", exact:true}).waitFor();
    const examples = sparse.locator(".room-photo-grid img");
    const samples = await examples.evaluateAll(images => Promise.all(images.map(async image => {
      await image.decode();
      return image.getAttribute("src").split("/").pop();
    })));
    assert.equal(new Set(samples).size, samples.length);
    assert.equal(samples.length, 7);
    for (const file of ["bathroom", "ceiling-fan", "standing-fan", "mini-fridge", "study-desk"])
      assert.ok(samples.includes(file + ".webp"), file);
    for (const file of ["air-conditioner", "personal-lockers", "bunk-drawers"])
      assert.ok(!samples.includes(file + ".webp"), file);
    assert.equal(await sparse.getByRole("link", { name: "Nguồn ảnh tham khảo" }).count(), 0);
    await sparse.locator("[data-tour-stage] canvas").waitFor({ timeout: 12000 });
    assert.deepEqual(new Set((await sparse.locator("[data-tour-stage]").getAttribute("data-modeled-amenities")).split(",")),
      new Set(["bath", "desk", "standingFan", "ceilingFan", "fridge"]));
    assert.deepEqual(await sparse.locator("[data-room-amenities] li").allTextContents(),
      ["WC khép kín", "Bàn học", "Quạt trần", "Quạt đứng", "Tủ lạnh", "Kệ bên máy lạnh"]);
    await sparse.locator("[data-tour-stage] .room-tour-marker:visible").first().waitFor();
    if (artifacts) await student.screenshot({ path: path.join(artifacts, "room-tour-fans-and-fridge.png") });
    await staff.goto(baseUrl + "/Rooms/Catalog");
    await staff.getByRole("link", { name: /^Tòa C/ }).click();
    await staff.getByRole("link", { name: /^Tầng 5/ }).click();
    const row504 = staff.locator("tbody tr").filter({ hasText: "C-504" });
    await row504.getByRole("button", { name: "Sửa", exact: true }).click();
    const editor = staff.locator("dialog[open]");
    await editor.getByText("Kệ bên máy lạnh", { exact: false }).waitFor();
    const options = editor.getByRole("checkbox");
    assert.equal(await options.count(), 9);
    for (const option of await options.all()) await option.check();
    await editor.getByRole("button", { name: "Lưu phòng", exact: true }).click();
    await row504.getByText(/Ban công.*Kệ bên máy lạnh/).waitFor();
    await student.reload();
    await sparse.locator("[data-tour-stage] canvas").waitFor();
    assert.deepEqual(new Set((await sparse.locator("[data-tour-stage]").getAttribute("data-modeled-amenities")).split(",")),
      new Set(["bath", "wifi", "air", "standingFan", "ceilingFan", "desk", "locker", "fridge", "heater", "balcony"]));
    const preview = await chooseRoom(student);
    await preview.getByRole("heading", {name:"Ảnh thật của phòng " + room, exact:true}).waitFor();
    assert.equal(await preview.getByText("Nguồn ảnh tham khảo").count(), 0);
    const image = preview.getByRole("img", { name: caption });
    await image.waitFor();
    await student.waitForFunction(() => document.querySelector(".room-tour-facts img")?.naturalWidth > 0);
    const imageUrl = new URL(await image.getAttribute("src"), baseUrl).href;
    const anonymous = await browser.newContext();
    const blocked = await anonymous.request.get(imageUrl, { maxRedirects: 0 });
    assert.equal(blocked.status(), 302);
    await anonymous.close();
    const cannotManage = await student.request.get(galleryUrl, { maxRedirects: 0 });
    assert.ok([302, 403].includes(cannotManage.status()));

    const select = preview.locator("select[name=TargetBedNumber]");
    const choices = await select.locator("option[value]:not([value=''])").evaluateAll(options => options.map(option => option.value));
    assert.ok(choices.length >= 2);
    const bed = choices.at(-1);
    const label = bed.padStart(2, "0");
    const stage = preview.locator("[data-tour-stage]");
    await stage.locator("canvas").waitFor({ timeout: 12000 });
    assert.deepEqual(new Set((await stage.getAttribute("data-modeled-amenities")).split(",")),
      new Set(["bath", "wifi", "air", "desk", "locker"]));
    const occupiedMarker = stage.locator('button[data-bed="2"]');
    await occupiedMarker.waitFor();
    assert.equal(await occupiedMarker.isDisabled(), true);
    assert.ok(!choices.includes("2"));
    const occupiedBounds = await occupiedMarker.boundingBox();
    await student.mouse.click(occupiedBounds.x + occupiedBounds.width / 2, occupiedBounds.y + occupiedBounds.height / 2);
    await student.mouse.click(occupiedBounds.x + occupiedBounds.width / 2 - 35, occupiedBounds.y + occupiedBounds.height / 2 + 4);
    assert.equal(await stage.getAttribute("data-focused-bed"), "0");
    assert.equal(await select.inputValue(), "");
    if (artifacts) await student.screenshot({ path: path.join(artifacts, "room-tour-overview.png") });
    const planMode = preview.getByRole("button", { name: "Mặt bằng", exact: true });
    await planMode.click();
    await preview.locator("[data-tour-stage].is-sliding").waitFor({ state: "detached" });
    assert.equal(await stage.getAttribute("data-tour-mode"), "plan");
    assert.equal(await planMode.getAttribute("aria-pressed"), "true");
    for (const landmark of ["Cửa sổ", "Cửa vào", "WC"])
      await stage.locator(".room-tour-landmark:visible").getByText(landmark, { exact: true }).waitFor();
    assert.equal(await occupiedMarker.isDisabled(), true);
    if (artifacts) await student.screenshot({ path: path.join(artifacts, "room-tour-floorplan.png") });
    const marker = stage.locator(`button[data-bed="${bed}"]`);
    await marker.waitFor({ timeout: 8000 });
    await marker.click({ timeout: 8000 });
    assert.equal(await stage.getAttribute("data-focused-bed"), bed);
    assert.equal(await select.inputValue(), bed);
    assert.match(await stage.getAttribute("class"), /is-sliding/, "The camera should glide by default");
    await preview.locator("[data-tour-stage].is-sliding").waitFor({ state: "detached" });
    await preview.locator("[data-tour-selection]").getByText("Giường " + label, { exact: false }).waitFor();
    if (artifacts) await student.screenshot({ path: path.join(artifacts, "room-tour-bed.png") });
    const overview = stage.getByRole("button", { name: "Về toàn cảnh", exact: true });
    await overview.click();
    assert.equal(await stage.getAttribute("data-focused-bed"), "0");
    await preview.locator("[data-tour-stage].is-sliding").waitFor({ state: "detached" });
    assert.equal(await stage.getAttribute("data-tour-mode"), "overview");
    if (artifacts) await student.screenshot({ path: path.join(artifacts, "student-room-desktop.png") });
    await student.emulateMedia({ reducedMotion: "no-preference" });
    // Click the visible mattress beside the number marker to exercise canvas picking.
    const markerBounds = await marker.boundingBox();
    const startPoint = await marker.evaluate(node => ({ x: parseFloat(node.style.left), y: parseFloat(node.style.top) }));
    await student.mouse.click(markerBounds.x + markerBounds.width / 2 + 28, markerBounds.y + markerBounds.height / 2 + 1);
    assert.equal(await stage.getAttribute("data-focused-bed"), bed);
    assert.match(await stage.getAttribute("class"), /is-sliding/);
    const canvasBounds = await stage.locator("canvas").boundingBox();
    await student.mouse.click(canvasBounds.x + 15, canvasBounds.y + 15);
    assert.match(await stage.getAttribute("class"), /is-sliding/, "A tap without dragging should not stop the camera");
    await student.waitForFunction(({ x, y, bed }) => {
      const label = document.querySelector(`.is-sliding [data-bed="${bed}"]`);
      return label && Math.hypot(parseFloat(label.style.left) - x, parseFloat(label.style.top) - y) > 18;
    }, { ...startPoint, bed }, { timeout: 4000 });
    await preview.locator("[data-tour-stage].is-sliding").waitFor({ state: "detached" });
    if (artifacts) await student.screenshot({ path: path.join(artifacts, "room-tour-bed.png") });
    await overview.click();
    assert.match(await stage.getAttribute("class"), /is-sliding/, "Returning to the overview should glide as well");
    await preview.locator("[data-tour-stage].is-sliding").waitFor({ state: "detached" });
    assert.equal(await stage.getAttribute("data-focused-bed"), "0");
    assert.equal(await select.inputValue(), bed);
    await occupiedMarker.waitFor();
    await student.emulateMedia({ reducedMotion: "reduce" });
    for (const width of [590, 360, 390]) {
      await student.setViewportSize({ width, height: 844 });
      assert.ok(await student.evaluate(() => document.documentElement.scrollWidth <= innerWidth));
      await stage.evaluate(async element => {
        await new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
        const markers = [...element.querySelectorAll(".room-tour-marker:not([hidden])")];
        if (markers.length < 2) throw new Error("Expected multiple visible bed markers in the overview.");
        const bounds = markers.map(marker => marker.getBoundingClientRect());
        for (let i = 0; i < bounds.length; i++) {
          for (let j = i + 1; j < bounds.length; j++) {
            const a = bounds[i], b = bounds[j];
            if (Math.hypot(a.x - b.x, a.y - b.y) < a.width - 1)
              throw new Error(`Bed markers ${markers[i].dataset.bed} and ${markers[j].dataset.bed} overlap in the ${element.clientWidth}px stage: ${JSON.stringify([a, b])}.`);
          }
        }
      });
    }
    if (artifacts) await student.screenshot({ path: path.join(artifacts, "student-room-mobile.png") });
    assert.equal(await preview.getByRole("checkbox", { name: "Lướt camera" }).count(), 0);
    await planMode.click();
    await preview.locator("[data-tour-stage].is-sliding").waitFor({ state: "detached" });
    await stage.locator(".room-tour-landmark:visible").filter({ hasText: "Cửa vào" }).waitFor();
    if (artifacts) await student.screenshot({ path: path.join(artifacts, "room-tour-floorplan-mobile.png") });
    await select.selectOption(choices[0]);
    assert.equal(await stage.getAttribute("data-focused-bed"), choices[0]);
    assert.match(await stage.getAttribute("class"), /is-sliding/);
    await overview.click();
    await preview.locator("[data-tour-stage].is-sliding").waitFor({ state: "detached" });
    assert.equal(await stage.getAttribute("data-focused-bed"), "0");

    // The normal form remains functional if WebGL2 is unavailable.
    await student.addInitScript(() => {
      const getContext = HTMLCanvasElement.prototype.getContext;
      HTMLCanvasElement.prototype.getContext = function (type, ...args) {
        return type === "webgl2" ? null : getContext.call(this, type, ...args);
      };
    });
    await student.reload();
    await preview.getByText(/Thiết bị không hiển thị được 3D/).waitFor();
    await select.selectOption(bed);
    await preview.locator("textarea").fill("Em cần chuyển sang phòng đã xem để thuận tiện lịch học.");
    await preview.getByRole("button", { name: /^Gửi đơn/ }).click();
    await student.waitForURL("**/Student/Applications");
    const card = student.locator(".application-card.current");
    await card.getByText("Giường " + label, { exact: true }).waitFor();
    const code = (await card.innerText()).match(/DK-\d{4}-\d{4,}/)?.[0];
    assert.ok(code);

    await staff.goto(baseUrl + "/Applications?Search=" + code);
    await staff.locator(".placement-proof").getByText("Giường đã chọn " + label, { exact: true }).waitFor();
    await staff.getByRole("button", { name: "Nhận xử lý đơn" }).click();
    staff.once("dialog", alert => alert.accept());
    await staff.getByRole("button", { name: "Duyệt và giữ chỗ" }).click();
    await staff.locator(".case-header .request-state").getByText("Chờ nhận phòng", { exact: true }).waitFor();
    await student.goto(baseUrl + "/Student/Dashboard");
    await assertRoommates(student, currentRoommates);

    await staff.goto(baseUrl + "/Students?Search=SV000001");
    await staff.getByRole("button", { name: "Cấp tài khoản", exact: true }).click();
    const newEmail = "rooms-bed-check@student.nepktx.local";
    const provision = staff.locator("#create-account-dialog");
    await provision.locator("[name=Email]").fill(newEmail);
    await provision.locator("[name=TemporaryPassword]").fill(e2ePassword);
    await provision.getByRole("button", { name: "Cấp tài khoản", exact: true }).click();
    await staff.getByText(newEmail, { exact: true }).first().waitFor();
    const other = await signIn(browser, newEmail);
    const otherDialog = await chooseRoom(other);
    const otherBeds = otherDialog.locator("select[name=TargetBedNumber] option");
    assert.ok(await otherBeds.count() >= 1);
    assert.equal(await otherBeds.filter({ hasText: new RegExp("^Giường " + label + "$") }).count(), 0);

    await staff.goto(baseUrl + "/Applications?Search=" + code);
    staff.once("dialog", alert => alert.accept());
    await staff.getByRole("button", { name: "Xác nhận nhận phòng" }).click();
    await staff.locator(".case-header .request-state").getByText("Đã duyệt", { exact: true }).waitFor();
    await student.goto(baseUrl + "/Student/Dashboard");
    assert.match(await student.locator(".room-pass-code").innerText(), new RegExp(room));
    assert.equal(await student.locator(".room-pass").getByText(label, { exact: true }).count(), 1);
    await assertRoommates(student, [destinationRoommate]);

    await staff.goto(galleryUrl);
    staff.once("dialog", alert => alert.accept());
    await staff.getByRole("button", { name: "Gỡ ảnh" }).click();
    await staff.getByText(/Phòng này chưa có ảnh thực tế/).waitFor();
    assert.equal((await student.request.get(imageUrl)).status(), 404);
    console.log("Room preview passed: furniture follows the room, occupied beds stay disabled, camera glides, and booking works without WebGL.");
  } finally {
    await browser.close();
  }
}

Promise.resolve().then(addFixtureRoommates).then(run).catch(error => { console.error(error.message); process.exitCode = 1; });
