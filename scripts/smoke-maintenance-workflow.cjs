const { chromium } = require("playwright");
const { spawnSync } = require("child_process");
const { baseUrl, e2ePassword } = require("./e2e-config.cjs");

const marker = "[E2E] Giai phong cho tam khoa";
const sqlcmd = "C:\\Program Files\\Microsoft SQL Server\\Client SDK\\ODBC\\170\\Tools\\Binn\\SQLCMD.EXE";
const server = "DESKTOP-OSSBOT1\\TUANANH";

function runSql(sql) {
  const settings = "SET ANSI_NULLS ON; SET QUOTED_IDENTIFIER ON; SET ANSI_PADDING ON; SET ANSI_WARNINGS ON; SET CONCAT_NULL_YIELDS_NULL ON; SET ARITHABORT ON; SET NUMERIC_ROUNDABORT OFF;";
  const result = spawnSync(sqlcmd, ["-S", server, "-d", "DormitoryManagementSystem", "-E", "-b", "-Q", `${settings} ${sql}`], { encoding: "utf8" });
  if (result.status !== 0) throw new Error(`SQL failed: ${result.stderr || result.stdout}`);
  return result.stdout;
}

function cleanup() {
  runSql(`
SET NOCOUNT ON;
DECLARE @RequestId int = (SELECT Id FROM MaintenanceRequests WHERE Code=N'SC-0284');
DELETE FROM Notifications WHERE CHARINDEX(N'${marker}', Body) > 0;
DELETE FROM MaintenanceUpdates WHERE MaintenanceRequestId=@RequestId AND CHARINDEX(N'${marker}', Note) > 0;
UPDATE MaintenanceRequests SET Status=N'New', Priority=N'Urgent', AssignedTeam=NULL, BlockedBeds=8,
UpdatedAt=DATEADD(hour,-18,SYSUTCDATETIME()) WHERE Id=@RequestId;`);
}

async function signIn(page) {
  await page.goto(`${baseUrl}/Account/Login`, { waitUntil: "networkidle" });
  await page.getByLabel("Email").fill("manager@nepktx.local");
  await page.getByLabel("Mật khẩu").fill(e2ePassword);
  await Promise.all([
    page.waitForNavigation({ waitUntil: "networkidle" }),
    page.getByRole("button", { name: "Đăng nhập" }).click()
  ]);
}

(async () => {
  cleanup();
  const browser = await chromium.launch({ executablePath: "C:\\Program Files (x86)\\Microsoft\\Edge\\Application\\msedge.exe", headless: true });
  const issues = [];
  try {
    const context = await browser.newContext({ viewport: { width: 1440, height: 950 } });
    const page = await context.newPage();
    await signIn(page);
    await page.goto(`${baseUrl}/Maintenance/Index?Search=SC-0284`, { waitUntil: "networkidle" });

    const before = await page.locator(".capacity-impact").innerText();
    if (!before.includes("Tạm khóa\n8") || !before.includes("Có thể xếp\n0")) issues.push(`unexpected opening capacity: ${before}`);

    await page.locator('.maintenance-action-form select[name="Status"]').selectOption("Resolved");
    await page.locator('.maintenance-action-form textarea[name="Note"]').fill(marker);
    const stalePayload = await page.locator(".maintenance-action-form").evaluate(form => Object.fromEntries(new FormData(form)));
    await Promise.all([
      page.waitForNavigation({ waitUntil: "networkidle" }),
      page.getByRole("button", { name: "Lưu cập nhật" }).click()
    ]);

    const after = await page.locator(".capacity-impact").innerText();
    if (!after.includes("Tạm khóa\n0") || !after.includes("Có thể xếp\n8")) issues.push(`resolved incident did not release capacity: ${after}`);
    const detail = await page.locator(".maintenance-detail").innerText();
    if (!detail.includes("Đã hoàn thành") || !detail.includes(marker)) issues.push("maintenance audit update is incomplete");

    await page.request.post(`${baseUrl}/Maintenance/Update`, { form: stalePayload });
    const updateCount = runSql(`SET NOCOUNT ON; SELECT COUNT(*) FROM MaintenanceUpdates WHERE MaintenanceRequestId=(SELECT Id FROM MaintenanceRequests WHERE Code=N'SC-0284') AND CHARINDEX(N'${marker}', Note)>0;`).trim();
    if (!updateCount.endsWith("1")) issues.push(`stale form was not rejected: ${updateCount}`);

    await page.goto(`${baseUrl}/Rooms/Map?Building=A&Room=A-301`, { waitUntil: "networkidle" });
    const roomDetail = await page.locator(".room-detail").innerText();
    if (!roomDetail.includes("Còn 8 chỗ") || !roomDetail.includes("Có thể xếp\n8")) issues.push(`room map did not share released capacity: ${roomDetail}`);

    await context.close();
    process.stdout.write(JSON.stringify({ issues }, null, 2));
    if (issues.length) process.exitCode = 1;
  } finally {
    await browser.close();
    cleanup();
  }
})().catch(error => {
  process.stderr.write(error.stack);
  process.exitCode = 1;
});
