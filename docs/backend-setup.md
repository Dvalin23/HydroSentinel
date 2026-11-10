# Backend setup (C#)

The repository contains the backend for accounts, devices, device check-in, subscriptions, Paystack payments,
monitoring data, alerts, and admin controls. The customer portal is wired to persisted, customer-scoped data.
`dotnet build --no-restore` succeeds with zero warnings and errors. NuGet's transitive dependency audit reports
no vulnerable packages after the dependency update. Runtime behavior has not been exercised as part of this
documentation update.

## What exists

| Area | Where |
|---|---|
| Data model (Identity, Device, Reading, DeviceEvent, Subscription, Payment, AuditEntry) | `Data/` |
| Billing / lock rules (pure, unit-testable) | `Backend/BillingRules.cs` |
| Device check-in + event diffing | `Backend/CheckInService.cs` |
| Paystack init / verify / webhook signature | `Backend/PaystackClient.cs` |
| Verified, idempotent settlement | `Backend/PaymentService.cs` |
| Recurring monthly enrollment, verification refunds, renewal settlement and status | `Backend/RecurringBillingService.cs` |
| HTTP endpoints | `Backend/Endpoints.cs` |
| DI, Identity, cookies, rate limits, seeding | `Backend/Startup.cs` |
| Sign in / sign up / admin sign in | `Pages/Login`, `Pages/GetStarted`, `Pages/Admin/Login` |
| Live checkout and result | `Pages/Customer/Payments/Checkout`, `Result` |

## Configuration

Never put secrets in `appsettings.json`. Use user-secrets locally and the host's secret store in production.

```
dotnet user-secrets set "Paystack:SecretKey" "sk_test_..."
dotnet user-secrets set "Paystack:PlanCodes:basic" "PLN_test_basic"
dotnet user-secrets set "Paystack:PlanCodes:pro" "PLN_test_pro"
dotnet user-secrets set "Paystack:PlanCodes:max" "PLN_test_max"
dotnet user-secrets set "Device:RelaySecret" "<long random string>"
dotnet user-secrets set "Seed:AdminEmail" "you@firmlon.example"
dotnet user-secrets set "Seed:AdminPassword" "<at least 10 chars>"
dotnet user-secrets set "Email:PublicBaseUrl" "https://portal.example.com"
dotnet user-secrets set "Email:SmtpHost" "smtp.example.com"
dotnet user-secrets set "Email:SmtpPort" "587"
dotnet user-secrets set "Email:SmtpUsername" "mailer@example.com"
dotnet user-secrets set "Email:SmtpPassword" "<smtp credential>"
dotnet user-secrets set "Email:FromAddress" "no-reply@example.com"
dotnet user-secrets set "Demo:Enabled" "false"      # Development only: use the real backend instead of sample data
```

In Development, payment requests accept only a Paystack test secret key (`sk_test_...`). Set
`Demo:Enabled` to `false` to use the real backend checkout instead of the simulated demo. Production accepts
only a live secret key (`sk_live_...`). Keep the API secret in user-secrets locally and never commit it.
The payment callback is built from `Paystack:PublicBaseUrl` when supplied, or from the current request origin.
Configure the webhook URL as `https://<public-host>/api/paystack/webhook` in the matching Paystack dashboard
mode. Paystack cannot send webhook requests to `localhost`; use a public HTTPS development tunnel or deployed
test environment to verify webhooks. Test and live dashboard modes have separate keys and webhook settings.

Recurring billing requires three Paystack monthly plans in the same mode as the configured key. Create plans
with NGN 5,000/month (Basic), NGN 15,000/month (Pro), and NGN 50,000/month (Max), without an invoice limit,
then store each returned plan code under `Paystack:PlanCodes:basic`, `:pro`, and `:max` using user-secrets or
environment secrets. Production uses separate live plan codes. Checkout checks plan interval, currency, and
amount with Paystack before accepting an enrollment. Do not use plan codes created in another Paystack mode.

The live flow asks the customer to authorize a clearly disclosed NGN 50.00 card verification payment. After
Paystack confirms a reusable card authorization, the app requests a refund and creates the monthly subscription
with its first charge at the end of the customer's current trial or paid period. Refunds are tracked as asynchronous
Paystack refunds; the webhook must include `refund.pending`, `refund.processing`, `refund.needs-attention`,
`refund.failed`, and `refund.processed`. Card numbers and CVVs never pass through this app. Paystack does not
automatically retry failed subscription charges; the app keeps the existing three-day access grace period and
records `invoice.payment_failed`. Customers cancel or update their card on Paystack's hosted subscription page;
cancellation stops renewal while local access continues through `PaidUntil`.

