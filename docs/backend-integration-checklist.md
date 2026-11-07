# HydroSentinel Customer Portal Backend Integration Checklist

This checklist is the handover boundary for the customer portal. The current Razor Pages project is a frontend and validation shell; no item is marked complete unless repository or runtime evidence supports it.

Status values: **confirmed complete**, **pending**, **blocked**, or **requires confirmation**.

## A. Identity and access

| Requirement | Responsible area | Status | Dependencies and acceptance criteria |
|---|---|---|---|
| Registration and sign-in | Backend | pending | Implement approved Identity/provider flow, validation, duplicate-account handling, email verification, secure cookies, and authenticated redirect tests. |
| ASP.NET Core Identity or approved provider | Backend/Product | requires confirmation | Select the provider and document claims, tenant/customer scope, password policy, MFA, recovery, and session lifetime. |
| Password recovery and password changes | Backend | pending | Use protected, expiring reset tokens; verify current password where required; rate-limit; never log or return passwords. |
| Customer authorization and ownership checks | Backend | pending | Derive customer scope from the authenticated server session; reject cross-customer device, ticket, alert, subscription, and payment access. |
| Session and cookie security | Backend/Operations | pending | HTTPS-only, Secure, HttpOnly, SameSite, appropriate expiry and invalidation; verify antiforgery behavior for browser POSTs. |
| Audit logging | Backend/Operations | pending | Record security and customer mutations with actor, time, correlation ID, outcome, and redaction. |

## B. Customer and device management

| Requirement | Responsible area | Status | Dependencies and acceptance criteria |
|---|---|---|---|
| Customer profile persistence | Backend | pending | Implement authenticated GET/PATCH with field validation, verified-contact rules, authorization, and truthful save responses. |
| Device registration and ownership verification | Backend/Hardware | pending | Validate serial/activation data, eligibility, ownership and conflicts; return pending/verified/rejected/unavailable states. |
| Device inventory and details | Backend | pending | Return only customer-owned records with stable IDs, masked identifiers where appropriate, capabilities and access state. |
| Device status and last-seen timestamps | Backend/Hardware | pending | Define status enum, UTC timestamps and freshness/staleness semantics. |
| Telemetry history and freshness | Backend/Hardware | pending | Confirm supported metrics, units, quality, timestamps, retention and offline/stale behavior; never infer missing values as zero. |
| Alerts and acknowledgement | Backend/Hardware/Product | pending | Define alert severity/status, ownership, unread state and idempotent acknowledgement only if product policy supports it. |

## C. Subscriptions and payments

| Requirement | Responsible area | Status | Dependencies and acceptance criteria |
|---|---|---|---|
| Approved plan catalogue and pricing | Product/Backend | requires confirmation | Approve plan IDs, descriptions, allowances, currency, one-time/recurring prices, billing terms and eligibility. |
| Subscription persistence and lifecycle | Backend | pending | Persist pending/active/expired/cancelled states and dates; enforce customer scope and lifecycle transitions. |
| Server-side payment initialization | Backend | pending | Re-price from the trusted catalogue; accept no client total; require antiforgery and idempotency; keep provider secrets server-side. |
| Paystack transaction verification | Backend | pending | Verify reference, amount, currency, customer and order/subscription with Paystack before any benefit activation. |
| Authenticated and idempotent webhooks | Backend/Operations | pending | Authenticate signatures, deduplicate delivery, reconcile safely, and audit state changes. |
| Payment history and receipts | Backend | pending | Return only customer-owned records; provide status, currency, method and authorized receipt links when available. |
| Subscription activation after verified payment | Backend | pending | Activate only after provider verification and business-rule reconciliation; expose pending/failure/cancelled states honestly. |
| Billing authorization and access rules | Backend | pending | Enforce ownership and permitted plan-change/refund/cancellation operations server-side. |

## D. Support and notifications

