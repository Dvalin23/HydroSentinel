# Proposed Customer Portal API Contracts

These are proposals for backend-team agreement, not implemented endpoints. Names, authentication scheme, identifiers, pagination, error envelopes, and authorization rules must be confirmed before coding.

## Pricing display and test-mode boundary

The UI catalogue in `Services/PlanCatalog.cs` is transcribed from `wwwroot/images/flyer.jpeg` for display only. It is not a payment authority and does not implement an API. A backend plan catalogue must own current price, currency, promotion eligibility, tax, inventory, and checkout totals.

Development form testing is not an API contract. It is server-gated to the ASP.NET Core Development environment, discards submitted values, and must never call these proposed services or create records.

## Common conventions

- JSON over same-origin HTTPS endpoints.
- User/tenant scope derived from the authenticated server session; never trust a client-supplied owner ID.
- `ProblemDetails`-style errors with a stable `code`, user-safe `message`, field errors, and correlation ID.
- ISO-8601 UTC timestamps; explicit units and `dataFreshness` on readings.
- Cursor pagination for lists where practical.
- Idempotency key required for device registration and payment initiation.

## Proposed resources

### Dashboard overview

`GET /api/customer/dashboard` -> `{ account: { displayName? }, devicesSummary: { registeredCount?, onlineCount?, sourceStatus, freshness }, subscriptionSummary: { status?, planName?, billingPeriod?, renewalAt?, sourceStatus }, openAlertsSummary: { count?, sourceStatus, freshness }, recentActivity: [{ id, type, occurredAt, summary }], monitoringPreview: { metric?, unit?, points?, lastUpdatedAt?, freshness, sourceStatus } }`

This dashboard contract is proposed and requires backend-team agreement. No provider, API client, fixture, telemetry data, account data, subscription data, alerts, activity, or chart data is implemented in Stage 3. The UI must render unavailable, empty, offline, and stale states from explicit source/freshness fields and must never infer live values from missing responses.

### Devices

`GET /api/customer/devices?query=&status=&cursor=` -> `{ items: [{ id, name, serialNumberMasked, status, lastSeenAt, stale }], nextCursor }`

`POST /api/customer/devices` with `{ serialNumber, verificationCode? }` -> `{ verificationStatus, device? }`. Verification must be server-side and must not expose secrets.

`GET /api/customer/devices/{id}` -> `{ id, name, status, installationLabel, lastSeenAt, capabilities, access }`.

Stage 4 device-management UI is now present, but these contracts remain proposed and unimplemented. The list response must support authorized query/filtering, stable pagination, and explicit source/freshness metadata. Registration must validate the serial number or approved activation code server-side, verify eligibility and customer ownership, and return a pending, verified, rejected, or unavailable result without exposing secrets. Detail responses must include only fields and capabilities permitted for the current customer; unavailable, offline, stale, forbidden, not-found, validation, conflict, timeout, and service-failure responses must map to explicit UI states. No device records, telemetry, timestamps, locations, statuses, or verification results are hardcoded in the current UI.

### Monitoring (proposed, unimplemented)

`GET /api/customer/devices/{id}/readings?from={ISO-8601 UTC}&to={ISO-8601 UTC}&metric={waterLevel|pressure|pumpStatus|flowRate|energyUsage}&cursor={cursor}&limit={1..200}` -> `{ deviceId, points: [{ timestamp, metric, value, unit, quality }], range: { from, to }, freshness: { state, lastUpdatedAt, ageSeconds? }, sourceStatus: live|delayed|stale|offline|unavailable|fixture, nextCursor? }`.

The backend must define which metrics the hardware actually supports. Numeric readings require an explicit unit such as `percent`, `bar`, `litresPerMinute`, or `kilowattHours`; status metrics must use an agreed enum rather than numeric inference. `timestamp` and `lastUpdatedAt` are ISO-8601 UTC. `quality` should identify verified, estimated, invalid, or missing values. The UI must never interpret a missing value as zero.