Account email delivery uses SMTP with STARTTLS on port 587. Set `Email:PublicBaseUrl` to the public HTTPS origin
without a path. Registration requires a configured mail sender and a confirmation link; password reset and email
changes use expiring Identity tokens. Keep SMTP credentials in user-secrets locally and the host secret store in
production. Do not set `Email:EnableSsl` to false.

Production also needs `Database:Provider=Postgres`, `ConnectionStrings:Default`, `Paystack:PublicBaseUrl`
(your public https origin), a persistent Data Protection key store (see `backend-handover.md`), and HTTPS.

## Database

Development uses `EnsureCreated` on SQLite. For production create a real migration (not done here: it needs the SDK):

```
dotnet tool install --global dotnet-ef
dotnet ef migrations add Initial
dotnet ef database update
```

Timestamps are stored as UTC ticks (a `DateTimeOffset` converter) so queries work on SQLite and PostgreSQL.

## Remote lock is OFF by default

The integration checklist marks "Supported remote commands" as *requires confirmation* and "Safety interlocks and
command protection" as **blocked**. So `Features:RemoteLockEnabled` is `false`. While false:

- `POST /api/admin/devices/{id}/lock` returns 403 `remote_lock_disabled`.
- Check-in always answers `OK,0,<time>` — no device is ever told to lock, including for an expired subscription.

Turn it on only after the team approves it. What the lock does: it can only stop the pump (never start it), takes effect
at the unit's next poll, and the unit stores the lock in EEPROM so a power cut does not clear it. Device-side interlocks,
command expiry and replay protection (checklist item E) are still to do on the firmware side.

## Device protocol

`GET /api/device/checkin?id=<serial>&k=<device key>&w=<L|M|F>&p=<0|1>&m=<0|1>&f=<fault>&lk=<0|1>&b=<battery %>&s=<csq>&pc=<fill %>&pr=<pressure centibar>`
with header `X-Relay-Secret`. Reply `OK,<lock 0|1>,<unix time>` or `ERR`. On `ERR` the unit keeps its last state.
`pc` is measured fill 0-100 and `pr` is line pressure in hundredths of a bar (215 = 2.15 bar); both are optional so
older firmware keeps working. Out-of-range values are stored as "not reported", never as zero.
The key is shown once when an admin registers the unit (`POST /api/admin/devices`) or when a customer claims it
(`POST /api/customer/devices/claim` with the serial and key); only its SHA-256 is stored.

## API summary

Customer (cookie auth, role `Customer`; POSTs need the `RequestVerificationToken` header from `/api/customer/antiforgery`):
`GET devices`, `devices/{id}`, `devices/{id}/readings`, `alerts`, `plans`, `subscription`, `payments`, `payments/{reference}`;
`POST devices/claim`, `alerts/{id}/acknowledge`, `payments/checkout`.

Admin (role `Admin`, same antiforgery scheme): `GET devices`, `payments`, `subscriptions`, `audit`;
`POST devices`, `devices/{id}/rotate-key`, `devices/{id}/assign`, `devices/{id}/lock`.

Public: `POST /api/paystack/webhook` (HMAC-SHA512 signature), `GET /pay/callback` (verifies server-side, then redirects), `GET /api/device/checkin`.

## Payment rules implemented

- Monthly plan prices are checked against Paystack's configured plan code at enrollment; catalog prices remain the source for expected amounts.
- Live enrollment requires explicit consent to the NGN 50.00 verification payment and recurring plan terms. The verification refund status is shown in billing history.
- Existing prepaid payment references still settle idempotently, but customer checkout now starts monthly recurring billing only.
- Success requires Paystack verify: reference, amount (kobo) and currency must equal what the server recorded.
- Settlement is an atomic `pending → success` claim inside a transaction, so webhook + callback + page refresh credit once.
- Hardware prices are **not** charged by checkout; hardware is invoiced separately.
- First device assignment starts the existing 7-day trial; subscription billing begins after that trial or any existing paid-through period.
- Renewal success extends access by one calendar month. Failed renewals keep the existing 3-day grace period. Customer cancellation preserves access through the current `PaidUntil` date.

## Not done / confirm before production

- Notification persistence/preferences, approved help articles, and attachments remain unavailable or validation-only shells.
- Customer dashboard, device, monitoring, alerts, subscription, plan, and payment-history pages are bound to the persisted customer data. Admin API endpoints exist, but the Admin pages still need their data binding reviewed.
- Registration requires email confirmation. The profile and security pages support profile edits, verified email changes, password changes, and password recovery when SMTP is configured. SMS/email alert delivery is not implemented.
- There is no background job that raises `went_offline` events; freshness is derived when data is read.
- Production needs EF migrations, a production database and secret store, persistent protected Data Protection keys, deployment/monitoring/backups, and a clean verification pass.
- Product must confirm catalogue terms and publish finalized recurring billing terms. Remote locking remains disabled pending firmware safety work and approval.
