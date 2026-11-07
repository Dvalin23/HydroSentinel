# Website quality upgrade handover

## Implemented

- Shared public, auth, and customer layouts now expose a Development-only test banner and load shared Firmlon-green hover/focus/active/disabled refinements.
- Existing inline SVG icons remain the consistent local icon approach. Decorative SVGs use `aria-hidden`; icon-only controls retain accessible labels.
- Customer dashboard hierarchy and unavailable states remain truthful; quick actions and contextual imagery are retained without adding fabricated account, device, telemetry, alert, or payment data.
- `Services/PlanCatalog.cs` is the single presentation catalogue for flyer-sourced prices.

## Flyer source and pricing

Source: `wwwroot/images/flyer.jpeg`.

| Plan | One-time hardware/setup | Recurring | Allowance |
|---|---:|---:|---|
| Basic | ₦79,000 | ₦5,000/month | 1 unit |
| Pro | ₦169,000 | ₦15,000/month | 3 units |
| Max | ₦749,000 | ₦50,000/month | More than 3 units |

The flyer also shows “10% OFF + FIRST MONTH FREE” and “LAUNCHING: 15TH NOVEMBER”. The UI labels this as flyer-supplied/pending commercial confirmation. It does not apply a discount, activate a promotion, or change checkout totals.

## Development form testing

`IFormTestingMode` is registered in `Program.cs` and returns enabled only when `IWebHostEnvironment.IsDevelopment()` is true. The public, auth, and customer layouts show:

> DEVELOPMENT TEST MODE — Form submissions are temporary and are not saved.

Support creation, profile, password-rule validation, notification-preference, and public contact forms can validate in Development and return an explicit temporary test confirmation. Values are discarded after the response. Passwords are cleared and never echoed or stored. No localStorage, files, database, email, payment, device, or external API operation is used. Device/safety controls, authentication, payment initiation, and subscription activation remain unavailable.

To verify locally, run `dotnet run --environment Development`, inspect the banner, submit invalid values to see validation, then submit valid non-sensitive test values and confirm the temporary message. Run with `--environment Production` to confirm the banner is absent. Browser verification remains a separate manual step if no browser automation is available.
