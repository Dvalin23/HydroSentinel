# Frontend visual redesign

The redesign is being delivered in controlled stages so the public website, customer portal, and admin portal retain their existing Razor Pages routes and integration boundaries.

## Completed foundation pass

- Audited the shared public, customer, and admin layouts.
- Preserved the existing token and stylesheet architecture.
- Removed the large customer development banner from the application flow.
- Added a compact `Demo data` disclosure in the customer header that links to Development demo controls.
- Preserved server-side Development gating and all demo safety text.
- Documented the shared design-system foundations in `docs/design-system.md`.

## Remaining staged work

The next passes should refine the public marketing pages, then bring the remaining customer and admin secondary pages to the same visual hierarchy. Each pass must be build-verified and checked at desktop, tablet, and mobile widths. No redesign pass may invent product capabilities, customer records, telemetry, payment outcomes, or production authorization.
