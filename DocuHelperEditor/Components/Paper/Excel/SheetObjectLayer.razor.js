const sessions = new WeakMap();

function cellRect(grid, row, column) {
    const cell = grid.querySelector('tr[data-r="' + row + '"] td[data-c="' + column + '"]');
    return cell ? cell.getBoundingClientRect() : null;
}

function hitCell(grid, x, y) {
    for (const tr of grid.querySelectorAll('tbody tr[data-r]')) {
        const r = tr.getBoundingClientRect();
        if (y < r.top || y >= r.bottom)
            continue;

        for (const td of tr.querySelectorAll('td[data-c]')) {
            const c = td.getBoundingClientRect();
            if (x >= c.left && x < c.right)
                return { row: +tr.dataset.r, column: +td.dataset.c };
        }

        return null;
    }

    return null;
}

function gridOf(root) {
    return root.closest('.grid');
}

export function layout(root) {
    const overlay = root.parentElement;
    const grid = gridOf(root);
    if (!overlay || !grid)
        return;

    const table = grid.querySelector('table.sheet');
    const zoom = parseFloat(table && table.style.zoom) || 1;
    const origin = overlay.getBoundingClientRect();
    for (const el of root.querySelectorAll('.obj')) {
        if (el.classList.contains('dragging'))
            continue;

        const d = el.dataset;
        const from = cellRect(grid, +d.fromRow, +d.fromColumn);
        if (!from) {
            el.style.visibility = 'hidden';
            continue;
        }

        const left = from.left - origin.left + Math.min((+d.fromColumnOffset || 0) * zoom, from.width);
        const top = from.top - origin.top + Math.min((+d.fromRowOffset || 0) * zoom, from.height);
        let right;
        let bottom;
        if (d.oneCell === 'true') {
            right = left + (+d.width || 0) * zoom;
            bottom = top + (+d.height || 0) * zoom;
        } else {
            const to = cellRect(grid, +d.toRow, +d.toColumn);
            if (!to) {
                el.style.visibility = 'hidden';
                continue;
            }

            right = to.left - origin.left + Math.min((+d.toColumnOffset || 0) * zoom, to.width);
            bottom = to.top - origin.top + Math.min((+d.toRowOffset || 0) * zoom, to.height);
        }

        const width = right - left;
        const height = bottom - top;
        if (width <= 0 || height <= 0) {
            el.style.visibility = 'hidden';
            continue;
        }

        el.style.left = left + 'px';
        el.style.top = top + 'px';
        el.style.width = width + 'px';
        el.style.height = height + 'px';
        el.style.visibility = 'visible';
    }
}

function cancelDrag(root) {
    const session = sessions.get(root);
    if (!session || !session.drag)
        return;

    session.drag.el.classList.remove('dragging');
    session.drag = null;
    layout(root);
}

function invoke(session, method, ...args) {
    session.dotnet.invokeMethodAsync(method, ...args).catch(() => { });
}

function onDown(root, e) {
    const el = e.target.closest('.obj');
    if (!el || !root.contains(el) || e.button !== 0)
        return;

    const session = sessions.get(root);
    if (!session)
        return;

    e.preventDefault();
    el.focus({ preventScroll: true });
    invoke(session, 'Select', el.dataset.id);
    if (el.dataset.editable !== 'true')
        return;

    const handle = e.target.closest('[data-handle="resize"]');
    const rect = el.getBoundingClientRect();
    session.drag = {
        el,
        mode: handle ? 'resize' : 'move',
        pointer: e.pointerId,
        x: e.clientX,
        y: e.clientY,
        rect,
        id: el.dataset.id,
        row: +el.dataset.fromRow,
        column: +el.dataset.fromColumn,
        toRow: +el.dataset.toRow,
        toColumn: +el.dataset.toColumn,
        moved: false,
    };
    el.classList.add('dragging');
    el.setPointerCapture(e.pointerId);
}

function onMove(root, e) {
    const session = sessions.get(root);
    const drag = session && session.drag;
    if (!drag || e.pointerId !== drag.pointer)
        return;

    const dx = e.clientX - drag.x;
    const dy = e.clientY - drag.y;
    if (Math.abs(dx) >= 3 || Math.abs(dy) >= 3)
        drag.moved = true;

    const origin = root.parentElement.getBoundingClientRect();
    if (drag.mode === 'move') {
        drag.el.style.left = (drag.rect.left - origin.left + dx) + 'px';
        drag.el.style.top = (drag.rect.top - origin.top + dy) + 'px';
    } else {
        drag.el.style.width = Math.max(8, drag.rect.width + dx) + 'px';
        drag.el.style.height = Math.max(8, drag.rect.height + dy) + 'px';
    }
}

