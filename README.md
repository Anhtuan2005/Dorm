# Dormitory Management System — NẾP KTX

ASP.NET Core MVC dormitory-management project using Razor Views, handwritten CSS/JavaScript, SQL Server, ASP.NET Core Identity, EF Core, Serilog and SignalR infrastructure.

The current implementation contains the approved Option 2 manager room map, operations dashboard, editable building/floor/room catalog, period/building reports with CSV export, student portal, application review/account management, billing ledger with period issuance, and the maintenance response desk with protected image evidence. Student room search, housing requests, move-in confirmation, invoice lookup, payment recording/reversal, repair requests, maintenance dispatch, notifications and account links use SQL Server data. The student AI assistant calls Google Gemini through an authenticated backend endpoint and guides users through implemented workflows.

## Run locally

The active workspace is `D:\DormPrj\Dorm-master`. Run commands from this directory; the HTTP launch profile serves `http://localhost:5001`.

Requirements: .NET 8 SDK/runtime and SQL Server instance `DESKTOP-OSSBOT1\TUANANH` with Windows authentication.

```powershell
dotnet restore DormitoryManagementSystem.sln
dotnet ef database update --project DormitoryManagementSystem --startup-project DormitoryManagementSystem
dotnet test DormitoryManagementSystem.sln -c Release
dotnet run --project DormitoryManagementSystem
```

The default test command runs policy tests and skips SQL integration cases when no test administrator connection is configured. To run the real SQL Server integration tests against disposable databases:

```powershell
$env:DORM_TEST_SQL_ADMIN_CONNECTION = "Server=DESKTOP-OSSBOT1\TUANANH;Database=master;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true"
dotnet test DormitoryManagementSystem.sln -c Release
```

Integration databases use the guarded `DormitoryManagementSystem_Test_<random>` name and are dropped after each test. Demo seeding is never run for them.

Development runs migrations at startup. Demo seeding is opt-in and its password must stay in .NET User Secrets:

```powershell
dotnet user-secrets set "DemoData:Seed" "true" --project DormitoryManagementSystem
dotnet user-secrets set "DemoData:Password" "<your-local-password>" --project DormitoryManagementSystem
```

The seed creates `manager@nepktx.local`, `admin@nepktx.local`, and `student@nepktx.local`. Never place the demo password in source control. Production migrations run only when `Database:ApplyMigrationsOnStartup=true`; otherwise deploy migrations separately.

Browser checks read the same local password from the process environment:

```powershell
npm ci
$env:DORM_E2E_PASSWORD = "<your-local-password>"
npm run audit
```

Browser tools use project-local Playwright and Sharp packages. Checks default to `http://127.0.0.1:5001`; set `DORM_E2E_BASE_URL` to check a different running instance.

The student room preview uses a shared Three.js room model that adapts to the room's numbered beds and supported catalogue amenities. New amenity names can be mapped in `frontend/dorm-room-model.js`. The form works without WebGL. The generated browser bundle is checked in so the .NET app can run without Node.js. After editing the model or viewer under `frontend/`, run `npm ci` and `npm run build:room-preview`.

Every room includes an en-suite WC. Demo seeding assigns different optional amenities across rooms. An existing demo database can refresh rooms that still match the original fixture with `--DemoData:RefreshRoomAmenities=true` in Development.

## VNPAY Sandbox

The student invoice page can open VNPAY Sandbox for the current outstanding balance. Use a demo database or a test deployment. The feature is disabled by default and cannot redirect to a live payment gateway.

From this workspace, configure a valid merchant in .NET User Secrets before starting the app in Development:

```powershell
dotnet user-secrets set "Vnpay:MerchantCode" "<sandbox website code>" --project DormitoryManagementSystem
$sandboxSecret = Read-Host "Active VNPAY Sandbox signing secret" -MaskInput
dotnet user-secrets set "Vnpay:HashSecret" $sandboxSecret --project DormitoryManagementSystem
Remove-Variable sandboxSecret
dotnet user-secrets set "Vnpay:PublicBaseUrl" "https://<sandbox-app-host>" --project DormitoryManagementSystem
dotnet user-secrets set "Vnpay:Enabled" "true" --project DormitoryManagementSystem
```

The app generates the browser return URL `/payments/vnpay/return`. Register `https://<sandbox-app-host>/payments/vnpay/ipn` as the IPN endpoint with VNPAY for the selected merchant. It must be reachable by VNPAY over HTTPS. An HTTP loopback base URL can be used for local callback simulation only. See the [VNPAY integration guide](https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html).

