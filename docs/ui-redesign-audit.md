# UI redesign audit

## Findings

- The Admin Portal has a dedicated layout and route set, but the previous Admin Login contained only a Development demo link; it had no email/password inputs or POST handler.
- Admin records are still served by `IAdminPortalProjection` from the Development-only `IDemoMode` store. Production data and administrator authentication are not connected.
- Public Login and Get Started are still preview-only pages without authentication or registration handlers.
- Customer pages use separate page-specific stylesheets layered over shared customer tokens and layout styles.
- The old full-width brown customer Development banner was rendered from `_CustomerLayout`; it has been removed from the visual flow and replaced with a compact header indicator.
- Inline SVG icons are used in the customer and admin navigation; no external icon library is installed.

## Changes in this audit pass

- Added a server-side Development-only Admin Login form with email, password, remember-me, validation, antiforgery-protected POST handling, and a truthful unavailable state outside Development.
- The Development handler never stores or logs the password and does not contact an external identity provider.

## Remaining integration work

Connect the form to the approved administrator identity service, role authorization, secure cookies, account recovery, and audit logging before production. Customer login, registration, and password recovery need the same service boundary when the backend is ready.
