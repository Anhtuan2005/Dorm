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

## Implementation constraints

- ASP.NET Core MVC with Razor Views.
- Handwritten CSS and browser-native JavaScript only; no Bootstrap, Tailwind, jQuery or frontend framework dependency.
- Use semantic HTML, visible keyboard focus and labels for icon-only controls.
