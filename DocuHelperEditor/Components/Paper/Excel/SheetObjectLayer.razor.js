const sessions = new WeakMap();

function cellRect(grid, row, column) {
    const cell = grid.querySelector('tr[data-r="' + row + '"] td[data-c="' + column + '"]');
    return cell ? cell.getBoundingClientRect() : null;
}

function cellAt(x, y) {
    const hit = document.elementFromPoint(x, y);
    const td = hit && hit.closest('td[data-c]');
    const tr = td && td.closest('tr[data-r]');
    if (!tr) return null;
    return { row: +tr.dataset.r, column: +td.dataset.c };
}

export function layout(root) {
    const overlay = root.parentElement;
    const grid = root.closest('.grid');
    if (!overlay || !grid) return;
    const table = grid.querySelector('table.sheet');
    const zoom = parseFloat(table && table.style.zoom) || 1;
    const origin = overlay.getBoundingClientRect();
    for (const el of root.querySelectorAll('.obj')) {
        const d = el.dataset;
        const from = cellRect(grid, +d.fromRow, +d.fromColumn);
        const to = cellRect(grid, +d.toRow, +d.toColumn);
        if (!from || !to) {
            el.style.visibility = 'hidden';
            continue;
        }

        const left = from.left - origin.left + (+d.fromColumnOffset || 0) * zoom;
        const top = from.top - origin.top + (+d.fromRowOffset || 0) * zoom;
        const right = to.left - origin.left + (+d.toColumnOffset || 0) * zoom;
        const bottom = to.top - origin.top + (+d.toRowOffset || 0) * zoom;
        el.style.left = left + 'px';
        el.style.top = top + 'px';
        el.style.width = Math.max(8, right - left) + 'px';
        el.style.height = Math.max(8, bottom - top) + 'px';
        el.style.visibility = 'visible';
    }
}

function onDown(root, e) {
    const el = e.target.closest('.obj');
    if (!el || e.button !== 0) return;
    const session = sessions.get(root);
    const handle = e.target.closest('[data-handle="resize"]');
    session.drag = {
        el,
        mode: handle ? 'resize' : 'move',
        pointer: e.pointerId,
        x: e.clientX,
        y: e.clientY,
        left: parseFloat(el.style.left) || 0,
        top: parseFloat(el.style.top) || 0,
        width: el.offsetWidth,
        height: el.offsetHeight,
        name: el.dataset.name,
        row: +el.dataset.fromRow,
        column: +el.dataset.fromColumn,
    };
    e.preventDefault();
    el.setPointerCapture(e.pointerId);
    el.focus({ preventScroll: true });
    session.dotnet.invokeMethodAsync('Select', session.drag.name);
}

function onMove(root, e) {
    const drag = sessions.get(root).drag;
    if (!drag || e.pointerId !== drag.pointer) return;
    const dx = e.clientX - drag.x;
    const dy = e.clientY - drag.y;
    if (drag.mode === 'move') {
        drag.el.style.left = drag.left + dx + 'px';
        drag.el.style.top = drag.top + dy + 'px';
    } else {
        drag.el.style.width = Math.max(8, drag.width + dx) + 'px';
        drag.el.style.height = Math.max(8, drag.height + dy) + 'px';
    }
}

function onUp(root, e) {
    const session = sessions.get(root);
    const drag = session.drag;
    if (!drag || e.pointerId !== drag.pointer) return;
    session.drag = null;
    const dx = e.clientX - drag.x;
    const dy = e.clientY - drag.y;
    if (Math.abs(dx) < 3 && Math.abs(dy) < 3) {
        layout(root);
        return;
    }

    const origin = root.parentElement.getBoundingClientRect();
    drag.el.style.pointerEvents = 'none';
    if (drag.mode === 'move') {
        const target = cellAt(origin.left + drag.left + dx, origin.top + drag.top + dy);
        drag.el.style.pointerEvents = '';
        if (target)
            session.dotnet.invokeMethodAsync('Drop', drag.name, 'move', target.row, target.column, 0, 0);
    } else {
        const end = cellAt(origin.left + drag.left + drag.width + dx, origin.top + drag.top + drag.height + dy);
        drag.el.style.pointerEvents = '';
        if (end) {
            const columns = Math.max(1, end.column - drag.column + 1);
            const rows = Math.max(1, end.row - drag.row + 1);
            session.dotnet.invokeMethodAsync('Drop', drag.name, 'resize', drag.row, drag.column, columns, rows);
        }
    }

    layout(root);
}

function onKey(root, e) {
    const active = document.activeElement;
    if (active && (active.matches('input, textarea, [contenteditable]') || active.closest('input, textarea'))) return;
    const session = sessions.get(root);
    const selected = root.querySelector('.obj.sel');
    if (!selected) return;
    if (e.key === 'Delete' || e.key === 'Backspace') {
        e.preventDefault();
        session.dotnet.invokeMethodAsync('Remove', selected.dataset.name);
    } else if (e.key === 'Escape') {
        session.dotnet.invokeMethodAsync('Select', '');
    }
}

export function attach(root, dotnet) {
    const session = { dotnet, drag: null };
    session.down = (e) => onDown(root, e);
    session.move = (e) => onMove(root, e);
    session.up = (e) => onUp(root, e);
    session.key = (e) => onKey(root, e);
    sessions.set(root, session);
    root.addEventListener('pointerdown', session.down);
    root.addEventListener('pointermove', session.move);
    root.addEventListener('pointerup', session.up);
    document.addEventListener('keydown', session.key);
}

export function detach(root) {
    const session = sessions.get(root);
    if (!session) return;
    root.removeEventListener('pointerdown', session.down);
    root.removeEventListener('pointermove', session.move);
    root.removeEventListener('pointerup', session.up);
    document.removeEventListener('keydown', session.key);
    sessions.delete(root);
}
