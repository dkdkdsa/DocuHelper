const sessions = new WeakMap();

function focusables(card) {
    return Array.from(card.querySelectorAll('button:not([disabled]), input:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])'));
}

function place(card, x, y, anchorId) {
    const anchor = anchorId ? document.getElementById(anchorId) : null;
    let left = x;
    let top = y;
    if (anchor) {
        const rect = anchor.getBoundingClientRect();
        left = rect.left;
        top = rect.bottom + 4;
    }

    card.style.maxHeight = (window.innerHeight - 16) + 'px';
    const width = card.offsetWidth;
    const height = Math.min(card.offsetHeight, window.innerHeight - 16);
    left = Math.max(8, Math.min(left, window.innerWidth - width - 8));
    top = Math.max(8, Math.min(top, window.innerHeight - height - 8));
    card.style.left = left + 'px';
    card.style.top = top + 'px';
}

export function attach(card, dotnet, searchId, x, y, anchorId) {
    const session = { dotnet };
    session.key = (e) => {
        if (e.key === 'Escape') {
            e.preventDefault();
            e.stopPropagation();
            dotnet.invokeMethodAsync('CloseFromKeyboard');
            return;
        }

        if (e.key !== 'Tab')
            return;

        const items = focusables(card);
        if (items.length === 0)
            return;

        const first = items[0];
        const last = items[items.length - 1];
        const inside = card.contains(document.activeElement);
        if (e.shiftKey) {
            if (!inside || document.activeElement === first) {
                e.preventDefault();
                last.focus();
            }
        } else if (!inside || document.activeElement === last) {
            e.preventDefault();
            first.focus();
        }
    };

    card.addEventListener('keydown', session.key);
    sessions.set(card, session);
    place(card, x, y, anchorId);
    const search = document.getElementById(searchId);
    if (search)
        search.focus();
    else
        focusables(card)[0]?.focus();
}

export function detach(card) {
    const session = sessions.get(card);
    if (!session)
        return;

    card.removeEventListener('keydown', session.key);
    sessions.delete(card);
}
