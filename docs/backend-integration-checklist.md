# HydroSentinel Customer Portal Backend Integration Checklist

This checklist tracks implementation evidence in the repository. **Implemented** means code is present; it does not mean runtime, security, product, hardware, or production acceptance has been completed.

Status values: **implemented**, **pending**, **blocked**, or **requires confirmation**.

## A. Identity and access

| Requirement | Responsible area | Status | Evidence / remaining work |
|---|---|---|---|
| Registration and sign-in | Backend | implemented / pending configuration | ASP.NET Identity registration and sign-in require confirmed email. Confirmation resend and lockout are implemented; configure SMTP before enabling registration. |
| Identity provider and customer model | Backend/Product | implemented / requires confirmation | ASP.NET Core Identity with Customer/Admin roles and one-user device ownership is configured. Product must confirm tenant/account model, MFA, and publish the customer terms/privacy documents. |
| Email confirmation and verified email changes | Backend/Operations | implemented / pending configuration | Identity confirmation/change tokens, pages, and SMTP STARTTLS sender exist. Configure the sender and public HTTPS origin; exercise delivery, expiry, and existing unconfirmed accounts. |
| Password recovery and password changes | Backend | implemented / pending configuration | Reset requests send expiring Identity links without revealing account existence; authenticated password change verifies the current password. SMTP delivery remains to be configured/tested. |
| Customer authorization and ownership checks | Backend | implemented / pending | Customer API/page access uses the signed-in user's role and owned device/user scope for devices, readings, alerts, subscription, payments, and account settings. Support services do not exist; cross-customer integration checks remain to be run. |
| Session and cookie security | Backend/Operations | implemented / pending | Secure, HttpOnly, SameSite=Lax cookies, eight-hour expiry, HTTPS redirect, and antiforgery are configured. Review production proxy/cookie behavior, MFA, and session invalidation. |
| Audit logging | Backend/Operations | implemented / pending | Audit service and selected device/payment mutations exist. Confirm coverage, correlation, retention, redaction, and operational sink. |

## B. Customer and device management

| Requirement | Responsible area | Status | Evidence / remaining work |
|---|---|---|---|
| Customer profile persistence | Backend | implemented | Full name and phone updates persist; changing email requires confirmation at the new address. |
| Device registration and ownership verification | Backend/Hardware | implemented / requires confirmation | Admin registration and customer self-claim by serial plus one-time key exist; duplicate/conflict outcomes are handled. Confirm provisioning and ownership recovery with hardware/support. |
| Device inventory and details | Backend | implemented | Customer-scoped database queries back device list and detail pages; identifiers are masked in customer-facing APIs. |
| Device status and last-seen timestamps | Backend/Hardware | implemented / requires confirmation | Check-ins persist last-seen state; freshness labels are derived in code. Confirm offline threshold with hardware/product. |
| Telemetry history and freshness | Backend/Hardware | implemented / requires confirmation | Readings persist tank level, fill percent, pressure, pump/mains, fault, battery, and signal. Percent and centibar inputs are optional and range-checked. Confirm units, cadence, retention, and hardware report behavior. |
| Alerts and acknowledgement | Backend/Hardware/Product | implemented / pending | Device events are persisted, customer-scoped and acknowledgeable. Review severity taxonomy, audit semantics, delivery, and cross-customer behavior. |

## C. Subscriptions and payments

| Requirement | Responsible area | Status | Evidence / remaining work |
|---|---|---|---|
| Approved plan catalogue and pricing | Product/Backend | requires confirmation | Plan catalog is centralized and used server-side. Confirm commercial prices, allowances, billing terms, currency, taxes, and eligibility. |
| Subscription persistence and lifecycle | Backend | implemented / pending sandbox | Monthly recurring agreements and ledger exist; first-device trial stays 7 days, renewal extends by calendar month, failed renewal keeps 3-day grace, and cancellation retains paid-through access. Sandbox event flow still needs verification. |
| Server-side payment initialization | Backend | implemented / pending configuration | Customer consents to NGN 50 card verification; server validates a matching monthly plan code and creates the checkout. Configure three test plan codes and exercise with a Paystack test account. |
| Paystack transaction verification and verification refund | Backend | implemented / pending sandbox | Reusable card authorization, amount/currency, refund request, and refund event statuses are tracked. Verify sandbox processing and refund edge cases. |
| Authenticated and idempotent webhooks | Backend/Operations | implemented / pending | Signature validation handles charge, subscription status, invoice failure, and refund events. Confirm configured endpoint, provider retries, and live operational monitoring. |
| Payment history and receipts | Backend | partially implemented | Customer-scoped one-off and recurring transactions/refund statuses are listed. Receipt delivery/download is not implemented. |
| Subscription activation after verified payment | Backend | implemented / pending sandbox | Delayed monthly subscription is created after reusable card verification, scheduled at the trial or paid-through date. Exercise success, failure, retry, duplicate, and provider-unavailable cases. |
| Billing authorization and access rules | Backend/Product | implemented / requires confirmation | Terms checkbox, Paystack hosted manage/cancel flow, paid-through access, and 3-day grace are coded. Publish final commercial terms and verify cancellation/refund behavior in sandbox. |

## D. Support and notifications

