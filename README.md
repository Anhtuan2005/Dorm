# Dormitory Management System — NẾP KTX

ASP.NET Core MVC dormitory-management project using Razor Views, handwritten CSS/JavaScript, SQL Server, ASP.NET Core Identity, EF Core, Serilog and SignalR infrastructure.

The current implementation contains the approved Option 2 manager room map, operations dashboard, editable building/floor/room catalog, period/building reports with CSV export, student portal, application review/account management, billing ledger with period issuance, and the maintenance response desk with protected image evidence. Student room search, housing requests, move-in confirmation, invoice lookup, payment recording/reversal, repair requests, maintenance dispatch, notifications and account links use SQL Server data. The AI assistant is a clearly labelled UI/demo response layer only; model/API integration remains a later extension.

## Run locally

The active workspace is `D:\DormPrj\Dorm-master`. Run commands from this directory; the HTTP launch profile serves `http://localhost:5001`.

Requirements: .NET 8 SDK/runtime and SQL Server instance `DESKTOP-OSSBOT1\TUANANH` with Windows authentication.

```powershell
dotnet restore DormitoryManagementSystem.sln
dotnet ef database update --project DormitoryManagementSystem --startup-project DormitoryManagementSystem
dotnet test DormitoryManagementSystem.sln -c Release
dotnet run --project DormitoryManagementSystem
```

The default test command runs policy tests and skips SQL integration cases when no test administrator connection is configured. To run the five real SQL Server integration tests against disposable databases:

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
- Invoice totals and line items are database-backed; online payment remains explicitly out of scope.
- AI help includes grounded demo answers, source labels, known-function links and a no-information fallback. No API key or external model call is present.

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

This completes the agreed core management scope. Real AI/document retrieval, online payment and realtime push clients remain deliberate extensions; the current chatbot uses clearly marked demo knowledge and SignalR remains infrastructure-only.

Generated design previews, screenshots, logs, build outputs and working documents are intentionally ignored by Git. README files and the interface-system contract remain versioned. Keep production secrets in environment variables or .NET User Secrets, never in tracked settings.

The architecture follows the controller-service-view-model separation and startup conventions verified against the user-provided reference repository `Anhtuan2005/E-Lectrical--Commerce-`.
