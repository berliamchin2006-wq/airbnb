// ============================================================================
// StayNest Global Interactions: Theme, Currency, Notifications, Wishlist, PWA
// ============================================================================
document.addEventListener('DOMContentLoaded', () => {
    // 1. Register PWA Service Worker
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('/sw.js').catch(() => {});
    }

    // 2. Dark / Light Theme Toggle
    const themeBtn = document.getElementById('themeToggle');
    if (themeBtn) {
        themeBtn.addEventListener('click', async () => {
            const html = document.documentElement;
            const next = html.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
            html.setAttribute('data-theme', next);
            themeBtn.innerHTML = next === 'dark' ? '<i class="bi bi-sun-fill"></i>' : '<i class="bi bi-moon-stars"></i>';
            await fetch(`/Account/SetPreferences?theme=${next}`, { method: 'POST' }).catch(() => {});
        });
    }

    // 3. Currency Switcher (MYR / USD / SGD)
    const currSel = document.getElementById('currencySwitcher');
    if (currSel) {
        currSel.addEventListener('change', async () => {
            await fetch(`/Account/SetPreferences?currency=${currSel.value}`, { method: 'POST' }).catch(() => {});
            window.location.reload();
        });
    }

    // 4. Unread Notification Bell Poll
    const notifBadge = document.getElementById('notifBadge');
    if (notifBadge) {
        fetch('/Stay/UnreadCount')
            .then(r => r.ok ? r.json() : null)
            .then(data => {
                if (data && data.count > 0) {
                    notifBadge.textContent = data.count > 9 ? '9+' : data.count;
                    notifBadge.classList.remove('d-none');
                }
            })
            .catch(() => {});
    }

    // 5. Global Wishlist AJAX Toggle (delegated for AJAX-reloaded cards too)
    document.body.addEventListener('click', async (e) => {
        const btn = e.target.closest('.sn-wish-btn');
        if (!btn) return;
        e.preventDefault();
        e.stopPropagation();

        const propId = btn.dataset.propertyId;
        try {
            const res = await fetch(`/Property/ToggleWishlist?propertyId=${propId}`, { method: 'POST' });
            if (res.redirected) {
                window.location.href = '/Account/Login';
                return;
            }
            if (!res.ok) return;
            const data = await res.json();
            btn.classList.toggle('saved', data.saved);
            const icon = btn.querySelector('i');
            if (icon) {
                icon.className = data.saved ? 'bi bi-heart-fill' : 'bi bi-heart';
            }
        } catch {
            // ignore
        }
    });

    // 6. Keyboard Shortcut: "/" focuses catalog search bar
    document.addEventListener('keydown', (e) => {
        if (e.key === '/' && document.activeElement.tagName !== 'INPUT' && document.activeElement.tagName !== 'TEXTAREA') {
            const searchInput = document.getElementById('searchQuery');
            if (searchInput) {
                e.preventDefault();
                searchInput.focus();
            }
        }
    });
});
