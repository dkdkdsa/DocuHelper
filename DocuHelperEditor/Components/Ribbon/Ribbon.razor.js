const ITEM = '[data-ribbon-item]';
const MENU_ITEM = '[data-menu-item]';
const TAB = '[role="tab"]';
const PANEL = '[data-ribbon-panel]';
const STRIP = '[data-ribbon-strip]';
const ARROWS = ['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'];
const roots = new Set();
let documentBound = false;

function usable(el) {
    return !el.disabled && el.getAttribute('aria-disabled') !== 'true' && !el.closest('[inert]') && el.getClientRects().length > 0;
}

function usableIn(container, selector) {
    return Array.prototype.filter.call(container.querySelectorAll(selector), usable);
}

function center(rect) {
    return { x: rect.left + rect.width / 2, y: rect.top + rect.height / 2 };
}

function nearest(items, from, key) {
    const a = from.getBoundingClientRect();
    const ac = center(a);
    let best = null;
    let bestScore = Infinity;
    for (const el of items) {
        if (el === from) continue;
        const b = el.getBoundingClientRect();
        const bc = center(b);
        let primary;
        let secondary;
        if (key === 'ArrowLeft' || key === 'ArrowRight') {
            if (b.bottom <= a.top + 1 || b.top >= a.bottom - 1) continue;
            primary = key === 'ArrowRight' ? bc.x - ac.x : ac.x - bc.x;
            secondary = Math.abs(bc.y - ac.y);
        } else {
            if (b.right <= a.left + 1 || b.left >= a.right - 1) continue;
            primary = key === 'ArrowDown' ? bc.y - ac.y : ac.y - bc.y;
            secondary = Math.abs(bc.x - ac.x);
        }

        if (primary <= 1) continue;
        const score = primary + secondary * 3;
        if (score < bestScore) {
            bestScore = score;
            best = el;
        }
    }

    return best;
}

function setStop(container, target) {
    for (const el of container.querySelectorAll(ITEM)) {
        const wanted = el === target ? '0' : '-1';
        if (el.getAttribute('tabindex') !== wanted) el.setAttribute('tabindex', wanted);
    }
}

function ensureStop(container) {
    const items = container.querySelectorAll(ITEM);
    let stop = null;
    for (const el of items) {
        if (!stop && el.getAttribute('tabindex') === '0' && usable(el)) stop = el;
    }

    if (!stop) stop = Array.prototype.find.call(items, usable) || null;
    setStop(container, stop);
    return stop;
}

function fit(root) {
    const panel = root.querySelector(PANEL);
    if (!panel) return;
    const groups = Array.prototype.slice.call(panel.querySelectorAll('[data-ribbon-group]'));
    for (const group of groups) group.removeAttribute('data-compact');
    if (panel.scrollWidth <= panel.clientWidth) return;
    for (let i = groups.length - 1; i >= 0; i--) {
        groups[i].setAttribute('data-compact', '');
        if (panel.scrollWidth <= panel.clientWidth) return;
    }
}

function refresh(root) {
    for (const container of root.querySelectorAll(PANEL + ',' + STRIP)) ensureStop(container);
    if (root.matches(STRIP)) ensureStop(root);
    fit(root);
}

function tabKey(root, tab, e) {
    const list = root.querySelector('[data-ribbon-tabs]');
    if (!list) return false;
    const tabs = Array.prototype.slice.call(list.querySelectorAll(TAB));
    const index = tabs.indexOf(tab);
    let next = null;
    if (e.key === 'ArrowRight') next = tabs[(index + 1) % tabs.length];
    else if (e.key === 'ArrowLeft') next = tabs[(index - 1 + tabs.length) % tabs.length];
    else if (e.key === 'Home') next = tabs[0];
    else if (e.key === 'End') next = tabs[tabs.length - 1];
    else if (e.key === 'ArrowDown') {
        const panel = root.querySelector(PANEL);
        const stop = panel ? ensureStop(panel) : null;
        if (!stop) return false;
        e.preventDefault();
        stop.focus();
        return true;
    } else return false;

    e.preventDefault();
    if (next && next !== tab) {
        next.focus();
        next.click();
    }

    return true;
}

function itemKey(root, container, item, e) {
    const items = usableIn(container, ITEM);
    let next = null;
    if (ARROWS.indexOf(e.key) >= 0) {
        next = nearest(items, item, e.key);
        if (!next && e.key === 'ArrowUp' && container.matches(PANEL)) {
            next = root.querySelector(TAB + '[aria-selected="true"]');
        }
    } else if (e.key === 'Home') next = items[0];
    else if (e.key === 'End') next = items[items.length - 1];
    else return;

    if (!next) return;
    e.preventDefault();
    next.focus();
}

