const DEFAULT_TRIES = 30;
const RETRY_MS = 20;

function find(selector) {
    const el = document.querySelector(selector);
    if (!el || el.disabled || el.getClientRects().length === 0) return null;
    return el;
}

export function focusWhenReady(selector, tries) {
    return new Promise(resolve => {
        let left = tries > 0 ? tries : DEFAULT_TRIES;
        const step = () => {
            const el = find(selector);
            if (el) {
                el.focus();
                if (typeof el.setSelectionRange === 'function') {
                    const end = el.value.length;
                    el.setSelectionRange(end, end);
                }

                resolve(document.activeElement === el);
                return;
            }

            left--;
            if (left <= 0) {
                resolve(false);
                return;
            }

            setTimeout(step, RETRY_MS);
        };

        step();
    });
}
