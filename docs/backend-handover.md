# Customer Portal Backend Handover

## Current implementation status

The repository now contains a working backend implementation in code; this is no longer a frontend-only shell. ASP.NET Core Identity, role authorization, SQLite development persistence, PostgreSQL provider configuration, customer-scoped device/telemetry/alert/subscription/payment APIs, Paystack verification, device check-in, and customer portal page-model bindings are present. Customer registration, sign-in, device self-claim, and first-claim trial setup are implemented.

The latest portal integration is commit `f0bf097` (`Wire the customer portal to live data`). NuGet's vulnerability audit reports no vulnerable packages, and `dotnet build --no-restore` succeeds with zero warnings and errors after the dependency update. No runtime behavior was exercised as part of this handover update. Treat all statements here as repository evidence, not a production-readiness or security sign-off.

## Remaining work

### Account and customer services

- Add confirmed-email delivery and password reset/change flows. Email confirmation is intentionally not required yet because no email sender is configured.
- Add persisted profile updates, support tickets, notification retrieval/preferences, and approved help content. These remain disabled or validation-only shells.
- Review the single-user ownership model, account recovery, MFA, session invalidation, and customer terms acceptance.

### Product and billing decisions

- Confirm plan catalogue, currency/prices, billing periods, trial length, grace period, covered-device rules, taxes, refund/cancellation behavior, and subscription access policy. Current plan prices come from the product flyer; lifecycle durations are documented as provisional.
- Validate Paystack credentials, callback/public URL, webhook delivery, and reconciliation in the intended sandbox before production use.
- Receipts and refunds are not implemented as a customer workflow.

### Hardware and operations

- Confirm supported controller/relay protocol, telemetry units and reporting cadence, offline thresholds, credential provisioning/rotation, and ownership recovery with the hardware team.
- Keep remote pump locking disabled until product approval and device-side interlocks, command expiry, replay protection, and safe-failure behavior are verified in firmware.
- Create and review production EF migrations; Development currently uses SQLite `EnsureCreated`.
- Configure production secrets, PostgreSQL, HTTPS/proxy settings, a persistent protected Data Protection key store, logging/monitoring, backups, recovery, and deployment health checks.
- Extend rate limiting beyond the current authentication and device check-in policies to sensitive mutations as appropriate.

## Verification before production

Run a clean build and focused integration/security checks for registration and sign-in, role authorization, cross-customer access, antiforgery, device claim conflicts, telemetry freshness, alert acknowledgement, payment idempotency/webhook verification, and production database migrations. Review accessible customer pages at desktop and mobile sizes. Do not enable remote pump controls as part of this work.

## Development configuration

Keep secrets out of `appsettings.json`; use .NET user-secrets locally and the host secret store in production. Development uses a project-local Data Protection key ring and console logging to work in the restricted Windows environment. Never copy Development keys into production. See [backend-setup.md](backend-setup.md) for configuration and device protocol details, and [backend-integration-checklist.md](backend-integration-checklist.md) for the detailed status matrix.