function menuKey(menu, target, e) {
    const items = usableIn(menu, MENU_ITEM);
    const current = target.closest(MENU_ITEM);
    if (!current) return;
    const typing = current.tagName === 'INPUT';
    let next = null;
    if (ARROWS.indexOf(e.key) >= 0) {
        if (typing && (e.key === 'ArrowLeft' || e.key === 'ArrowRight')) return;
        next = nearest(items, current, e.key);
        if (!next && e.key === 'ArrowDown') next = items[0];
        if (!next && e.key === 'ArrowUp') next = items[items.length - 1];
    } else if (e.key === 'Home' && !typing) next = items[0];
    else if (e.key === 'End' && !typing) next = items[items.length - 1];
    else return;

    if (!next || next === current) return;
    e.preventDefault();
    next.focus();
    if (next.tagName === 'INPUT') next.select();
}

function onKeyDown(root, e) {
    const target = e.target;
    if (!(target instanceof Element)) return;
    const tab = target.closest(TAB);
    if (tab && root.contains(tab)) {
        tabKey(root, tab, e);
        return;
    }

    const menu = target.closest('[data-ribbon-menu]');
    if (menu && root.contains(menu)) {
        menuKey(menu, target, e);
        return;
    }

    const item = target.closest(ITEM);
    if (!item) return;
    const container = item.closest(PANEL + ',' + STRIP);
    if (container && root.contains(container)) itemKey(root, container, item, e);
}

function onFocusIn(root, e) {
    const target = e.target;
    if (!(target instanceof Element)) return;
    const item = target.closest(ITEM);
    if (!item || !usable(item)) return;
    const container = item.closest(PANEL + ',' + STRIP);
    if (container && root.contains(container)) setStop(container, item);
}

function release(root) {
    const state = root.__ribbon;
    if (!state) return;
    root.removeEventListener('keydown', state.key);
    root.removeEventListener('focusin', state.focus);
    state.mutation.disconnect();
    state.resize.disconnect();
    root.__ribbon = null;
    roots.delete(root);
    if (roots.size === 0 && documentBound) {
        document.removeEventListener('keydown', onDocumentKey, true);
        documentBound = false;
    }
}

function onDocumentKey(e) {
    if (e.key !== 'F1' || !e.ctrlKey || e.altKey || e.shiftKey || e.metaKey) return;
    for (const root of Array.from(roots)) {
        if (!root.isConnected) {
            release(root);
            continue;
        }

        const toggle = root.querySelector('[data-ribbon-toggle]');
        if (!toggle) continue;
        const scope = root.closest('[data-region="paper"]') || root.parentElement || root;
        const active = document.activeElement;
        if (!active || !scope.contains(active)) continue;
        e.preventDefault();
        toggle.click();
        return;
    }
}

export function attach(root) {
    if (!root || root.__ribbon) return;
    const state = { width: -1 };
    state.key = e => onKeyDown(root, e);
    state.focus = e => onFocusIn(root, e);
    root.addEventListener('keydown', state.key);
    root.addEventListener('focusin', state.focus);
    roots.add(root);
    if (!documentBound) {
        document.addEventListener('keydown', onDocumentKey, true);
        documentBound = true;
    }

    state.mutation = new MutationObserver(() => refresh(root));
    state.mutation.observe(root, { childList: true, subtree: true, attributes: true, attributeFilter: ['disabled', 'aria-disabled', 'inert'] });
    state.resize = new ResizeObserver(() => {
        const width = root.clientWidth;
        if (width === state.width) return;
        state.width = width;
        refresh(root);
    });
    state.resize.observe(root.querySelector('[data-ribbon-probe]') || root);
    if (document.fonts && document.fonts.ready) {
        document.fonts.ready.then(() => {
            if (root.__ribbon === state) refresh(root);
        });
    }

    root.__ribbon = state;
    refresh(root);
}

export function detach(root) {
    if (root) release(root);
}

export function focusFirst(menu) {
    if (!menu) return;
    const items = usableIn(menu, MENU_ITEM);
    const first = items[0];
    const target = first && first.tagName === 'INPUT' ? first : items.find(el => el.getAttribute('aria-checked') === 'true') || first;
    if (!target) return;
    target.focus();
    if (target.tagName === 'INPUT') target.select();
}
