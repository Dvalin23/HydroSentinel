# Customer Portal Route Map

The customer route map includes both live data-backed pages and remaining presentation shells. Customer pages require the configured Identity role outside Development demo mode. Admin routes are intentionally excluded.

## Account routes

| Route | Purpose | States/actions |
|---|---|---|
| `/Login` | Existing login page; integrate with real account handler | validation, authentication failure, return URL |
| `/GetStarted` | Customer registration with required email confirmation | validation, generic duplicate handling, confirmation email |
| `/Account/RegisterConfirmation` | Registration confirmation state | message delivery state, resend link |
| `/Account/ConfirmEmail` | Confirm new account email or changed address | success, invalid/expired token |
| `/Account/ResendConfirmation` | Resend account confirmation email | generic response, SMTP availability |
| `/Account/ForgotPassword` | Password recovery request | generic response, SMTP availability |
| `/Account/ResetPassword` | Reset password from a one-use Identity token | password validation, expired/invalid token, success |

## Customer routes

| Route | Purpose | Main actions |
|---|---|---|
| `/Customer` | Dashboard/overview | open device, alerts, subscription, support, quick actions |
| `/Customer/Devices` | Device list | Stage 4 UI shell: search/filter structure, truthful unavailable state, Add Device and Help/Contact routes; backend unconnected |
| `/Customer/Devices/Add` | Register pump/device | Stage 4 guided UI shell: disabled integration boundary, ownership note, registration steps; no verification endpoint |
| `/Customer/Devices/Details?id={id}` | Device detail | Stage 4 detail shell: Overview, Monitoring, History, Settings states and related-alert navigation; no device record service |
| `/Customer/Monitoring` | Monitoring workspace | Stage 5 responsive workspace; selectors, charts and readings remain unavailable until telemetry is connected |
| `/Customer/Alerts` | Alerts list | Stage 5 alert centre; filters, records and acknowledgement remain unavailable until alert service is connected |
| `/Customer/Alerts/Details?id={id}` | Alert detail | Stage 5 integration-ready detail state; accepts an optional alert id without resolving or fabricating a record |
| `/Customer/Subscription` | Current subscription | covered devices, renewal, current status |
| `/Customer/Subscription/Plans` | Plan selection/comparison | select plan, review terms, continue to checkout |
| `/Customer/Payments/Checkout?plan={id}` | Checkout | summary, billing period, provider handoff |
| `/Customer/Payments/Result?reference={reference}` | Provider result | pending, verified success, failed, cancelled |
| `/Customer/Payments/History` | Transaction history | filter, detail, receipt when available |
| `/Customer/Support` | Ticket list | search/filter, create ticket, open ticket |
| `/Customer/Support/Create` | Create ticket | validate, submit, confirmation from server |
| `/Customer/Support/Details?id={id}` | Ticket conversation | reply, status, resolution history |
| `/Customer/Settings/Profile` | Profile details | save full name/phone; email change requires confirmation |
| `/Customer/Settings/Security` | Password/security | change password after current-password verification; link to reset flow |
| `/Customer/Settings/Notifications` | Notification preferences | save preferences, confirmation |
| `/Customer/Help` | Help Centre | FAQs, setup, troubleshooting, support links |

## Navigation rules

Sidebar items now point to real Razor routes. Feature pages render a shared "Not connected yet" state and link to Contact, Help Centre, or the relevant public page; they do not show fake completion. Mobile navigation uses the same route map in a drawer, with active state, overlay, focus return, and Escape dismissal.

## Page state coverage

- Dashboard: loading, no devices, populated, stale/offline summary, request failure, unauthorized.
- Devices: loading, empty, validation errors, registration failure, verification pending/success/failure, unauthorized.
- Device details/monitoring: loading, no readings, stale, offline, request failure, access denied.
- Alerts: loading, no alerts, filter-empty, request failure, acknowledgement pending/success/failure.
- Subscription/plans: loading, no subscription, plan comparison, unavailable plan, unauthorized.
- Checkout/result/history: summary validation, pending provider response, verified success, failure, cancelled, duplicate/idempotency conflict, receipt unavailable.
- Support: loading, no tickets, form validation, submission failure, created confirmation, conversation reply failure.
- Settings: loading, validation error, save success, save failure, unauthorized, confirmation dialog.
- Help: loading, populated, no results, request failure, contact fallback.

## Stage 6 implementation notes

The subscription and payment routes now provide responsive integration-ready UI surfaces without backend behavior:

- `/Customer/Subscription` renders unavailable subscription status, coverage, renewal, and management states.
- `/Customer/Subscription/Plans` renders a plan-comparison structure with no published prices or selectable offers.
- `/Customer/Payments/Checkout?plan={id}` preserves the requested plan identifier only as untrusted display context.
- `/Customer/Payments/Result?reference={reference}` distinguishes a missing reference from an unverified reference; it never treats the reference as payment proof.
- `/Customer/Payments/History` renders an authorized-records table structure and unavailable filters/pagination state.

The five pages remain dependent on an authenticated customer context, canonical plan catalog, subscription service, payment provider, server-side verification, reconciliation, and receipt service. No payment, plan selection, subscription activation, transaction, receipt, or success state is implemented.

## Stage 7 implementation notes

The support, account, notification, and help routes now have dedicated responsive presentation surfaces:

- `/Customer/Support` shows an honest unavailable ticket-list state and links to ticket creation, Help Centre, and public contact.
- `/Customer/Support/Create` provides validated subject, category, related-device, and description fields. Controls remain disabled until ticket persistence and ownership services exist; no ticket success is shown.
- `/Customer/Support/Details?id={id}` validates the query-string reference and does not resolve or expose ticket data without a server ownership check.
- `/Customer/Notifications` provides an unavailable notification centre. Read-state controls remain disabled and do not mutate local state.
- `/Customer/Settings/Profile` reads and saves the signed-in user's name and phone; email changes take effect only after confirmation at the new address.
- `/Customer/Settings/Security` uses ASP.NET Identity for authenticated password changes; reset requests use expiring, one-use Identity tokens sent by the configured SMTP service.
- `/Customer/Settings/Notifications` provides disabled preference controls and does not assume email, SMS, WhatsApp, push, or delivery capabilities.
- `/Customer/Help` provides route-linked topic guidance, honest article-unavailable/search states, and restrained draft FAQ structure.

Support, notification, preference, and approved-help-content pages remain dependent on services that are not implemented. Account profile/password workflows are connected to Identity, but email flows require SMTP configuration and MFA is not enabled.
