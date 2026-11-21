// ============================================================================
// StayNest Global Interactions: Theme, Currency, Notifications, Wishlist, PWA
// ============================================================================
document.addEventListener('DOMContentLoaded', () => {
    // 1. Register PWA Service Worker
    if ('serviceWorker' in navigator) {
        navigator.serviceWorker.register('/sw.js').catch(() => {});
    }

    // 2. Dark / Light Theme Toggle (Accessible aria-pressed sync)
    const themeBtn = document.getElementById('themeToggle');
    if (themeBtn) {
        themeBtn.addEventListener('click', async () => {
            const html = document.documentElement;
            const next = html.getAttribute('data-theme') === 'dark' ? 'light' : 'dark';
            html.setAttribute('data-theme', next);
            themeBtn.setAttribute('aria-pressed', next === 'dark' ? 'true' : 'false');
            themeBtn.innerHTML = next === 'dark'
                ? '<i class="bi bi-sun-fill" aria-hidden="true"></i>'
                : '<i class="bi bi-moon-stars" aria-hidden="true"></i>';
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

    // 5. Global Wishlist AJAX Toggle (preserves returnUrl & updates aria-pressed)
    document.body.addEventListener('click', async (e) => {
        const btn = e.target.closest('.sn-wish-btn');
        if (!btn) return;
        e.preventDefault();
        e.stopPropagation();

        const propId = btn.dataset.propertyId;
        btn.disabled = true;
        try {
            const res = await fetch(`/Property/ToggleWishlist?propertyId=${propId}`, { method: 'POST' });
            if (res.redirected) {
                const ret = encodeURIComponent(window.location.pathname + window.location.search);
                window.location.href = `/Account/Login?returnUrl=${ret}`;
                return;
            }
            if (!res.ok) return;
            const data = await res.json();
            btn.classList.toggle('saved', data.saved);
            btn.setAttribute('aria-pressed', data.saved ? 'true' : 'false');
            const icon = btn.querySelector('i');
            if (icon) {
                icon.className = data.saved ? 'bi bi-heart-fill' : 'bi bi-heart';
            }
        } catch {
            // resilient failure
        } finally {
            btn.disabled = false;
        }
    });

    // 6. Keyboard Shortcut: "/" focuses catalog search bar (respects all editable fields)
    document.addEventListener('keydown', (e) => {
        const active = document.activeElement;
        const isEditing = active && (
            active.tagName === 'INPUT' ||
            active.tagName === 'TEXTAREA' ||
            active.tagName === 'SELECT' ||
            active.isContentEditable
        );
        if (e.key === '/' && !isEditing) {
            const searchInput = document.getElementById('searchQuery');
            if (searchInput) {
                e.preventDefault();
                searchInput.focus();
            }
        }
    });
});