Authorization: derive customer and device ownership from the authenticated server session; reject cross-customer access. Validate that `from` precedes `to`, enforce an agreed maximum range, metric allow-list and page size. Return 401 unauthorised, 403 access denied, 404 device not found, 422 invalid range/metric, 429 rate limited, 504 provider timeout, and 503 offline/service unavailable. A stale response must include freshness metadata and must not be presented as current. Development fixtures require an explicit Development-only marker and must never be returned as live production data.

### Alerts (proposed, unimplemented)

`GET /api/customer/alerts?severity={critical|warning|info}&status={open|acknowledged|resolved}&unread={true|false}&deviceId={id}&query={text}&cursor={cursor}&limit={1..100}` -> `{ items: [{ id, severity, type, deviceId, deviceName?, description, occurredAt, status, unread, actions: { canView, canAcknowledge } }], nextCursor?, sourceStatus, freshness: { state, lastUpdatedAt? } }`.

`GET /api/customer/alerts/{id}` -> `{ id, severity, type, deviceId, deviceName?, occurredAt, description, status, unread, relatedReadings?, actions: { canAcknowledge, canMarkRead }, sourceStatus, freshness }`.

`POST /api/customer/alerts/{id}/acknowledge` -> `{ id, status, acknowledgedAt, acknowledgedBy?, sourceStatus }`, only when the backend supports acknowledgement and the authenticated customer is authorised for that alert. The server must make acknowledgement idempotent and audit the actor.

Alert timestamps are ISO-8601 UTC. Severity and status values require an agreed enum. `sourceStatus` and `freshness` must distinguish live, delayed, stale, offline and unavailable. Return 401 unauthorised, 403 access denied, 404 alert not found, 409 already resolved/concurrent update, 422 invalid filter or action, 429 rate limited, 504 provider timeout, and 503 offline/service unavailable. An empty list is valid only after a successful authorised response; an unavailable response must not be rendered as an all-clear.

Stage 5 UI note: `/Customer/Monitoring`, `/Customer/Alerts`, and `/Customer/Alerts/Details` provide responsive, integration-ready surfaces for these proposed contracts. No telemetry or alert endpoint is implemented. Refresh, device/time-range selectors, filtering, acknowledgement, charts, and alert records remain unavailable and are visibly disabled or labelled as unavailable. Missing data is never interpreted as zero or an all-clear.

### Subscriptions and plans

`GET /api/customer/subscription` -> `{ status, planId?, planName?, billingPeriod?, startedAt?, renewalAt?, coveredDeviceCount?, sourceStatus, freshness }`.

`POST /api/customer/subscription/change` -> `{ planId, billingPeriod, idempotencyKey }` only if plan changes are approved; returns a pending server-owned operation and never activates benefits before verified payment.

`GET /api/customer/plans` -> `{ catalogVersion, termsVersion, plans: [{ id, name, description, oneTimeAmount?, recurringAmount?, currency?, billingPeriod?, deviceAllowance?, features[], limitations[], available }] }`.

No price, discount, renewal, or feature should be hardcoded in the portal if a canonical backend source exists.

### Payments

`POST /api/customer/payments/checkout` with `{ planId, billingPeriod, idempotencyKey }` -> `{ paymentId, providerReference?, status: pending|failed, redirectUrl? }`. The server re-prices from the approved catalog and never accepts a client total or secret.

`GET /api/customer/payments/{reference}` -> `{ paymentId, reference, status: pending|verified|failed|cancelled|unableToVerify, amount, currency, planId?, verifiedAt?, failureCode?, sourceStatus }`. A reference is only a lookup key; provider verification belongs to the server payment service.

`GET /api/customer/payments?cursor=&status=&query=` -> `{ items: [{ id, reference, paidAt?, description?, amount, currency, status, method?, receiptAvailable }], nextCursor, sourceStatus }` scoped to the authenticated customer.

`GET /api/customer/payments/{id}/receipt` -> an authorized receipt resource only when available. The service must enforce customer ownership.

