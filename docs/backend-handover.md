# Customer Portal Backend Handover

## Current implementation status

The repository now contains a working backend implementation in code; this is no longer a frontend-only shell. ASP.NET Core Identity, role authorization, SQLite development persistence, PostgreSQL provider configuration, customer-scoped device/telemetry/alert/subscription/payment APIs, Paystack verification, device check-in, and customer portal page-model bindings are present. Customer registration, sign-in, device self-claim, and first-claim trial setup are implemented.

Customer account services now include confirmed registration, email-change confirmation, password reset/change, and persisted name/phone updates. Email sending uses MailKit over SMTP STARTTLS; the user configured Gmail locally and confirmed the account email flow works. Other environments must provide their own SMTP credentials and public HTTPS origin. The latest build succeeds with zero warnings and errors. Treat this as repository evidence, not a production-readiness or security sign-off.

Support tickets now persist with customer ownership checks, customer conversation replies, an authenticated admin queue, staff replies, controlled status updates, and audit entries. Persistent in-app notifications now cover device warnings and critical events, staff ticket replies, and billing/payment lifecycle updates. The customer inbox supports owner-scoped read/unread state and marking individual or all notifications read. Notifications remain in-app only; email/SMS/push alerts and delivery preferences need policy decisions. A build passed; the ticket runtime flow still needs customer-isolation and staff-workflow verification. Attachments and reviewed production database migrations remain outstanding.

## Remaining work

### Account and customer services

- Confirm SMTP configuration and deliverability in the target environment. New registrations require email confirmation; password reset and email change use expiring Identity tokens.
- Review existing unconfirmed accounts after enabling required email confirmation. They can request a confirmation link, but cannot sign in until they confirm.
- In-app notifications are persisted and customer-scoped. Email/SMS/push delivery and notification preferences remain pending policy approval; approved Help Centre content remains outstanding. Support tickets are implemented in code; production database migrations and workflow verification remain outstanding.
- Product must approve the customer/tenant model, MFA policy, session invalidation behavior, and publish the Terms of Service and Privacy Policy referenced during registration.

### Product and billing decisions

- User decisions for billing: monthly automatic renewal only; retain flyer prices in sandbox; keep the existing 7-day trial using a disclosed NGN 50.00 card verification charge with an immediate refund request; cancel before renewal while preserving access through the paid period; keep the 3-day grace period; defer the flyer promotion until its terms are confirmed.
- Recurring Paystack code is implemented in `Backend/RecurringBillingService.cs`; customer checkout, status, manage/cancel link, payment history, and subscription/refund webhooks are wired. It has only been compiled; sandbox checkout, renewal and refund delivery still need to be exercised.
- Create separate Basic/Pro/Max monthly plans in Paystack Test Mode at NGN 5,000, NGN 15,000, and NGN 50,000, with no invoice limit. Configure their plan codes in local user-secrets. Production needs separate live plan codes.
- Development payment requests require a test secret key and `Demo:Enabled=false`; production requires a live secret key. A public HTTPS webhook host is required for Paystack to reach `/api/paystack/webhook`; configure charge, subscription, invoice failure, and refund events.
- Customer-facing receipt delivery/download is not implemented. Review refund handling and final commercial/legal terms before live processing.

### Hardware and operations

- Confirm supported controller/relay protocol, telemetry units and reporting cadence, offline thresholds, credential provisioning/rotation, and ownership recovery with the hardware team.
- Keep remote pump locking disabled until product approval and device-side interlocks, command expiry, replay protection, and safe-failure behavior are verified in firmware.
- A PostgreSQL baseline migration is now checked in under `Data/Migrations/Postgres`; apply it to a provisioned database before production startup. Development continues to use SQLite `EnsureCreated`.
- Configure production secrets, PostgreSQL, HTTPS/proxy settings, a persistent protected Data Protection key store, logging/monitoring, backups, recovery, and deployment health checks. See `deployment/README.md`.
- Extend rate limiting beyond the current authentication and device check-in policies to sensitive mutations as appropriate.

## Verification before production

Run a clean build and focused integration/security checks for registration and confirmation, resend confirmation, password reset/change, email change, role authorization, cross-customer access, antiforgery, device claim conflicts, telemetry freshness, alert acknowledgement, payment idempotency/webhook verification, and apply/restore the production PostgreSQL migration on a staging database. Test SMTP delivery and link expiry with the chosen mail provider. Review accessible customer pages at desktop and mobile sizes. Do not enable remote pump controls as part of this work.

## Development configuration

Keep secrets out of `appsettings.json`; use .NET user-secrets locally and the host secret store in production. Development uses a project-local Data Protection key ring and console logging to work in the restricted Windows environment. Never copy Development keys into production. See [backend-setup.md](backend-setup.md) for configuration and device protocol details, and [backend-integration-checklist.md](backend-integration-checklist.md) for the detailed status matrix.
