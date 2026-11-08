# Backend setup (C#)

This adds the first real backend to the Razor Pages shell: accounts, devices, device check-in, subscriptions,
Paystack payments, monitoring data, alerts, and admin controls. **It has not been compiled or run** by its author
(no .NET SDK was available). Run `dotnet build` and the checks below before relying on it.

## What exists

| Area | Where |
|---|---|
| Data model (Identity, Device, Reading, DeviceEvent, Subscription, Payment, AuditEntry) | `Data/` |
| Billing / lock rules (pure, unit-testable) | `Backend/BillingRules.cs` |
| Device check-in + event diffing | `Backend/CheckInService.cs` |
| Paystack init / verify / webhook signature | `Backend/PaystackClient.cs` |
| Verified, idempotent settlement | `Backend/PaymentService.cs` |
| HTTP endpoints | `Backend/Endpoints.cs` |
| DI, Identity, cookies, rate limits, seeding | `Backend/Startup.cs` |
| Sign in / sign up / admin sign in | `Pages/Login`, `Pages/GetStarted`, `Pages/Admin/Login` |
| Live checkout and result | `Pages/Customer/Payments/Checkout`, `Result` |

## Configuration

Never put secrets in `appsettings.json`. Use user-secrets locally and the host's secret store in production.

```
dotnet user-secrets set "Paystack:SecretKey" "sk_test_..."
dotnet user-secrets set "Device:RelaySecret" "<long random string>"
dotnet user-secrets set "Seed:AdminEmail" "you@firmlon.example"
dotnet user-secrets set "Seed:AdminPassword" "<at least 10 chars>"
dotnet user-secrets set "Demo:Enabled" "false"      # Development only: use the real backend instead of sample data
```

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

- Amount is computed server-side from `PlanCatalog` (monthly fee × months). The client sends plan id, months, idempotency key.
- Success requires Paystack verify: reference, amount (kobo) and currency must equal what the server recorded.
- Settlement is an atomic `pending → success` claim inside a transaction, so webhook + callback + page refresh credit once.
- Hardware prices are **not** charged by checkout; hardware is invoiced separately.
- A subscription lasts 30 days per paid month, with a 3-day grace period; first device assignment starts a 7-day trial.
  These numbers are placeholders for product to confirm (checklist section C).

## Not done

Support tickets, notifications, profile/security settings and attachments are still the existing disabled shells.
Customer dashboard, device, monitoring, alerts, subscription and history **pages** still render their shells; the
data is available from the JSON endpoints above and the pages need binding to it. Email confirmation, password reset
email, SMS/email alerts, a background job that raises "went offline" events, and the Admin pages' data binding are also open.
