# Customer Portal Backend Handover

## Current verified baseline

The current project has no configured authentication provider, ASP.NET Core Identity, user store, database, customer API, device telemetry service, subscription service, Paystack/payment integration, support ticket service, or email delivery service. `UseAuthorization()` is present in `Program.cs`, but no authenticated customer policy or identity scheme is registered.

`/Login` and `/GetStarted` are intentionally preview-only pages. Their controls are disabled and do not submit or store credentials. Do not enable them by removing `disabled` until the real account workflow exists.

## Backend decisions required

1. Account provider: ASP.NET Core Identity or an approved external identity provider; define password policy, email verification, recovery, session lifetime, MFA, sign-out, and return URL rules.
2. Tenant/customer model: define customer ownership, roles, device access, and whether a user may belong to multiple organizations.
3. Database and migrations: select provider, retention, audit records, concurrency, and encryption requirements.
4. Device registration: define serial/verification authority, duplicate handling, ownership transfer, and verification expiry.
5. Telemetry: define supported metrics, units, sampling, retention, freshness, offline semantics, and access control.
6. Alerts: define severity taxonomy, acknowledgement semantics, notification channels, and audit history.
7. Plans: define canonical prices, currency, billing periods, covered-device rules, renewal/cancellation policy, taxes, and effective terms versions.
8. Payments: choose provider, initiation, redirect/callback, webhook signature verification, reconciliation, idempotency, refunds, receipts, and failure/cancellation semantics.
9. Support: define ticket persistence, attachments, staff workflow, replies, status transitions, and notification delivery.
10. Security operations: rate limits, CSRF/antiforgery, secure cookies, CSP, logging redaction, monitoring, backups, and incident response.

## Stage 7 support and account handover

The Stage 7 pages are presentation and validation shells only. Before enabling their disabled actions, implement and test:

- authenticated customer profile reads/updates with verified-contact rules and audit history;
- password change/reset through the selected identity provider, including antiforgery, rate limiting, session invalidation policy, and redacted logging;
- customer-scoped support tickets, ownership checks, status transitions, replies, idempotency, and safe confirmation based on a persisted ticket ID;
- notification retrieval and optional read-state mutations without treating unavailable data as an all-clear;
- approved notification-preference channels and safety policy;
- reviewed, versioned Help Centre articles and search.

Do not enable the Stage 7 buttons by removing `disabled` until those services return trusted responses. Do not add arbitrary attachment uploads without the documented scanning, storage, authorization, and retention design.

## Stage 8 runtime and deployment note

In the restricted Windows development environment, the default EventLog provider could not write to the `.NET Runtime` source and caused request-time logging exceptions. Development now uses console logging only. The profile's existing DPAPI-protected Data Protection key could not be decrypted, and the default user key directory was not writable; Development therefore uses the ignored project-local `.data-protection/development-keys/` repository. This is only a local development fallback. Production must configure a persistent, access-controlled, appropriately encrypted Data Protection key store and an approved logging sink; do not copy development keys or disable protection.

## Stage 6 subscription and payment handover

The Stage 6 Razor Pages are integration-ready presentation surfaces only. Before enabling any action, the backend team must provide a versioned plan catalog, an authenticated subscription service, an idempotent checkout endpoint that re-prices on the server, provider verification plus authenticated webhook reconciliation, and a customer-scoped payment-history and receipt service.

Required server states include pending, verified, failed, cancelled, unable-to-verify, duplicate/idempotency conflict, unauthorized, forbidden, unavailable, and provider timeout. A browser reference or redirect must never be treated as payment success. No plan prices, totals, transactions, receipts, subscription activation, provider credentials, or success states are implemented in the current UI.

## Required server behavior

- Authorize every customer resource by authenticated user and tenant/device ownership, not by route ID alone.
- Return safe structured errors and correlation IDs; never return passwords, provider secrets, or internal stack traces.
- Verify payment results server-side and make webhook processing idempotent.
- Mark stale/offline telemetry explicitly and include source timestamps.
- Enforce validation on the server even when the Razor UI provides client-side validation.
- Add audit events for device registration, acknowledgement, subscription changes, payment state changes, password changes, and support actions.

## Suggested delivery sequence

1. Backend contract and identity spike; connect `/Login`, `/GetStarted`, and `/Account/ForgotPassword`.
2. Persist customer, device, and authorization models.
3. Implement customer shell and dashboard against a controlled fixture adapter in Development.
4. Replace fixtures with device and monitoring services; add stale/offline states.
5. Add alerts and support services.
6. Add subscription catalog and verified payment flow in a sandbox, including pending/success/failure/cancelled results.
7. Add profile/security/notification settings.
8. Run security review, accessibility checks, end-to-end tests, and production-readiness review.

## Verification checklist before enabling UI actions

- Authenticated and unauthenticated route tests pass.
- Cross-customer resource access returns 403/404 as designed.
- Registration duplicate handling and password recovery are tested.
- Device verification has success, failure, expired, and duplicate cases.
- Telemetry fixtures are absent or visibly marked outside Development.
- Payment success is only emitted after verified provider state.
- Receipts are authorized and unavailable receipts are handled honestly.
- Support confirmation is based on a persisted ticket ID.
- Profile/security saves return actual server results.
- Loading, empty, failure, offline, stale, and access-denied states are manually reviewed at mobile and desktop widths.

## Current build result

Command: `dotnet build`

The project restored successfully, but the build was blocked by the existing running `bin\\Debug\\net10.0\\HydroSentinel.exe` process locking the apphost file. The reported errors were MSB3027/MSB3021 after ten retries; no C# or Razor compilation error was reported. Stop the running application and rerun `dotnet build` to obtain a clean result.
## Website quality upgrade handover

- Pricing display is centralized in `Services/PlanCatalog.cs` and transcribed from `wwwroot/images/flyer.jpeg`: Basic ₦79,000 + ₦5,000/month (1 unit), Pro ₦169,000 + ₦15,000/month (3 units), and Max ₦749,000 + ₦50,000/month (more than 3 units). The flyer launch offer is displayed only as a pending commercial note; it is not an activated promotion.
- Backend checkout must reprice from the authoritative catalogue. Client-visible catalogue values and `plan` query strings must never be trusted for payment totals or subscription activation.
- Development form testing is server-gated to Development and is non-persistent. It must not be carried into production or connected to identity, devices, support, notifications, payment, email, or hardware services.
- The portal still requires real authentication, authorization, persistence, provider verification, and audit logging before any disabled operation can be enabled.
