# NẾP KTX interface system

## Direction

- Product interface for daily dormitory operations; calm, bright, compact and practical.
- The primary management job on the room map is finding an actually assignable bed and understanding why a room is unavailable.
- The building/floor/room map is the visual anchor. Alerts and filters support that task without competing with it.
- Use Vietnamese labels that describe state explicitly. Never communicate room status by color alone.

## Tokens

- Shared visual tokens live in `wwwroot/css/workspace.css`: quiet paper surfaces, warm gray canvas, dark green ink and one restrained green action color. The existing room-map and student tokens are aliases to that palette.
- Body text is 14px; supporting text is 12px; compact status labels may use 11px. Headings use 24px on mobile and 28px on desktop, with 500–600 weights for content.
- Red is reserved for urgent/blocked states; orange for holds/overdue finance; green/teal for assignable state.
- Desktop body text is 14–15px. Headings are compact, high contrast, and use `Segoe UI` with system fallbacks.
- Radius: 6–8px for operational surfaces; pill radius only for status badges.
- Record surfaces use a single 1px border and 8px radius. Controls use 6px and dialogs use 10px. Shadows are reserved for overlays; decorative stripes, tinted icon tiles and nested statistic cards are avoided.

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
- The first focal surface is the single action that needs attention today, such as an unpaid invoice. Current room details use a flat record with a clear room number, dates and price.
- Room search uses three cards per row when the content area has space, then two or one on smaller screens. Room identity, price, occupancy and assignable beds are aligned for comparison. An uploaded room photo can serve as the cover. The current-room link opens the names-only roommate list on the dashboard.
- Personal data is always resolved from the authenticated account on the server. Forms never accept a student identifier from the browser.
- The AI help launcher remains small and user-initiated. On mobile its dialog becomes a full-screen panel. Responses identify Gemini and functional guidance, with allowlisted navigation links. Source citations are never invented; unknown regulations defer to management.
- Working content targets 13–15px on desktop and keeps compact 10–11px text only for metadata, labels and timestamps.

## Manager review-desk pattern

- Application review uses two zones: a prioritized worklist and a selected record containing evidence and its action form. The room transition and five-part capacity ledger appear before the decision. The primary approval action says “Duyệt và giữ chỗ”; physical move-in remains a separate confirmation.
- Contact details and audit history use native disclosures. Optional notes are collapsed until needed. The record header provides a direct jump to its action.
- Overdue, review and missing-information states are written labels as well as color treatments. Secondary decisions that need an explanation use focused dialogs.
- Mobile separates scanning the queue from reading the case while preserving the same order of evidence. The fixed bottom navigation must not cover decisions or the case heading.
- Student account management starts from the durable student profile. Linking, password reset and lock state are grouped in one access-control region rather than mixed into housing facts.
- The signature workflow motif is a compact handoff strip: `Lượt xử lý hiện tại → Việc tiếp theo`. It appears in both manager and student views and moves between the two roles without changing visual grammar.
- A submitted request exposes “Nhận xử lý đơn” before approval. A missing-information request replaces approval with the reason it is blocked and gives the student an inline response surface.
- Checkout shows source-stay evidence and says “Duyệt trả phòng”; it never borrows target-room or capacity copy from registration/transfer.

## Maintenance response-desk pattern

- Maintenance uses a worklist and a selected incident with an inline update form. Room evidence and capacity remain immediately before the form; history is available through a disclosure.
- Summary counters are actionable filters for open, urgent, unassigned and SLA-breached work. Red is limited to urgent/safety signals; assignment and progress actions remain teal.
- The room context shows capacity, occupied, held, blocked and assignable counts immediately before the update action. Closing or resolving an incident explicitly explains that blocked beds return to the room.
- Incident status, priority and assignment are always written labels. Timeline entries record actor, note and time; mobile presents queue and detail in document order with a direct “Cập nhật tiến độ” anchor.

## Operations overview and report pattern

- The manager landing page starts with a concrete list of overdue applications, overdue invoices and urgent incidents. Capacity and aggregate debt are secondary records. Building rows remain comparable and recent activity is limited to a short supporting list.
- Capacity always keeps occupied, held, blocked and assignable beds visible together. Finance shows issued, net collected, outstanding and overdue as separate concepts.
- Building comparison repeats the room-map language and links back to the selected building. Percentages are paired with exact counts and never stand alone.
- Period reports put native filters directly above the evidence, then read left-to-right from finance to workflow/maintenance outcomes and down to the building reconciliation table.
- CSV export uses the active report scope. Empty states say what data is absent and provide a clear filter reset where applicable.
- Operational color is semantic and restrained: teal for healthy/collected/assignable, orange for financial attention and red only for safety, overdue or blocked states.

## Implementation constraints

- Shared workspace styles load after the functional room-map/student styles. Reuse `workspace.css` tokens and component rules for new surfaces.
- Mobile filters disclose additional fields on demand. Worklists initially collapse to a clear “Hiện danh sách” control so the selected record is immediately reachable. Selecting a row preserves filters and focuses the chosen record.
- Use native dialogs and details. Mobile navigation must trap focus, make background content inert, support Escape and return focus to its trigger. Collapsed icon navigation retains accessible labels.
- Server validation errors remain visible until dismissed. Optional non-critical toast hints may expire. Preserve form drafts and antiforgery fields, and guard accidental double submits.
- When a repair submission is rejected, reopen the form with the entered text and the server message. Preview and remove selected photos on the device before upload. Explain when the browser requires a fresh file selection.
- Use persistent disclosures for payment instructions and a standalone layout for access, error and data-information pages.
- Desktop controls are at least 40px high; mobile inputs and primary actions are at least 44px. Use one main landmark, visible keyboard focus and a skip link.

- ASP.NET Core MVC with Razor Views.
- Handwritten CSS and browser-native JavaScript only; no Bootstrap, Tailwind, jQuery or frontend framework dependency.
- Use semantic HTML, visible keyboard focus and labels for icon-only controls.