Only a verified IPN can add a receipt. Browser returns show the stored status and can be refreshed. Pending sessions expire after 15 minutes; a verified late or ambiguous result that cannot be applied safely is shown under **Thu phí → VNPAY Sandbox · Cần đối soát**. The app does not send refunds to VNPAY.

The automated browser check skips the real gateway and runs with a disposable database. After a Release build and `npm ci`, run `pwsh -NoProfile -File .scratch/vnpay-sandbox/verify.ps1` on the configured SQL Server machine.

## Realtime notifications

SignalR keeps signed-in browser tabs synchronized without external credentials. Students see their unread count, inbox, and recent notifications update. Managers see the attention bell and sidebar counts update from the same rules used for the initial render.

The authenticated hub sends a change signal after the SQL write, not the notification content. Each browser fetches its own latest state. On reconnect or return to the tab it catches up from SQL Server. The browser client is bundled locally under `wwwroot/vendor/signalr` (Microsoft SignalR 8.0.29, MIT); no CDN or Node build is required to run the app.

Run `pwsh -NoProfile -File .scratch/vnpay-sandbox/verify.ps1 -Scenario Realtime` after the Release build and `npm ci` to verify role/recipient isolation, open tabs, and network recovery against a disposable SQL Server database.

## Student help and Gemini