function onUp(root, e) {
    const session = sessions.get(root);
    const drag = session && session.drag;
    if (!drag || e.pointerId !== drag.pointer)
        return;

    session.drag = null;
    drag.el.classList.remove('dragging');
    if (!drag.moved) {
        layout(root);
        return;
    }

    const dx = e.clientX - drag.x;
    const dy = e.clientY - drag.y;
    const grid = gridOf(root);
    if (drag.mode === 'move') {
        const target = hitCell(grid, drag.rect.left + dx, drag.rect.top + dy);
        if (target && (target.row !== drag.row || target.column !== drag.column))
            invoke(session, 'Drop', drag.id, 'move', target.row, target.column, 0, 0);
    } else {
        const end = hitCell(grid, drag.rect.right + dx, drag.rect.bottom + dy);
        if (end) {
            const columns = Math.max(1, end.column - drag.column + 1);
            const rows = Math.max(1, end.row - drag.row + 1);
            invoke(session, 'Drop', drag.id, 'resize', drag.row, drag.column, columns, rows);
        }
    }

    layout(root);
}

function onKeyDown(root, e) {
    if (!root.contains(document.activeElement))
        return;

    const session = sessions.get(root);
    const focused = document.activeElement;
    if (!session || !focused.classList.contains('obj'))
        return;

    if (e.key === 'Escape') {
        e.preventDefault();
        if (session.drag) {
            cancelDrag(root);
        } else {
            invoke(session, 'Select', '');
        }

        return;
    }

    if (session.drag)
        return;

    if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        invoke(session, 'Select', focused.dataset.id);
        return;
    }

    const arrows = { ArrowLeft: [-1, 0], ArrowRight: [1, 0], ArrowUp: [0, -1], ArrowDown: [0, 1] };
    const step = arrows[e.key];
    if (step && focused.dataset.editable === 'true') {
        e.preventDefault();
        const [dx, dy] = step;
        const row = +focused.dataset.fromRow;
        const column = +focused.dataset.fromColumn;
        if (e.shiftKey) {
            const columns = Math.max(1, (+focused.dataset.toColumn - column + 1) + dx);
            const rows = Math.max(1, (+focused.dataset.toRow - row + 1) + dy);
            invoke(session, 'Drop', focused.dataset.id, 'resize', row, column, columns, rows);
        } else {
            invoke(session, 'Drop', focused.dataset.id, 'move', Math.max(0, row + dy), Math.max(0, column + dx), 0, 0);
        }

        return;
    }

    if (e.key === 'Delete' && focused.dataset.editable === 'true') {
        e.preventDefault();
        invoke(session, 'Remove', focused.dataset.id);
    }
}

function onDocumentDown(root, e) {
    const session = sessions.get(root);
    if (!session)
        return;

    if (e.target.closest && e.target.closest('.obj') && root.contains(e.target))
        return;

    if (session.drag)
        return;

    invoke(session, 'Select', '');
}

function onCancel(root, e) {
    const session = sessions.get(root);
    if (!session || !session.drag || e.pointerId !== session.drag.pointer)
        return;

    cancelDrag(root);
}

export function attach(root, dotnet) {
    const session = { dotnet, drag: null };
    session.down = (e) => onDown(root, e);
    session.move = (e) => onMove(root, e);
    session.up = (e) => onUp(root, e);
    session.cancel = (e) => onCancel(root, e);
    session.key = (e) => onKeyDown(root, e);
    session.docDown = (e) => onDocumentDown(root, e);
    sessions.set(root, session);
    root.addEventListener('pointerdown', session.down);
    root.addEventListener('pointermove', session.move);
    root.addEventListener('pointerup', session.up);
    root.addEventListener('pointercancel', session.cancel);
    root.addEventListener('lostpointercapture', session.cancel);
    document.addEventListener('keydown', session.key);
    document.addEventListener('pointerdown', session.docDown, true);
}

export function detach(root) {
    const session = sessions.get(root);
    if (!session)
        return;

    root.removeEventListener('pointerdown', session.down);
    root.removeEventListener('pointermove', session.move);
    root.removeEventListener('pointerup', session.up);
    root.removeEventListener('pointercancel', session.cancel);
    root.removeEventListener('lostpointercapture', session.cancel);
    document.removeEventListener('keydown', session.key);
    document.removeEventListener('pointerdown', session.docDown, true);
    sessions.delete(root);
}
