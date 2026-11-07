# HydroSentinel frontend demo mode

## Purpose

Development Demo Mode lets the existing Razor Pages website be explored with fictional customer data before authentication, database, Paystack, support, and hardware services are connected.

## Start

From the project directory:

```powershell
dotnet run --environment Development
```

Open `/Demo`, then choose **Open customer demo**. The customer portal also displays a `DEMO MODE` banner and account menu in Development.

The sample account is fictional: Ada Demo Customer (`ada.demo@example.test`). No real identity is created and no authentication provider is bypassed.

## Simulated features

- Dashboard summary, devices, device details, search, status filtering, add/remove demo devices.
- Illustrative monitoring values, historical readings, device selector, and time-range controls.
- Alert search/filtering, alert details, and temporary acknowledgement.
- Notifications and mark-all-as-read.
- Subscription overview and flyer-supplied plan catalogue.
- Checkout outcomes: confirmed, pending, failed, and cancelled. These never contact Paystack and never activate a real subscription.
- Temporary payment history and result pages.
- Support ticket creation and fictional ticket responses.
- Profile and notification-preference edits in temporary memory.
- Public Contact form validation and development-only confirmation.

All sample telemetry, payment records, alerts, tickets, notifications, and profile information are labelled demonstration content in the UI.

## Reset

Use **Reset data** in the demo banner or open `/Demo` and choose **Reset demo data**. The store is a singleton in server memory and resets when the process stops. Reset restores the original sample account.

## Safety boundary

`IDemoMode` is registered as a mock service, and `DemoMode.Enabled` is true only when `IWebHostEnvironment.IsDevelopment()` is true. Demo routes and POST handlers return `404` when the application is not in Development. No query string, JavaScript flag, localStorage value, or browser action can enable the mode in Production.

The demo store does not write to a database or file, call external APIs, send email, store passwords, collect card details, contact hardware, or expose pump-control commands.

## Backend replacement

The demo page models currently depend on `IDemoMode` only at the presentation/integration boundary. Backend implementations can replace the store with authenticated customer, device, telemetry, alert, billing, support, and notification services. Ownership checks, trusted pricing, Paystack verification, webhook idempotency, audit logging, and hardware command safety remain backend responsibilities.