The student help dialog posts to `/Chatbot/Ask`. Its server-side HTTP client uses the [Gemini generateContent API](https://ai.google.dev/api/generate-content), with `gemini-3.5-flash-lite` configured under `Chatbot:Model`. Configure the key outside source control:

```powershell
dotnet user-secrets set "Chatbot:ApiKey" "<your-gemini-api-key>" --project DormitoryManagementSystem
```

For deployment, set the environment variable `Chatbot__ApiKey`. The browser receives no API key. Restart the application after initial configuration.

Chat is restricted to the Student role, requires an antiforgery token and allows eight requests per account per minute. Questions are limited to 1,000 characters. Supported workflow questions are mapped to predefined prompts before reaching Gemini, with a 30-second provider timeout. The model receives neither the conversation nor account records. Questions about personal balances open the signed-in student's invoice page.

The document lookup needs no API key. It matches a question or configured alias, ignoring Vietnamese accents, capitalization and punctuation. The workflow router also recognizes phrases within naturally worded questions and keeps the source when a matching excerpt exists. Room-availability answers show up to three rooms with assignable beds from the authenticated student's Tìm phòng results. A short follow-up can name a building. Unclear questions prompt for clarification. If the assistant lacks a source for policy or fee information, it links to the Ban quản lý contact section. The Development environment also shows labeled examples from `Knowledge/student-help-demo.json`. Production citations use the approved catalog.

Publish confirmed contact details with `StudentSupport:Office`, `StudentSupport:Phone`, and `StudentSupport:Email` in server configuration (or `StudentSupport__Office`, `StudentSupport__Phone`, and `StudentSupport__Email` in the environment). The assistant and `/Home/Privacy#contact` use those same public details. When no phone or email is configured, they direct students to the KTX office.

After Ban quản lý verifies an excerpt, add it to `App_Data/chatbot-knowledge.json` in the application's content root and restart. The catalog lives outside `wwwroot` and is excluded from Git and publish artifacts. Include the current dorm rules, housing eligibility, published fees and deadlines, procedures for registration, transfer and checkout, repair guidance, and the management contact page. Each excerpt has its own question aliases and source reference:

```json
[
  {
    "questions": ["Nội quy KTX", "Xem nội quy ở đâu?"],
    "answer": "<Trích đoạn đã được Ban quản lý xác nhận>",
    "document": "<Tên văn bản và phiên bản>",
    "section": "<Mục được trích>",
    "approvalReference": "<Số văn bản hoặc mã xác nhận nội bộ>",
    "url": "https://example.invalid/noi-quy#mu-c",
    "action": "rooms"
  }
]
```

Use distinct aliases for each excerpt. `url` and `action` are optional. Use a public HTTPS URL if available. The supported actions are `rooms`, `applications`, `invoices`, `repairs`, `notifications` and `password`.

## Workspace UX

The manager and student interfaces share `wwwroot/css/workspace.css` and small browser-native enhancements in `wwwroot/js/workspace.js`. The overview prioritizes actionable work; application, billing and maintenance screens keep actions inside the selected record. Mobile worklists and advanced filters disclose on demand. Keyboard navigation, persistent validation feedback, optional notes and antiforgery-protected forms remain available across both portals.

Visual direction and component rules are recorded in `.interface-design/system.md`. No UI framework or database migration is required for this refinement.

## Current verified data

| Scope | Capacity | Occupied | Held | Assignable | Blocked |
|---|---:|---:|---:|---:|---:|
| Building A | 320 | 304 | 4 | 4 | 8 |
| Building B | 320 | 282 | 8 | 26 | 4 |
| Building C | 320 | 258 | 7 | 51 | 4 |
| Building D | 320 | 298 | 5 | 13 | 4 |
| Total | 1,280 | 1,142 | 24 | 94 | 20 |

`Assignable = Capacity - Occupied - Held - Blocked`. Maintenance-blocked beds are never counted as assignable.

## Structure

- `Controllers/`: MVC entry points and authorization boundaries.
- `DormitoryManagementSystem.Tests/`: deterministic domain and workflow policy tests.
- `DormitoryManagementSystem.IntegrationTests/`: guarded SQL Server transaction, constraint and concurrency tests.
- `Services/`: room-map use cases and view-model projection.
- `Data/`: EF Core context, migrations and deterministic demo seed.
- `Models/`: Identity and dormitory domain entities.
- `Views/`: server-rendered Razor UI.
- `wwwroot/css` and `wwwroot/js`: custom styling and browser-native behavior.
- `scripts/capture-room-map.cjs`: repeatable desktop/mobile browser capture and smoke checks.
- `scripts/capture-student-portal.cjs`: visual and authorization regression checks for the student portal.
- `scripts/smoke-student-workflows.cjs`: submit/cancel housing request, invoice read and repair-submit browser tests with deterministic cleanup.
- `scripts/capture-application-workflow.cjs`: desktop/mobile visual regression for the manager review desk and student directory.
- `scripts/smoke-application-transaction.cjs`: end-to-end submit, approve, assign, notify and deterministic rollback verification.
- `scripts/smoke-housing-request-lifecycle.cjs`: two-role handoff, student supplementation, checkout approval and deterministic rollback verification.
- `scripts/capture-housing-request-lifecycle.cjs`: desktop/mobile visual regression for the application handoff states.
- `scripts/smoke-student-account-lifecycle.cjs`: account creation, password reset, lock/unlock and role-protected login verification.
- `scripts/capture-billing-workflow.cjs`: desktop/mobile billing-ledger and student-invoice visual regression.
- `scripts/smoke-billing-ledger.cjs`: payment, idempotent replay, overpayment rejection, reversal and deterministic cleanup verification.
- `scripts/capture-maintenance-workflow.cjs`: desktop/mobile maintenance-desk visual and authorization regression.
- `scripts/smoke-maintenance-workflow.cjs`: resolve, capacity release, stale-form rejection, room-map propagation and deterministic cleanup verification.
- `scripts/audit-final-system.cjs`: desktop/mobile route crawl, authorization, antiforgery, dead-link, overflow, CSV and browser-console regression audit.
- `scripts/smoke-core-completions.cjs`: building/floor/room and student-profile maintenance, invoice issuance, protected repair-image upload/read and cleanup verification.

## Student portal checkpoint

- Separate role-protected student layout, responsive dashboard and mobile bottom navigation.
- Search only returns assignable rooms compatible with the student's profile.
- Housing-request and repair forms use antiforgery validation and derive the student/room identity from the signed-in account.
- Invoice totals and line items are database-backed. VNPAY Sandbox is an optional test-only checkout for outstanding invoices.
- AI help calls Gemini from the backend, follows implemented workflow guidance and clearly identifies AI responses. The key stays in User Secrets or deployment environment variables; unknown regulations and personal-data queries defer to management or the relevant portal page.

## Manager application and account checkpoint

- The application queue prioritizes overdue work and keeps the selected case, room-capacity proof and final decision visible in one review desk.
- Approval rechecks gender policy, current stay, holds, maintenance-blocked beds and live occupancy inside a serializable SQL transaction, then reserves one bed. A separate manager confirmation creates the new stay only when physical handover occurs.
- Each transition writes an audit event and a student notification. Stale Razor forms are rejected through the row-version concurrency token.
- The student directory can link an Identity account to an existing profile, reset its password and lock or unlock access without exposing password data.
- `Admin` and `DormManager` can access manager workflows; `Student` remains restricted to the student portal.

## Manager billing checkpoint

- The worklist prioritizes overdue invoices and supports search plus period, status, building and balance-range filters without losing context when opening a case.
- `BillingLedgerService` is the single seam for derived payment state and outstanding balance used by manager billing, student invoices, application review and the student directory.
- Payments are append-only records with database-issued receipt codes, idempotency keys, manager audit data and optimistic concurrency. Mistakes are reversed by a linked negative adjustment rather than editing history.
- Recording and reversal run inside a serializable SQL transaction through EF Core's retrying execution strategy. Overpayment, stale forms, invalid future timestamps and missing bank references fail safely.
- The `AddBillingLedger` migration backfills legacy paid totals before removing the old mutable field. Demo data currently contains 10 invoices and 4 baseline ledger transactions.
- Six policy unit tests cover payment-state boundaries; browser smoke tests cover payment, replay, overpayment, reversal, student authorization and the prior room/application/account workflows.

## Manager maintenance checkpoint

- The response desk prioritizes urgent, unassigned and SLA-breached incidents in a database-filtered worklist; selected room evidence and the dispatch form remain visible together.
- Managers can assign a team, change priority/progress, document work and set the number of temporarily blocked beds. Every update is appended to the incident timeline and notifies the reporting student when one exists.
- State transitions run in a serializable transaction and reject stale row versions. Resolving or closing an incident releases its blocked beds instead of leaving unusable capacity behind.
- `RoomCapacityPolicy` is the shared invariant for room map, student room search and application approval: `Assignable = Capacity - Occupied - Held - Blocked`.
- The `AddMaintenanceWorkflow` migration adds assignee, actor audit and optimistic-concurrency data. Eight unit tests now cover billing boundaries and room-capacity clamping; browser smoke tests verify the cross-module capacity release.

## Housing-request lifecycle checkpoint

- `HousingRequestWorkflow` centralizes valid transitions, Vietnamese status presentation, the current `Lượt xử lý`, next action and the 24-hour manager SLA.
- Submitted requests must be received before approval. Missing-information requests hand responsibility to the student; a supplement appends an audit event and hands the request back to management without counting student waiting time against the manager SLA.
- Registration works for students without a current stay. Transfer and checkout pin the exact source stay, while approved registration/transfer stays retain provenance to their source request.
- Checkout is a first-class path: the student submits a reason, management verifies and approves the source stay, and the portal switches safely to the no-current-room state.
- Queue filtering, priority ordering and pagination execute in SQL. Request numbers come from a SQL sequence, and both manager and student forms reject stale row versions.
- The `DeepenHousingRequestLifecycle` migration backfills action clocks, source stays, approved-stay provenance and missing audit history for existing data. Twenty-one policy tests and repeatable two-role browser smoke tests cover the delivered paths.

## Final core checkpoint: reporting and hardening

- The manager landing page is a database-backed operational snapshot centered on capacity, current receivables and work that needs intervention; manager login now lands here.
- Reports can be filtered by billing period and building, compare current safe capacity with period finance/workflow/maintenance outcomes, and export a UTF-8 CSV protected against spreadsheet-formula injection.
- Room-map attention counters, student debt links, room-student history links and global search now route to real filtered workflows instead of placeholders.
- Settings presents the connected database/migration status, operating scope, domain safety rules and role boundaries without exposing server secrets.
- The final browser audit covers 27 manager/student route passes at 1,586px and 430px, authorization boundaries, antiforgery tokens, dead links, horizontal overflow, CSV access and browser/runtime errors.
- Thirty-seven policy tests and five SQL Server integration tests pass; EF Core reports no pending model changes, NuGet reports no known vulnerable packages, and database invariant queries report zero duplicate active stays/requests, invalid move-in holds or room-capacity overflow.

## Completion additions

- Managers can create and update buildings, floors and rooms. Historical rooms cannot be deleted, and capacity cannot be reduced below occupied, held and maintenance-blocked beds.
- Billing periods can issue one immutable invoice per eligible stay with room, electricity, water and service lines; rerunning the same scope skips duplicates.
- Registration and transfer approvals enter `Chờ nhận phòng`, reserve capacity and require explicit handover confirmation before changing the active stay.
- `HardenCapacityConstraints` adds database guards for positive room/hold capacity, non-negative maintenance blocks and chronological stays; serializable service transactions continue to enforce the cross-row capacity invariant.
- Students can upload up to three server-validated JPG/PNG/WebP evidence files per repair request. Files live outside `wwwroot` and are returned only through role/owner-authorized endpoints.
- Student notifications have unread counts, mark-one and mark-all-read flows. Every authenticated user can change their own password without exposing credentials in source.

This completes the agreed core management scope. The student chatbot calls Gemini for workflow guidance, SignalR updates notifications in open tabs, and VNPAY checkout is available for Sandbox testing. Document retrieval, personal-data tools, live payments, refunds and background device notifications remain separate work.

Generated design previews, screenshots, logs, build outputs and working documents are intentionally ignored by Git. README files and the interface-system contract remain versioned. Keep production secrets in environment variables or .NET User Secrets, never in tracked settings.

The architecture follows the controller-service-view-model separation and startup conventions verified against the user-provided reference repository `Anhtuan2005/E-Lectrical--Commerce-`.