| Requirement | Responsible area | Status | Dependencies and acceptance criteria |
|---|---|---|---|
| Ticket creation, ownership, replies and status transitions | Backend/Support | pending | Persist ticket before confirmation; scope reads/replies to the customer; define category, priority, status and idempotency. |
| Notification delivery and persistence | Backend/Operations | pending | Define supported channels and event categories; persist delivery/read state and safe retry behavior. |
| Read/unread state | Backend | pending | Implement authenticated mark-read operations; do not treat local UI changes as persistence. |
| Supported notification preferences | Product/Backend | requires confirmation | Approve channels/categories and safety-critical rules before enabling controls. |
| Approved Help Centre articles | Product/Support | pending | Review, version and publish only verified setup, troubleshooting, safety and billing content. |
| Attachments | Backend/Operations | requires confirmation | Do not enable until type/size limits, authorization, malware scanning, storage, retention and download controls exist. |

## E. Hardware integration and safety

| Requirement | Responsible area | Status | Dependencies and acceptance criteria |
|---|---|---|---|
| Controller connectivity and supported protocols | Hardware/Backend | requires confirmation | Confirm controller model, protocol, gateway/cloud path, authentication and connection lifecycle. |
| Device API, MQTT broker, gateway or cloud service | Hardware/Backend | requires confirmation | Name the authoritative service and document availability, retry, timeout and ownership behavior. |
| Device identity and credential management | Hardware/Backend | pending | Use device credentials/keys with rotation, revocation, least privilege and no client exposure. |
| Telemetry schema, units and reporting frequency | Hardware/Product | requires confirmation | Approve metric enum, units, quality, timestamp source, sampling/reporting frequency and retention. |
| Offline and stale-device semantics | Hardware/Backend | requires confirmation | Define thresholds and UI/API states for delayed, stale, offline and unknown data. |
| Supported remote commands | Hardware/Product | requires confirmation | Do not expose pump controls until supported commands, permissions and failure behavior are approved. |
| Safety interlocks and command protection | Hardware/Backend | blocked | Require device-side interlocks, authorization, expiry, replay protection, audit trail and safe failure behavior. |
| Firmware update process and security | Hardware/Operations | requires confirmation | Define signed artifacts, rollout/rollback, device authorization, compatibility and recovery procedures. |

## F. Operations and deployment

| Requirement | Responsible area | Status | Dependencies and acceptance criteria |
|---|---|---|---|
| Database selection and migrations | Backend/Operations | pending | Choose the production database, migration strategy, backup schedule and rollback procedure. |
| Secrets management | Operations | pending | Use an approved secret store; never commit provider keys, credentials or encryption keys to the repository. |
| HTTPS and production hosting | Operations | pending | Configure certificates, secure headers, trusted proxy behavior, host restrictions and deployment health checks. |
| Data Protection key persistence | Operations | pending | Production must use a persistent, access-controlled key store shared appropriately by instances. The current development-only project-local key ring is not suitable for production. |
| Error handling and monitoring | Backend/Operations | pending | Use safe ProblemDetails responses, correlation IDs, structured redacted logs, alerts and dashboards. |
| Backups and recovery | Operations | pending | Test database, key, payment and support recovery objectives and restoration procedures. |
| Rate limiting | Backend/Operations | pending | Apply limits to authentication, registration, device verification, payment, support and notification mutations. |
| Privacy and retention policies | Product/Operations | requires confirmation | Define collection purpose, retention/deletion, customer export, payment/support records and telemetry privacy rules. |
| Production configuration and deployment checks | Operations | pending | Validate environment-specific settings, migrations, secrets, HTTPS, logging, health checks and smoke tests before release. |

## Current repository evidence

- Razor Pages compile successfully with `dotnet build` and zero warnings/errors after the QA fix.
- The portal pages intentionally show unavailable/integration-required states and do not contain customer, device, telemetry, payment, ticket or notification fixtures presented as live data.
- `Program.cs` uses a Development-only console logger and project-local Data Protection XML repository because this restricted Windows profile cannot write the EventLog source or the default user key directory. The generated `.data-protection/development-keys/` directory is ignored by source control. Production configuration is unchanged and still requires an approved persistent protected key store.
- No real authentication, database, customer API, device service, telemetry service, alert service, support service, notification service or payment provider is implemented.
