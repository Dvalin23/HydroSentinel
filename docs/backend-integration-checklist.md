# HydroSentinel Customer Portal Backend Integration Checklist

This checklist tracks implementation evidence in the repository. **Implemented** means code is present; it does not mean runtime, security, product, hardware, or production acceptance has been completed.

Status values: **implemented**, **pending**, **blocked**, or **requires confirmation**.

## A. Identity and access

| Requirement | Responsible area | Status | Evidence / remaining work |
|---|---|---|---|
| Registration and sign-in | Backend | implemented | ASP.NET Identity registration/sign-in, lockout, generic login errors, and customer-only self-registration exist. Email confirmation and recovery are not available. |
| Identity provider and customer model | Backend/Product | implemented / requires confirmation | ASP.NET Core Identity with Customer/Admin roles and one-user device ownership is configured. Product must confirm tenant/account model, MFA, recovery, and customer terms. |
| Password recovery and password changes | Backend | pending | No email sender, reset delivery, or authenticated password-change flow. |
| Customer authorization and ownership checks | Backend | implemented / pending | Customer API/page access uses the signed-in user's role and owned device/user scope for devices, readings, alerts, subscription, and payments. Support/profile services do not exist; cross-customer integration checks remain to be run. |
| Session and cookie security | Backend/Operations | implemented / pending | Secure, HttpOnly, SameSite=Lax cookies, eight-hour expiry, HTTPS redirect, and antiforgery are configured. Review production proxy/cookie behavior, MFA, and session invalidation. |
| Audit logging | Backend/Operations | implemented / pending | Audit service and selected device/payment mutations exist. Confirm coverage, correlation, retention, redaction, and operational sink. |

## B. Customer and device management

| Requirement | Responsible area | Status | Evidence / remaining work |
|---|---|---|---|
| Customer profile persistence | Backend | pending | Registration data is persisted; editable profile reads/updates are not implemented. |
| Device registration and ownership verification | Backend/Hardware | implemented / requires confirmation | Admin registration and customer self-claim by serial plus one-time key exist; duplicate/conflict outcomes are handled. Confirm provisioning and ownership recovery with hardware/support. |
| Device inventory and details | Backend | implemented | Customer-scoped database queries back device list and detail pages; identifiers are masked in customer-facing APIs. |
| Device status and last-seen timestamps | Backend/Hardware | implemented / requires confirmation | Check-ins persist last-seen state; freshness labels are derived in code. Confirm offline threshold with hardware/product. |
| Telemetry history and freshness | Backend/Hardware | implemented / requires confirmation | Readings persist tank level, fill percent, pressure, pump/mains, fault, battery, and signal. Percent and centibar inputs are optional and range-checked. Confirm units, cadence, retention, and hardware report behavior. |
| Alerts and acknowledgement | Backend/Hardware/Product | implemented / pending | Device events are persisted, customer-scoped and acknowledgeable. Review severity taxonomy, audit semantics, delivery, and cross-customer behavior. |

## C. Subscriptions and payments

| Requirement | Responsible area | Status | Evidence / remaining work |
|---|---|---|---|
| Approved plan catalogue and pricing | Product/Backend | requires confirmation | Plan catalog is centralized and used server-side. Confirm commercial prices, allowances, billing terms, currency, taxes, and eligibility. |
| Subscription persistence and lifecycle | Backend | implemented / requires confirmation | Subscription/trial lifecycle and state projection exist. Confirm trial length, 30-day billing increments, grace period, cancellation, and access policy. |
| Server-side payment initialization | Backend | implemented | Checkout prices from the server catalog; client submits plan, months, and idempotency key. Verify behavior in the intended environment. |
| Paystack transaction verification | Backend | implemented / pending | Reference, amount, and currency are checked through Paystack before settlement. Sandbox integration verification remains. |
| Authenticated and idempotent webhooks | Backend/Operations | implemented / pending | Webhook signature validation and settlement idempotency exist. Confirm configured endpoint, provider retries, and live operational monitoring. |
| Payment history and receipts | Backend | partially implemented | Customer-scoped payment history and reference lookup exist. Receipt delivery/download is not implemented. |
| Subscription activation after verified payment | Backend | implemented / pending | Settlement activates/extends subscription after verification; exercise success, failure, retry, duplicate, and provider-unavailable cases. |
| Billing authorization and access rules | Backend | pending | Ownership checks exist, but cancellation/refund/plan-change workflows and final business access rules need product decisions. |

## D. Support and notifications

| Requirement | Responsible area | Status | Evidence / remaining work |
|---|---|---|---|
| Ticket creation, ownership, replies and status transitions | Backend/Support | pending | Support pages are validation-only; no ticket persistence/service. |
| Notification delivery and persistence | Backend/Operations | pending | No notification store or delivery integration. |
| Read/unread state | Backend | pending | No persisted notification read state. |
| Supported notification preferences | Product/Backend | requires confirmation | Controls remain unavailable outside development validation mode; approve channels and safety rules first. |
| Approved Help Centre articles | Product/Support | pending | Search/content shell exists; approved article catalogue is not connected. |
| Attachments | Backend/Operations | requires confirmation | Not implemented; define scanning, storage, authorization, retention, and download controls first. |

## E. Hardware integration and safety

| Requirement | Responsible area | Status | Evidence / remaining work |
|---|---|---|---|
| Controller connectivity and supported protocols | Hardware/Backend | requires confirmation | HTTP check-in endpoint and relay secret are implemented. Confirm actual controller, relay, network, and deployment protocol. |
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
| Database selection and migrations | Backend/Operations | partially implemented / pending | SQLite is used in Development and PostgreSQL is configurable. Development uses `EnsureCreated`; production EF migrations and rollback plan are still needed. |
| Secrets management | Operations | pending | Configuration supports secrets outside source; provision and document the host secret store. |
| HTTPS and production hosting | Operations | partially implemented / pending | HTTPS redirect and HSTS outside Development exist. Hosting, trusted proxy, headers, certificates, and health checks need deployment configuration. |
| Data Protection key persistence | Operations | pending | Development has a project-local key ring. Production needs a persistent, access-controlled protected key store. |
| Error handling and monitoring | Backend/Operations | partially implemented / pending | Safe API problem responses include correlation IDs. Configure structured redacted logs, monitoring, alerting, and production exception review. |
| Backups and recovery | Operations | pending | Define and exercise database, key, payment, and support recovery. |
| Rate limiting | Backend/Operations | partially implemented / pending | Authentication and device check-in are rate limited. Add suitable limits to other sensitive mutations and review partitioning. |
| Privacy and retention policies | Product/Operations | requires confirmation | Define collection purpose, retention/deletion, export, and telemetry/payment data handling. |
| Production configuration and deployment checks | Operations | pending | Configure environment-specific secrets/database/key store/logging, migrations, health checks, and smoke/security checks. |

## Current repository evidence and handover

- The latest customer portal integration is commit `f0bf097` (`Wire the customer portal to live data`). It binds customer dashboard, device, monitoring, alert, subscription, and payment-history pages to persisted, owner-scoped data.
- NuGet's transitive dependency audit reports no vulnerable packages after the dependency update. `dotnet build --no-restore` succeeds with zero warnings and errors. Runtime behavior was not exercised. Do not interpret **implemented** as production-approved.
- Development uses a project-local Data Protection key directory and console logging for the restricted Windows development environment. These are development-only choices.
- See [backend-setup.md](backend-setup.md) for setup and protocol details and [backend-handover.md](backend-handover.md) for remaining work and production readiness.
