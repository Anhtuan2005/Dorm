# Dormitory Management System — NẾP KTX

ASP.NET Core MVC dormitory-management project using Razor Views, handwritten CSS/JavaScript, SQL Server, ASP.NET Core Identity, EF Core, Serilog and SignalR infrastructure.

The current implementation contains the approved Option 2 manager room map and the first student-portal vertical slice. Student room search, housing requests, invoice lookup and repair requests use SQL Server data. The AI assistant is a clearly labelled UI/demo response layer only; model/API integration remains a later extension.

## Run locally

Requirements: .NET 8 SDK/runtime and SQL Server instance `DESKTOP-OSSBOT1\TUANANH` with Windows authentication.

```powershell
dotnet restore DormitoryManagementSystem.sln
dotnet ef database update --project DormitoryManagementSystem --startup-project DormitoryManagementSystem
dotnet run --project DormitoryManagementSystem
```

The application runs migrations and idempotent demo seeding at startup.

Demo manager account:

- Email: `manager@nepktx.local`
- Password: `Demo@123`

Demo student account:

- Email: `student@nepktx.local`
- Password: `Demo@123`
- Linked profile: `SV002106` · Nguyễn Minh An · room `A-302`

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
- `Services/`: room-map use cases and view-model projection.
- `Data/`: EF Core context, migrations and deterministic demo seed.
- `Models/`: Identity and dormitory domain entities.
- `Views/`: server-rendered Razor UI.
- `wwwroot/css` and `wwwroot/js`: custom styling and browser-native behavior.
- `scripts/capture-room-map.cjs`: repeatable desktop/mobile browser capture and smoke checks.
- `scripts/capture-student-portal.cjs`: visual and authorization regression checks for the student portal.
- `scripts/smoke-student-workflows.cjs`: submit/cancel housing request, invoice read and repair-submit browser tests with deterministic cleanup.

## Student portal checkpoint

- Separate role-protected student layout, responsive dashboard and mobile bottom navigation.
- Search only returns assignable rooms compatible with the student's profile.
- Housing-request and repair forms use antiforgery validation and derive the student/room identity from the signed-in account.
- Invoice totals and line items are database-backed; online payment remains explicitly out of scope.
- AI help includes grounded demo answers, source labels, known-function links and a no-information fallback. No API key or external model call is present.

See `docs/IMPLEMENTATION-CHECKPOINT-STUDENT-PORTAL.md` for the implemented flow and verification boundary.

The architecture follows the controller-service-view-model separation and startup conventions verified against the user-provided reference repository `Anhtuan2005/E-Lectrical--Commerce-`.