The client must never mark a payment successful from a redirect alone. Paystack or another provider is not currently configured in this project.

Payment initiation requires antiforgery protection for same-origin browser posts, idempotency, server-side pricing, provider handoff, and safe pending/error responses. Paystack secrets remain server-only. Webhooks must be authenticated, idempotent, and reconciled. The current project has no payment provider or endpoint; all Stage 6 payment actions are disabled.

### Support

`GET /api/customer/support/tickets?status=&cursor=`; `POST /api/customer/support/tickets` with validated subject/category/message; `GET /api/customer/support/tickets/{id}`; `POST /api/customer/support/tickets/{id}/replies`.

Ticket confirmation is shown only after the server returns a ticket ID.

### Profile and settings

`GET/PATCH /api/customer/profile`; `POST /api/customer/security/password`; `GET/PATCH /api/customer/notification-preferences`.

Password changes require the existing security provider, current-password verification where appropriate, antiforgery protections, rate limiting, and safe error handling.

### Support, notifications, profile, security, and help (proposed, unimplemented)

These contracts require backend-team agreement and are not implemented by the current Razor Pages application.

- `GET /api/customer/support/tickets?status=&query=&cursor=` returns authenticated-customer ticket summaries `{ items: [{ id, reference, subject, category, status, priority?, createdAt, updatedAt? }], nextCursor, sourceStatus }`. The server derives customer ownership, validates filters, and returns explicit unavailable, timeout, unauthorized, forbidden, and failure responses.
- `POST /api/customer/support/tickets` accepts `{ subject, category, deviceId?, description }` with antiforgery protection, validation, rate limiting, ownership checks for related devices, and an idempotency key. A success response must contain a persisted ticket ID/reference; the UI must not show confirmation before that response. Attachments require a separate approved contract covering type, size, malware scanning, storage, retention, and authorization.
- `GET /api/customer/support/tickets/{id}` returns only an owned ticket `{ id, reference, subject, description, category, status, priority?, device?, createdAt, updatedAt?, messages? }`. Query IDs are never authorization. `POST /api/customer/support/tickets/{id}/replies` is optional and must return a persisted reply or a safe failure.
- `GET /api/customer/notifications?category=&unread=&cursor=` returns `{ items: [{ id, title, message, category?, severity?, createdAt, unread, destination? }], nextCursor, sourceStatus, freshness? }`. Destinations must be server-approved or safely mapped; unavailable is not an empty/all-clear response.
- `POST /api/customer/notifications/{id}/read` and `POST /api/customer/notifications/read-all` are optional idempotent operations. The UI must only enable them after backend support exists and must not claim persistence from local state.
- `GET/PATCH /api/customer/profile` returns/updates only approved fields such as `{ fullName?, email?, phone?, company? }`. The server owns identity, verified-contact rules, authorization, validation, audit, and concurrency handling. Email changes may require a separate verification flow.
- `POST /api/customer/security/password` accepts `{ currentPassword, newPassword, confirmPassword }` over an authenticated antiforgery-protected request. The identity provider must enforce policy, rate limits, safe errors, session rules, and redacted logging. Passwords must never be returned or logged.
- `GET/PATCH /api/customer/notification-preferences` returns/updates only approved channels and categories. Safety-critical notification policy must be enforced server-side; unsupported channels must not be exposed as enabled capabilities.
- `GET /api/customer/help/articles?query=&category=&cursor=` returns approved, versioned article content `{ items: [{ id, title, category, summary, body, updatedAt }], nextCursor, sourceStatus }`. Draft or unreviewed product guidance must not be presented as official instructions.

The Stage 7 routes implement these response boundaries as unavailable/disabled UI states. No endpoint above is present in the current project.

## Error and fixture policy

All contracts need documented 401, 403, 404, 409, 422, 429, 500, timeout, and offline behavior. Development fixtures may implement the same DTOs behind interfaces, but pages must label them as fixtures and never present them as live pump telemetry, verified payment, or real account data.