| Requirement | Responsible area | Status | Evidence / remaining work |
|---|---|---|---|
| Ticket creation, ownership, replies and status transitions | Backend/Support | implemented / pending verification | Customer-owned tickets and conversations persist; customers can reply, admins can reply and change status, and mutations are audited. A build passes; exercise cross-customer denial and staff workflow before production. Attachments and customer email notifications are not included. |
| In-app notification persistence | Backend | implemented / pending runtime verification | Customer-owned records persist for device warnings and critical events, staff ticket replies, and payment/subscription lifecycle updates. External email/SMS/push delivery is not connected. |
| Read/unread state | Backend | implemented / pending runtime verification | Customer inbox lists only the signed-in user's records and supports marking one or all as read. Database indexes support owner/time and owner/read queries. |
| Supported notification preferences | Product/Backend | requires confirmation | Controls remain unavailable outside development validation mode; approve channels and safety rules first. |
| Approved Help Centre articles | Product/Support | pending | Search/content shell exists; approved article catalogue is not connected. |
| Attachments | Backend/Operations | requires confirmation | Not implemented; define scanning, storage, authorization, retention, and download controls first. |

## E. Hardware integration and safety

| Requirement | Responsible area | Status | Evidence / remaining work |
|---|---|---|---|
| Controller connectivity and supported protocols | Hardware/Backend | partially implemented / requires confirmation | Check-in accepts a trusted relay or a direct HTTPS request authenticated with the per-device key. The submitted SIM800L sketch does not yet send check-ins; verify HTTPS and server-certificate support on its exact modem firmware and confirm APN/hosting. |
| Device API, MQTT broker, gateway or cloud service | Hardware/Backend | implemented / requires confirmation | `/api/device/checkin` is the current API boundary. Confirm it is the approved authoritative path and document availability/retry behavior. |
| Device identity and credential management | Hardware/Backend | implemented / pending | One-time device keys are hashed; admin rotation and customer claim exist. Confirm secure physical provisioning, revocation, rotation, and operations. |
| Telemetry schema, units and reporting frequency | Hardware/Product | partially implemented / requires confirmation | Tank state, percentage, pressure, pump/mains, fault, battery, and signal are represented. Confirm definitions, sampling, source clock, and retention. |
| Offline and stale-device semantics | Hardware/Backend | implemented / requires confirmation | Freshness derives from last check-in; no offline event background job exists. Approve thresholds and delayed/stale semantics. |
| Supported remote commands | Hardware/Product | requires confirmation | Remote lock is disabled by configuration. Do not enable until product/hardware approves command behavior. |
| Safety interlocks and command protection | Hardware/Backend | blocked | Firmware-side interlocks, expiry, replay protection, and safe failure behavior are not verified. Keep remote lock disabled. |
| Firmware update process and security | Hardware/Operations | requires confirmation | No update workflow is documented in this repository. |

## F. Operations and deployment

| Requirement | Responsible area | Status | Evidence / remaining work |
|---|---|---|---|
| Database selection and migrations | Backend/Operations | implemented / pending deployment | SQLite `EnsureCreated` is Development-only. A PostgreSQL baseline migration and idempotent SQL script are checked in; Production requires Postgres and rejects startup with pending migrations. Provision the managed DB, apply the migration, and verify backup/restore and rollback procedures in staging. |
| Secrets management | Operations | partially implemented / pending deployment | Render Blueprint keeps credentials out of source and generates the relay secret. Enter SMTP, admin, and Paystack values in the Render secret store; review access and rotation after deployment. |
| HTTPS and production hosting | Operations | implemented / pending deployment | Render Docker service, forwarded HTTPS handling, HTTPS redirect/HSTS, and database health check are configured in `render.yaml`. Deploy and verify the hosted URL and custom domain. |
| Data Protection key persistence | Operations | implemented / pending deployment | Development has a project-local key ring. Production keys persist in PostgreSQL using the EF-backed key store; verify cookies remain usable across restarts after deploy. |
| Error handling and monitoring | Backend/Operations | partially implemented / pending | Safe API problem responses include correlation IDs. Configure structured redacted logs, monitoring, alerting, and production exception review. |
| Backups and recovery | Operations | pending | Define and exercise database, key, payment, and support recovery. |
| Rate limiting | Backend/Operations | partially implemented / pending | Authentication and device check-in are rate limited. Add suitable limits to other sensitive mutations and review partitioning. |
| Privacy and retention policies | Product/Operations | requires confirmation | Define collection purpose, retention/deletion, export, and telemetry/payment data handling. |
| Production configuration and deployment checks | Operations | pending | Configure environment-specific secrets/database/key store/logging, migrations, health checks, and smoke/security checks. |

## Current repository evidence and handover

- The latest customer portal integration is commit `f0bf097` (`Wire the customer portal to live data`). It binds customer dashboard, device, monitoring, alert, subscription, and payment-history pages to persisted, owner-scoped data.
- NuGet's transitive dependency audit reports no vulnerable packages after the dependency update. `dotnet build --no-restore` succeeds with zero warnings and errors. The user confirmed local account-email delivery; the support flow still needs runtime verification. Do not interpret **implemented** as production-approved.
- Development uses a project-local Data Protection key directory and console logging for the restricted Windows development environment. These are development-only choices.
- See [backend-setup.md](backend-setup.md) for setup and protocol details and [backend-handover.md](backend-handover.md) for remaining work and production readiness.
