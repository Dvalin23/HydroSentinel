# HydroSentinel Visual Redesign — Stage A

## Objective

Create a unified premium Firmlon HydroSentinel design foundation across the existing ASP.NET Core Razor Pages application, beginning with the shared public, customer, and admin layouts. Preserve routes, page models, forms, demo boundaries, and existing business meaning.

## Direction

Premium industrial IoT SaaS: deep navy operational chrome, cool white workspace surfaces, Firmlon green interaction accents, water blue for monitoring context, restrained amber/red states, crisp modern typography, deliberate density, and subtle depth. Avoid generic card grids, excessive gradients, and uniform rounded rectangles.

## Scope for this pass

- Audit and strengthen shared design tokens and global interaction states.
- Remove the large customer `demo-mode-banner` from the visual flow. Keep a compact, neutral/gently green demo disclosure in the customer header or workspace so simulated data remains clearly identified.
- Preserve the development-only behavior and production boundaries.
- Standardize public header/footer, customer shell, admin shell, buttons, forms, cards, tables, focus states, and responsive breakpoints where the current CSS already provides hooks.
- Improve the visual hierarchy of the current customer and admin dashboards without inventing data.
- Reuse local approved image assets; do not add external URLs or fake product photography.
- Add documentation describing the design system and this staged rollout.

## Constraints

Use the existing Razor Pages architecture and CSS files. Do not introduce a frontend framework or a new icon dependency. Use existing inline SVG icons or simple CSS treatments. Do not remove safety disclosures, antiforgery protections, demo-mode server gating, or backend integration-required states. Run `dotnet build` before completion.
