(() => {
    const menuButton = document.querySelector('.menu-toggle');
    const navigation = document.querySelector('.primary-navigation');
    if (!menuButton || !navigation) return;

    const screenReaderLabel = menuButton.querySelector('.sr-only');
    const firstNavigationLink = navigation.querySelector('a');
    const closeNavigation = (returnFocus = false) => {
        navigation.classList.remove('is-open');
        menuButton.setAttribute('aria-expanded', 'false');
        menuButton.setAttribute('aria-label', 'Open primary navigation');
        if (screenReaderLabel) screenReaderLabel.textContent = 'Open navigation';
        if (returnFocus) menuButton.focus();
    };

    menuButton.addEventListener('click', () => {
        const isOpen = navigation.classList.toggle('is-open');
        menuButton.setAttribute('aria-expanded', String(isOpen));
        menuButton.setAttribute('aria-label', isOpen ? 'Close primary navigation' : 'Open primary navigation');
        if (screenReaderLabel) screenReaderLabel.textContent = isOpen ? 'Close navigation' : 'Open navigation';
        if (isOpen && firstNavigationLink) {
            requestAnimationFrame(() => firstNavigationLink.focus());
        }
    });
    navigation.querySelectorAll('a').forEach(link => link.addEventListener('click', () => {
        closeNavigation();
    }));
    document.addEventListener('keydown', event => {
        if (event.key === 'Escape' && navigation.classList.contains('is-open')) {
            closeNavigation(true);
        }
    });
    document.addEventListener('click', event => {
        if (navigation.classList.contains('is-open') && !navigation.contains(event.target) && !menuButton.contains(event.target)) {
            closeNavigation(true);
        }
    });

    window.addEventListener('resize', () => {
        if (window.innerWidth > 900 && navigation.classList.contains('is-open')) {
            closeNavigation();
        }
    });
})();
