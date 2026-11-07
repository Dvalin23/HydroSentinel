# Admin Portal Premium Redesign Brief

Redesign every existing page under `Pages/Admin` and the shared admin shell in the existing Razor Pages application. This is an implementation task, not a static mockup.

## Visual direction

Create a distinctive Firmlon HydroSentinel operations console: deep navy sidebar, cool white workspace, subtle blue-grey atmospheric background, Firmlon green for active/primary states, water blue for telemetry, restrained amber/red status colors, compact page-title treatment, strong hierarchy, and refined dense layouts. It should feel like a professional industrial IoT control room, not a generic admin template.

## Scope

Improve all current admin routes and shared components:

- Login
- Dashboard
- Customers and customer details
- Devices and device details
- Monitoring
- Alerts and alert details
- Subscriptions and Plans
- Payments and payment details
- Support and support details
- Reports
- Notifications
- Settings and Profile/Security/Notifications/Integrations
- Help Centre

## Required design work

- Replace any remaining text/Unicode placeholder icons with consistent accessible inline SVG line icons.
- Improve the sidebar brand treatment, active/hover states, grouping, and mobile drawer.
- Improve the top bar with search/context, notification action, demo status, and administrator identity.
- Establish reusable page-header, metric, panel, table, filter, badge, empty/unavailable state, detail, and action styles in `wwwroot/css/admin.css`.
- Give the dashboard a strong composition: intro, meaningful metrics, network/telemetry panel, attention queue, activity/system state, and quick actions. Demo data must remain clearly labelled.
- Make tables usable and visually polished at narrow widths.
- Make Login visually premium and balanced; the demo entry action must not be an oversized block.
- Make every visible button/link either work, navigate, or be clearly unavailable.
- Add hover, focus-visible, active, disabled, reduced-motion, and responsive states.
- Use existing local images only; no external image URLs and no invented product facts.

## Safety constraints

Preserve the Development-only demo boundary, existing routes, page models, antiforgery behavior, customer portal, and public site. Do not add production authentication bypasses, real payments, hardware controls, or fabricated production records. Keep demo/unavailable notices readable but visually integrated.

Run `dotnet build` and report all files changed.
