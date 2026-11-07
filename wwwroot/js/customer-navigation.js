(() => {
  const app = document.querySelector('[data-customer-app]');
  const sidebar = document.querySelector('[data-customer-sidebar]');
  const menu = document.querySelector('[data-customer-menu]');
  const overlay = document.querySelector('[data-customer-overlay]');
  const collapse = document.querySelector('[data-customer-collapse]');
  if (!app || !sidebar) return;
  const mobile = () => window.matchMedia('(max-width: 900px)').matches;
  const closeDrawer = (focus = false) => { sidebar.classList.remove('is-open'); overlay.hidden = true; menu?.setAttribute('aria-expanded', 'false'); if (focus) menu?.focus(); };
  const openDrawer = () => { sidebar.classList.add('is-open'); overlay.hidden = false; menu?.setAttribute('aria-expanded', 'true'); sidebar.querySelector('a')?.focus(); };
  menu?.addEventListener('click', () => sidebar.classList.contains('is-open') ? closeDrawer() : openDrawer());
  overlay?.addEventListener('click', () => closeDrawer(true));
  sidebar.querySelectorAll('a').forEach(link => link.addEventListener('click', () => { if (mobile()) closeDrawer(); }));
  collapse?.addEventListener('click', () => { if (mobile()) return; const collapsed = app.classList.toggle('customer-sidebar-collapsed'); collapse.setAttribute('aria-pressed', String(collapsed)); collapse.setAttribute('aria-label', collapsed ? 'Expand sidebar' : 'Collapse sidebar'); localStorage.setItem('hydro-customer-sidebar-collapsed', String(collapsed)); });
  if (localStorage.getItem('hydro-customer-sidebar-collapsed') === 'true' && !mobile()) { app.classList.add('customer-sidebar-collapsed'); collapse?.setAttribute('aria-pressed','true'); collapse?.setAttribute('aria-label','Expand sidebar'); }
  document.addEventListener('keydown', event => { if (event.key === 'Escape' && sidebar.classList.contains('is-open')) closeDrawer(true); });
  window.addEventListener('resize', () => { if (!mobile()) closeDrawer(); });
})();
