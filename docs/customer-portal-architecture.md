# HydroSentinel Customer Portal Architecture

## Status and scope

Stage 2 shell implementation is now present for the customer portal only. The Admin Portal is explicitly out of scope. The shell includes a responsive shared layout, grouped navigation, truthful routed placeholders, and reusable visual state patterns. Authentication configuration, database, payment integration, device integration, and customer APIs remain unavailable.

## Current application audit

The application is an ASP.NET Core 10 Razor Pages application:

- Project: `HydroSentinel.csproj`, target framework `net10.0`.
- Startup: `Program.cs` registers only `AddRazorPages()`, routing, authorization middleware, static assets, and Razor Pages. There is no `AddAuthentication`, `AddAuthorization` policy, Identity, database, API client, payment provider, or device service registration.
- Public pages currently live directly under `Pages/`: `Index`, `Product`, `HowItWorks`, `Pricing`, `About`, `Contact`, `Privacy`, `Error`, `Login`, and `GetStarted`.
- Public layout: `Pages/Shared/_Layout.cshtml`; authentication layout: `Pages/Shared/_AuthLayout.cshtml`.
- Existing form handling: `Pages/Contact.cshtml.cs` validates a contact form in memory and displays a review notice; it does not deliver messages.
- Login and Get Started currently present disabled, truthful preview forms. They do not collect, validate, store, or transmit credentials.
- Client JavaScript is currently `wwwroot/js/site.js`, which handles the public mobile navigation. There is no portal API client or chart library.
- Styling is split across `site.css`, `layout-polish.css`, `responsive-audit.css`, `page-redesign.css`, and page-specific stylesheets. The portal should introduce tokens and portal-specific styles without replacing public-page CSS.

## Proposed implementation architecture

Use Razor Pages under `Pages/Customer` with a dedicated `_CustomerLayout.cshtml`. Keep `/Login` and `/GetStarted` at their existing public routes unless a real account system is introduced; then redirect or link to them from the portal without duplicating pages.

Proposed shared pieces:

- `_CustomerLayout.cshtml`: authenticated shell, sidebar, top bar, skip link, notifications, account menu, and page scripts/styles.
- `_CustomerSidebar.cshtml`: grouped navigation with active route state and accessible collapse behavior.
- `_CustomerHeader.cshtml`: page title, breadcrumbs, notification trigger, account menu, and mobile drawer trigger.
- Shared partials/components for status badges, empty/error/offline states, confirmation dialogs, toast region, loading skeletons, data tables, and responsive chart containers.
- Feature-specific PageModels and services, with all external calls behind interfaces. Razor Pages should orchestrate view state, not contain payment, telemetry, or persistence logic.

Suggested service boundaries:

`IDeviceService`, `IMonitoringService`, `IAlertService`, `ISubscriptionService`, `IPaymentService`, `ISupportService`, `IProfileService`, and `IAccountService`. Each must have a real implementation before its related UI is enabled; otherwise use explicitly labelled development fixtures and never show them as live data.

## Data and trust boundaries

The browser should call same-origin server endpoints or Razor Page handlers. Server-side services should own authorization, tenant/user scoping, validation, provider calls, idempotency, and audit logging. Device readings, payment results, subscription status, and account details must be server-sourced. Fixtures must be clearly marked `Development only` and disabled in production.

Payment success is valid only after server-side provider verification and webhook/reconciliation rules succeed. A client redirect or button click is never sufficient. Telemetry charts must show timestamps, source state, freshness, units, and a visible stale/offline indicator.

## Design tokens

Initial tokens, subject to visual validation against the existing logo:

```css
:root {
  --portal-navy: #082B3B;
  --portal-green: #008F68;
  --portal-blue: #1685E5;
  --portal-bg: #F4F8FB;
  --portal-surface: #FFFFFF;
  --portal-text-muted: #5E7482;
  --portal-border: #DCE7ED;
  --portal-danger: #C83E46;
  --portal-warning: #B86A00;
  --portal-success: #008F68;
  --portal-radius-sm: 8px;
  --portal-radius-md: 14px;
  --portal-shadow-sm: 0 6px 18px rgb(8 43 59 / 7%);
}
```

Use Lucide-style inline SVG icons or an already approved local icon library. Do not add emoji or a large dependency solely for icons. Every icon-only control needs an accessible name.

