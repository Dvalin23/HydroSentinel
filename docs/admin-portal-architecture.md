# Admin portal architecture

The first admin slice lives under `Pages/Admin` and uses the dedicated `_AdminLayout` and `_AdminSidebar`. It is intentionally separate from the customer portal layout.

## Development boundary

`IAdminPortalProjection` is a presentation boundary registered in `Program.cs`. `AdminPortalProjection` only exposes the existing `IDemoMode` snapshot when `IWebHostEnvironment.IsDevelopment()` is true. Outside Development it returns an unavailable snapshot; it does not expose a production authentication bypass, query-string switch, browser flag, external call, payment action, telemetry claim, or hardware command.

The demo projection is fictional and server-memory only. Every admin page labels demo content and provides unavailable states where a backend is required. `PlanCatalog` is reused for subscription figures; the admin UI does not create alternate prices.

## Backend handover

Replace `IAdminPortalProjection` with an authenticated implementation that composes customer, device, telemetry, alert, billing, support, notification, and reporting services. Keep authorization, tenant ownership, audit logging, verified payment webhooks, secret management, and hardware command safety in those backend services. The page routes and UI structure can remain stable while the projection changes.

## Routes

`/Admin/Login`, `/Admin/Dashboard`, `/Admin/Customers`, `/Admin/Customers/Details?id=...`, `/Admin/Devices`, `/Admin/Devices/Details?id=...`, `/Admin/Monitoring`, `/Admin/Alerts`, `/Admin/Alerts/Details?id=...`, `/Admin/Subscriptions`, `/Admin/Plans`, `/Admin/Payments`, `/Admin/Payments/Details?id=...`, `/Admin/Support`, `/Admin/Support/Details?id=...`, `/Admin/Notifications`, `/Admin/Reports`, `/Admin/Settings`, `/Admin/Settings/Profile`, `/Admin/Settings/Security`, `/Admin/Settings/Notifications`, `/Admin/Settings/Integrations`, and `/Admin/Help` are present in this slice.

Detail routes accept an identifier through the query string and return a labelled not-found state for unknown demo ids. They never fall back to fabricated records. The login page uses a split-screen operations-console composition; Development mode exposes only a clearly labelled link to the existing in-memory demo workspace, while other environments show the integration-required state.
