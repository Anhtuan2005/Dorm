# NẾP KTX interface system

## Direction

- Product interface for daily dormitory operations; calm, bright, compact and practical.
- The primary management job on the room map is finding an actually assignable bed and understanding why a room is unavailable.
- The building/floor/room map is the visual anchor. Alerts and filters support that task without competing with it.
- Use Vietnamese labels that describe state explicitly. Never communicate room status by color alone.

## Tokens

- Primary: `#0F766E`; page: `#F5F7F6`; surface: `#FFFFFF`.
- Main text: `#1F2937`; deep heading: `#17233F`; muted text: `#64748B`; border: `#E2E8E5`.
- Red is reserved for urgent/blocked states; orange for holds/overdue finance; green/teal for assignable state.
- Desktop body text is 14–15px. Headings are compact, high contrast, and use `Segoe UI` with system fallbacks.
- Radius: 6–8px for operational surfaces; pill radius only for status badges.
- Shadow is subtle and reserved for raised panels or overlays.

## Layout and behavior

- Desktop uses a collapsible left sidebar, global header, central map and closable room-detail panel.
- Building selection shows the number of assignable beds. Only one building is expanded.
- A room card always includes room code, occupied/capacity and a written status.
- Selected rooms use a teal border plus a light teal fill. Maintenance rooms state why placement is blocked.
- Filters appear only when relevant; date filters do not belong on the room map.
- Empty filtered results offer a direct clear-filter action.
- Mobile shows one floor at a time in a two-column room grid. Room details become a dedicated full-screen view, and primary actions remain reachable at the bottom.

## Student portal pattern

- The student portal is a distinct task space rather than a reduced manager dashboard. Desktop uses a narrow personal-work sidebar; mobile uses a four-item bottom navigation.
- The first focal surface is the single action that needs attention today (for example, an unpaid invoice). The second is a dark-teal room pass containing the current building, floor, room, price and move-in date.
- Search results use a room-door block as the recurring domain motif. Every result states room code, occupancy, assignable beds, price and amenities in text.
- Personal data is always resolved from the authenticated account on the server. Forms never accept a student identifier from the browser.
- The AI help launcher remains small and user-initiated. On mobile its dialog becomes a full-screen panel. Answers show a source label and navigation action; unknown questions explicitly defer to dormitory management.
- Working content targets 13–15px on desktop and keeps compact 10–11px text only for metadata, labels and timestamps.

## Manager review-desk pattern

- Application review uses three coordinated zones: prioritized queue, selected case with live placement proof, and a compact decision rail. It must feel like an operations inbox, not a generic data table.
- The room transition (`current → target`) and five-part bed ledger (capacity, occupied, held, blocked, assignable) sit immediately before approval. The primary action always states the outcome: “Duyệt và xếp phòng”.
- Overdue, review and missing-information states are written labels as well as color treatments. Secondary decisions that need an explanation use focused dialogs.
- Mobile separates scanning the queue from reading the case while preserving the same order of evidence. The fixed bottom navigation must not cover decisions or the case heading.
- Student account management starts from the durable student profile. Linking, password reset and lock state are grouped in one access-control region rather than mixed into housing facts.
- The signature workflow motif is a compact handoff strip: `Lượt xử lý hiện tại → Việc tiếp theo`. It appears in both manager and student views and moves between the two roles without changing visual grammar.
- A submitted request exposes “Nhận xử lý đơn” before approval. A missing-information request replaces approval with the reason it is blocked and gives the student an inline response surface.
- Checkout shows source-stay evidence and says “Duyệt trả phòng”; it never borrows target-room or capacity copy from registration/transfer.

## Maintenance response-desk pattern

- Maintenance uses a three-zone response corridor: prioritized incident queue, selected room evidence, and a narrow dispatch rail. The dominant task is deciding what must be handled now and how much capacity remains safe to assign.
- Summary counters are actionable filters for open, urgent, unassigned and SLA-breached work. Red is limited to urgent/safety signals; assignment and progress actions remain teal.
- The room context shows capacity, occupied, held, blocked and assignable counts immediately before the update action. Closing or resolving an incident explicitly explains that blocked beds return to the room.
- Incident status, priority and assignment are always written labels. Timeline entries record actor, note and time; mobile presents queue and detail in document order with a direct “Cập nhật tiến độ” anchor.

## Operations overview and report pattern

- The manager landing page uses one connected “operations spine”, not a row of interchangeable KPI cards: current capacity is primary, money is secondary and intervention work is the immediate action rail.
- Capacity always keeps occupied, held, blocked and assignable beds visible together. Finance shows issued, net collected, outstanding and overdue as separate concepts.
- Building comparison repeats the room-map language and links back to the selected building. Percentages are paired with exact counts and never stand alone.
- Period reports put native filters directly above the evidence, then read left-to-right from finance to workflow/maintenance outcomes and down to the building reconciliation table.
- CSV export uses the active report scope. Empty states say what data is absent and provide a clear filter reset where applicable.
- Operational color is semantic and restrained: teal for healthy/collected/assignable, orange for financial attention and red only for safety, overdue or blocked states.

## Implementation constraints

- ASP.NET Core MVC with Razor Views.
- Handwritten CSS and browser-native JavaScript only; no Bootstrap, Tailwind, jQuery or frontend framework dependency.
- Use semantic HTML, visible keyboard focus and labels for icon-only controls.
