(function () {
    const app = document.querySelector('[data-admin-app]');
    if (!app) return;
    const sidebar = app.querySelector('[data-admin-sidebar]');
    const overlay = app.querySelector('[data-admin-overlay]');
    const menu = app.querySelector('[data-admin-menu]');
    const close = app.querySelector('[data-admin-close]');
    if (!sidebar || !overlay || !menu || !close) return;
    const setOpen = (open) => { sidebar.classList.toggle('is-open', open); overlay.hidden = !open; menu.setAttribute('aria-expanded', String(open)); if (open) close.focus(); else menu.focus(); };
    menu.addEventListener('click', () => setOpen(true));
    close.addEventListener('click', () => setOpen(false));
    overlay.addEventListener('click', () => setOpen(false));
    document.addEventListener('keydown', e => { if (e.key === 'Escape') setOpen(false); });
})();