## Responsive behavior

- Mobile-first base styles with a drawer sidebar below `900px`.
- At `640px`, stack cards, forms, tables, and chart controls; use full-width primary actions.
- At `900px`, show the collapsible desktop sidebar and top bar layout.
- At `1200px`, allow dashboard cards and charts to use two-column compositions.
- Set `min-width: 0` on grid/flex children; prevent horizontal overflow; make tables scroll inside labelled containers rather than shrinking critical text.
- Support keyboard focus, Escape-to-close drawers/dialogs, reduced motion, and touch-sized controls.

## Page state model

Every portal page should expose a view state rather than infer success from markup:

`Loading`, `Populated`, `Empty`, `ValidationError`, `RequestFailure`, `Offline`, `Stale`, `Pending`, `Success`, `Failure`, and `Unauthorized` as applicable. Each state needs a useful next action. “Success” must be tied to a verified server response.

## Testing plan

1. Build with `dotnet build` and run Razor route discovery tests.
2. Unit-test PageModels and service adapters for authorization, validation, idempotency, and state mapping.
3. Integration-test every proposed handler with unauthorised, empty, invalid, provider-failure, and success cases.
4. Verify payment success only after a mocked verified provider callback/webhook.
5. Verify charts never render live-looking data without freshness/source labels.
6. Run keyboard, screen-reader landmark, focus, contrast, and reduced-motion checks.
7. Inspect at 320, 375, 768, 1024, 1280, and 1440px with no horizontal overflow.

## Stage 2 implementation notes

The following files now provide the shell: `Pages/Shared/_CustomerLayout.cshtml`, `_CustomerSidebar.cshtml`, `_CustomerHeader.cshtml`, `_CustomerPlaceholder.cshtml`, `wwwroot/css/customer-design-tokens.css`, `wwwroot/css/customer-layout.css`, `wwwroot/css/customer-pages.css`, and `wwwroot/js/customer-navigation.js`. The route pages under `Pages/Customer` are intentionally minimal and state that their integration boundary is not connected. No page claims live telemetry, payment success, saved settings, ticket creation, or authenticated access.

The sidebar supports desktop collapse and a mobile drawer with overlay, Escape dismissal, focus return, active route styling, and reduced-motion CSS. The account menu links to the existing preview Login page; notifications and all feature destinations are real Razor routes.

## Stage 3 implementation notes

The `/Customer` dashboard overview now uses the shared customer shell and provides a responsive overview composition: connection-state introduction, four summary metrics, device and alert panels, subscription and activity states, monitoring preview, and quick actions. All values remain unavailable until real account and device services are connected. The contextual installation image is explicitly labelled as non-live imagery, and no chart readings, alerts, transactions, identities, or subscription values are fabricated.

Dashboard-specific styling is in `wwwroot/css/customer-dashboard.css`. The proposed dashboard data contract is documented in `docs/api-contracts.md` under `GET /api/customer/dashboard`.

## Next implementation stages

1. Agree the account/tenant model and authentication provider.
2. Agree the proposed API contracts in `docs/api-contracts.md`.
3. Add persistence and server-side services behind interfaces.
4. Implement the shared customer shell and dashboard with fixtures only in Development.
5. Implement devices and monitoring, then alerts.
6. Add subscriptions and verified payment integration.
7. Add support and settings, then accessibility and end-to-end verification.

## Website quality upgrade notes

The shared public and customer layouts load `wwwroot/css/quality-upgrade.css`, which centralizes Firmlon-green hover, focus-visible, active, disabled, transition, and reduced-motion refinements without replacing the existing page styles. Inline SVG icons remain the local icon strategy; no external icon dependency or emoji icon system was added.

The flyer-backed plan catalogue is centralized in `Services/PlanCatalog.cs` and sourced from `wwwroot/images/flyer.jpeg`. It is presentation context only. The backend must remain authoritative for price, eligibility, promotion, checkout totals, payment verification, and activation.

Development form testing is provided by `Services/FormTestingMode.cs` and is server-gated with `IWebHostEnvironment.IsDevelopment()`. The shared layouts show a temporary banner only in Development. Testable forms validate through normal Razor Page handlers, discard submitted values, and never call external services, persist records, store passwords, or initiate payments. Production does not expose this mode.
