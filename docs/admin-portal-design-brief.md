# HydroSentinel Admin Portal Design Brief

## Objective

Implement the first complete, premium Admin Portal slice inside the existing ASP.NET Core Razor Pages application. Preserve the public website and customer portal. The admin area must be visibly separate, responsive, and ready for backend integration without inventing production data.

## Routes in this slice

- `/Admin/Login`
- `/Admin/Dashboard`
- `/Admin/Customers`
- `/Admin/Devices`
- `/Admin/Monitoring`
- `/Admin/Alerts`
- `/Admin/Subscriptions`
- `/Admin/Payments`
- `/Admin/Support`
- `/Admin/Notifications`
- `/Admin/Reports`
- `/Admin/Settings`
- `/Admin/Help`

When no backend exists, routes may expose clearly labelled Development Demo Mode content only. Production must show an integration-required state or remain protected; never add a production authentication bypass.

## Visual direction

Use the supplied Admin Portal reference screens as direction: a deep navy operations sidebar, Firmlon green interaction accent, cool white/blue workspace, compact dark page-title pill, crisp typography, restrained radii and shadows, consistent line icons, and dense but breathable enterprise tables. The result should feel like an industrial control room rather than a generic SaaS template.

Use the existing logo and design tokens. Reuse `Services/PlanCatalog.cs` for the approved flyer figures. Do not add fake real-world customers, payments, telemetry, or operational health; demo records must be visibly labelled.

## UX requirements

- Dedicated admin layout and navigation; do not reuse the customer sidebar.
- Keyboard-visible focus, semantic landmarks, accessible labels, responsive mobile navigation, and no horizontal overflow.
- Every visible control must either work locally, navigate to an existing route, or be explicitly disabled/unavailable.
- Include populated, unavailable, and empty states where relevant.
- Use CSS/inline SVG line icons consistently; no emoji icons.
- Use a chart/graph treatment only for clearly labelled demo data.
- No pump-control commands, payment calls, secrets, or production data.

## Implementation boundary

Create maintainable admin PageModels and a small Development-only demo projection/service if needed. Keep backend contracts clearly documented. A later backend can replace the demo service through interfaces without changing the UI structure.
