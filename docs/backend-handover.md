# Customer Portal Backend Handover

## Current implementation status

The repository now contains a working backend implementation in code; this is no longer a frontend-only shell. ASP.NET Core Identity, role authorization, SQLite development persistence, PostgreSQL provider configuration, customer-scoped device/telemetry/alert/subscription/payment APIs, Paystack verification, device check-in, and customer portal page-model bindings are present. Customer registration, sign-in, device self-claim, and first-claim trial setup are implemented.

Customer account services now include confirmed registration, email-change confirmation, password reset/change, and persisted name/phone updates. Email sending uses MailKit over SMTP STARTTLS; the user configured Gmail locally and confirmed the account email flow works. Other environments must provide their own SMTP credentials and public HTTPS origin. The latest build succeeds with zero warnings and errors. Treat this as repository evidence, not a production-readiness or security sign-off.

Support tickets now persist with customer ownership checks, customer conversation replies, an authenticated admin queue, staff replies, controlled status updates, and audit entries. A build passed; the new runtime flow still needs customer-isolation and staff-workflow verification. Notifications/email alerts, attachments, and reviewed production database migrations remain outstanding.

## Remaining work

### Account and customer services

- Confirm SMTP configuration and deliverability in the target environment. New registrations require email confirmation; password reset and email change use expiring Identity tokens.
- Review existing unconfirmed accounts after enabling required email confirmation. They can request a confirmation link, but cannot sign in until they confirm.
- Notifications and approved Help Centre content remain disabled or validation-only shells. Support tickets are implemented in code; production database migrations and workflow verification remain outstanding.
- Product must approve the customer/tenant model, MFA policy, session invalidation behavior, and publish the Terms of Service and Privacy Policy referenced during registration.

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

Run a clean build and focused integration/security checks for registration and confirmation, resend confirmation, password reset/change, email change, role authorization, cross-customer access, antiforgery, device claim conflicts, telemetry freshness, alert acknowledgement, payment idempotency/webhook verification, and production database migrations. Test SMTP delivery and link expiry with the chosen mail provider. Review accessible customer pages at desktop and mobile sizes. Do not enable remote pump controls as part of this work.

## Development configuration

Keep secrets out of `appsettings.json`; use .NET user-secrets locally and the host secret store in production. Development uses a project-local Data Protection key ring and console logging to work in the restricted Windows environment. Never copy Development keys into production. See [backend-setup.md](backend-setup.md) for configuration and device protocol details, and [backend-integration-checklist.md](backend-integration-checklist.md) for the detailed status matrix.
