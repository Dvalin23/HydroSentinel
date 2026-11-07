# Firmlon HydroSentinel design system

The application uses a shared industrial-IoT visual language: deep navy navigation, cool neutral workspaces, Firmlon green for primary interaction, water blue for monitoring context, and restrained warning/error colors.

## Foundations

- Public pages use the global `site.css`, `layout-polish.css`, and page-specific styles.
- Customer pages use `customer-design-tokens.css`, `customer-layout.css`, and the page-specific customer stylesheets.
- Admin pages use the dedicated `admin.css` and `_AdminLayout`/`_AdminSidebar` components.
- Inline SVG icons are used for navigation and actions; decorative icons are hidden from assistive technology.
- Focus-visible outlines, reduced-motion handling, responsive grids, and overflow-safe tables are required for interactive components.

## Interaction rules

Primary actions use Firmlon green with a darker hover state. Secondary actions use neutral surfaces with a green border/text hover state. Disabled actions remain visibly unavailable. Status is communicated through text and indicators, not color alone.

## Demo disclosure

Development Demo Mode remains server-gated by `IWebHostEnvironment.IsDevelopment()`. The customer portal uses a compact header indicator linking to demo controls rather than a full-width banner. Simulated records remain explicitly labelled and are never presented as production data.

## Responsive behavior

Customer and admin sidebars become drawer navigation on narrow screens. Content grids collapse progressively, tables remain usable through overflow containers, and focus outlines must not be clipped by panel shapes.
