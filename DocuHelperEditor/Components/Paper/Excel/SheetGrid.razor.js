const EDGE = 24;
const STEP = 20;
const GRAB = 5;
const MIN_SIZE = 8;

function cellOf(target) {
    const td = target && target.closest ? target.closest('td') : null;
    if (!td || td.dataset.c === undefined) return null;
    const tr = td.parentElement;
    if (!tr || !tr.parentElement || tr.parentElement.tagName !== 'TBODY') return null;
    return { row: +tr.dataset.r, column: +td.dataset.c };
}

function send(state, phase, row, column, shift, ctrl) {
    state.dotnet.invokeMethodAsync('Pointer', phase, row, column, !!shift, !!ctrl).catch(() => { });
}

function isEditingSink(el) {
    return el && el.classList && el.classList.contains('sink') && el.classList.contains('on');
}

function scrollNearEdge(grid, e) {
    const rect = grid.getBoundingClientRect();
    if (e.clientY > rect.bottom - EDGE) grid.scrollTop += STEP;
    else if (e.clientY < rect.top + 28 + EDGE) grid.scrollTop -= STEP;
    if (e.clientX > rect.right - EDGE) grid.scrollLeft += STEP;
    else if (e.clientX < rect.left + 44 + EDGE) grid.scrollLeft -= STEP;
}

function edgeAt(state, e) {
    const th = e.target && e.target.closest ? e.target.closest('th') : null;
    if (!th || !state.grid.contains(th)) return null;
    const rect = th.getBoundingClientRect();
    const tr = th.parentElement;
    if (tr.parentElement.tagName === 'THEAD') {
        if (rect.right - e.clientX <= GRAB && th.dataset.c !== undefined) return { rows: false, index: +th.dataset.c, cell: th };
        const previous = th.previousElementSibling;
        if (e.clientX - rect.left <= GRAB && previous && previous.dataset.c !== undefined) return { rows: false, index: +previous.dataset.c, cell: previous };
        return null;
    }

    if (th.cellIndex !== 0 || tr.dataset.r === undefined) return null;
    if (rect.bottom - e.clientY <= GRAB) return { rows: true, index: +tr.dataset.r, cell: th };
    const previous = tr.previousElementSibling;
    if (e.clientY - rect.top <= GRAB && previous && previous.dataset.r !== undefined) return { rows: true, index: +previous.dataset.r, cell: previous.cells[0] };
    return null;
}

function hover(state, e) {
    const edge = edgeAt(state, e);
    state.grid.classList.toggle('rz-c', !!edge && !edge.rows);
    state.grid.classList.toggle('rz-r', !!edge && edge.rows);
}

function startResize(state, edge, e) {
    const rect = edge.cell.getBoundingClientRect();
    const box = state.grid.getBoundingClientRect();
    const guide = document.createElement('div');
    guide.style.cssText = 'position:fixed;z-index:70;pointer-events:none;background:#1C2A48;';
    if (edge.rows) {
        guide.style.left = box.left + 'px';
        guide.style.width = box.width + 'px';
        guide.style.height = '2px';
    } else {
        guide.style.top = box.top + 'px';
        guide.style.height = box.height + 'px';
        guide.style.width = '2px';
    }

    document.body.appendChild(guide);
    state.mode = 'resize';
    state.resize = { rows: edge.rows, index: edge.index, origin: edge.rows ? rect.top : rect.left, start: edge.rows ? e.clientY : e.clientX, size: edge.rows ? rect.height : rect.width, guide };
    moveGuide(state, e);
}

function resizedSize(state, e) {
    const r = state.resize;
    const delta = (r.rows ? e.clientY : e.clientX) - r.start;
    return { delta, size: Math.max(MIN_SIZE, Math.round(r.size + delta)) };
}

function moveGuide(state, e) {
    const r = state.resize;
    const { size } = resizedSize(state, e);
    if (r.rows) r.guide.style.top = (r.origin + size - 1) + 'px';
    else r.guide.style.left = (r.origin + size - 1) + 'px';
}

function finishResize(state, e) {
    const r = state.resize;
    const { delta, size } = resizedSize(state, e);
    state.resize = null;
    state.mode = null;
    r.guide.remove();
    if (Math.abs(delta) < 2) return;
    state.dotnet.invokeMethodAsync('Resize', r.rows, r.index, size).catch(() => { });
}

function onDown(state, e) {
    if (e.button !== 0) return;
    const target = e.target;
    if (isEditingSink(target)) return;

    const edge = edgeAt(state, e);
    if (edge) {
        e.preventDefault();
        startResize(state, edge, e);
        return;
    }

    if (target.classList && target.classList.contains('fh')) {
        e.preventDefault();
        state.mode = 'fill';
        state.last = null;
        return;
    }

    const th = target.closest ? target.closest('th') : null;
    if (th && state.grid.contains(th)) {
        e.preventDefault();
        const tr = th.parentElement;
        if (tr.parentElement.tagName === 'THEAD') {
            if (th.dataset.c === undefined) send(state, 'all', -1, -1, e.shiftKey);
            else send(state, 'column', -1, +th.dataset.c, e.shiftKey);
        } else {
            send(state, 'row', +tr.dataset.r, -1, e.shiftKey);
        }
        return;
    }

    const cell = cellOf(target);
    if (!cell) return;
    e.preventDefault();
    state.mode = 'select';
    state.last = cell;
    send(state, 'down', cell.row, cell.column, e.shiftKey, e.ctrlKey || e.metaKey);
}

function onMove(state, e) {
    if (state.mode === 'resize') {
        moveGuide(state, e);
        return;
    }

    if (!state.mode) {
        if (state.grid.contains(e.target)) hover(state, e);
        return;
    }

    if ((e.buttons & 1) === 0) {
        finish(state);
        return;
    }

    scrollNearEdge(state.grid, e);
    const cell = state.grid.contains(e.target) ? cellOf(e.target) : null;
    if (!cell) return;
    if (state.last && state.last.row === cell.row && state.last.column === cell.column) return;
    state.last = cell;
    send(state, state.mode === 'fill' ? 'fill-move' : 'move', cell.row, cell.column, false);
}

function finish(state) {
    const mode = state.mode;
    const last = state.last;
    state.mode = null;
    state.last = null;
    send(state, mode === 'fill' ? 'fill-up' : 'up', last ? last.row : -1, last ? last.column : -1, false);
}

function onUp(state, e) {
    if (state.mode === 'resize') finishResize(state, e);
    else if (state.mode) finish(state);
}

function onDoubleClick(state, e) {
    const edge = edgeAt(state, e);
    if (!edge) return;
    e.preventDefault();
    state.dotnet.invokeMethodAsync('Resize', edge.rows, edge.index, -1).catch(() => { });
}

function onContextMenu(state, e) {
    const target = e.target;
    if (isEditingSink(target)) return;
    const th = target.closest ? target.closest('th') : null;
    let kind = null;
    let row = -1;
    let column = -1;
    if (th && state.grid.contains(th)) {
        const tr = th.parentElement;
        if (tr.parentElement.tagName === 'THEAD') {
            if (th.dataset.c === undefined) return;
            kind = 'column';
            column = +th.dataset.c;
        } else {
            kind = 'row';
            row = +tr.dataset.r;
        }
    } else {
        const cell = cellOf(target);
        if (!cell) return;
        kind = 'cell';
        row = cell.row;
        column = cell.column;
    }

    e.preventDefault();
    state.dotnet.invokeMethodAsync('Menu', kind, row, column, e.clientX, e.clientY).catch(() => { });
}

function onPaste(state, e) {
    const target = e.target;
    if (!target.classList || !target.classList.contains('sink') || isEditingSink(target)) return;
    e.preventDefault();
    const text = e.clipboardData ? e.clipboardData.getData('text/plain') : '';
    if (text) state.dotnet.invokeMethodAsync('Paste', text).catch(() => { });
}

function onKey(state, e) {
    const target = e.target;
    if (!target.classList || !target.classList.contains('sink')) return;
    if (e.key === 'Tab') {
        e.preventDefault();
        return;
    }

    if (e.isComposing || e.keyCode === 229) return;
    const editing = isEditingSink(target);
    const arrow = (e.key || '').startsWith('Arrow');
    if ((e.ctrlKey || e.metaKey) && !e.altKey) {
        const key = (e.key || '').toLowerCase();
        if (['f', 'h', 'd', 'r', 'y'].includes(key)) e.preventDefault();
        else if (!editing && (['a', 'c', 'x', 'z', '9', '0', '-', '+', '=', 'b', 'i', 'u', 'k', 't', 'e', 'l', '`', 'f3', '%'].includes(key) || arrow)) e.preventDefault();
        return;
    }

    if (!editing && (e.key === 'F3' || e.key === 'F7' || (e.key === 'F2' && e.shiftKey) || (e.altKey && e.key === '='))) e.preventDefault();

    if (!editing && arrow) e.preventDefault();
}

export function layout(grid, rows, columns) {
    const table = grid ? grid.querySelector('table.sheet') : null;
    if (!table) return;
    const head = table.tHead;
    let top = head ? head.getBoundingClientRect().height : 0;
    if (rows > 0 && table.tBodies.length > 0) {
        for (const tr of table.tBodies[0].rows) {
            if (+tr.dataset.r >= rows) break;
            tr.style.setProperty('--fz-top', top + 'px');
            top += tr.getBoundingClientRect().height;
        }
    }

    if (columns > 0 && head && head.rows.length > 0) {
        const cells = head.rows[0].cells;
        let left = cells[0].getBoundingClientRect().width;
        for (let i = 1; i < cells.length; i++) {
            const column = +cells[i].dataset.c;
            if (column >= columns) break;
            table.style.setProperty('--fz-l' + column, left + 'px');
            left += cells[i].getBoundingClientRect().width;
        }
    }
}

function report(state) {
    if (state.frame) return;
    state.frame = requestAnimationFrame(() => {
        state.frame = 0;
        const table = state.grid.querySelector('table.sheet');
        if (!table) return;
        const zoom = parseFloat(table.style.zoom) || 1;
        const head = table.tHead ? table.tHead.getBoundingClientRect().height / zoom : 0;
        const top = state.grid.scrollTop / zoom - head;
        const height = state.grid.clientHeight / zoom;
        const rows = [];
        const heights = [];
        for (const tr of table.tBodies[0].rows) {
            if (tr.dataset.r === undefined) continue;
            const row = +tr.dataset.r;
            const measured = tr.getBoundingClientRect().height / zoom;
            const known = state.sent.get(row);
            if (known !== undefined && Math.abs(known - measured) <= 0.5) continue;
            state.sent.set(row, measured);
            rows.push(row);
            heights.push(measured);
        }

        if (rows.length === 0 && top === state.top && height === state.height) return;
        state.top = top;
        state.height = height;
        state.dotnet.invokeMethodAsync('Viewport', top, height, rows, heights).catch(() => { });
    });
}

export function refresh(grid, version) {
    const state = grid ? grid.__sheet : null;
    if (!state) return;
    if (state.version !== version) {
        state.version = version;
        state.sent.clear();
    }

    report(state);
}

export function attach(grid, dotnet) {
    if (!grid || grid.__sheet) return;
    const state = { grid, dotnet, mode: null, last: null, resize: null, frame: 0, scroll: null, resizer: null, sent: new Map(), version: null, top: null, height: null };
    state.down = e => onDown(state, e);
    state.move = e => onMove(state, e);
    state.up = e => onUp(state, e);
    state.dbl = e => onDoubleClick(state, e);
    state.menu = e => onContextMenu(state, e);
    state.paste = e => onPaste(state, e);
    state.key = e => onKey(state, e);
    state.scroll = () => report(state);
    state.resizer = new ResizeObserver(() => report(state));
    grid.addEventListener('scroll', state.scroll, { passive: true });
    state.resizer.observe(grid);
    grid.addEventListener('mousedown', state.down);
    grid.addEventListener('dblclick', state.dbl);
    grid.addEventListener('contextmenu', state.menu);
    document.addEventListener('mousemove', state.move);
    document.addEventListener('mouseup', state.up);
    grid.addEventListener('paste', state.paste, true);
    grid.addEventListener('keydown', state.key, true);
    grid.__sheet = state;
    report(state);
}

export function detach(grid) {
    const state = grid ? grid.__sheet : null;
    if (!state) return;
    if (state.resize) state.resize.guide.remove();
    if (state.frame) cancelAnimationFrame(state.frame);
    grid.removeEventListener('scroll', state.scroll);
    state.resizer.disconnect();
    grid.removeEventListener('mousedown', state.down);
    grid.removeEventListener('dblclick', state.dbl);
    grid.removeEventListener('contextmenu', state.menu);
    document.removeEventListener('mousemove', state.move);
    document.removeEventListener('mouseup', state.up);
    grid.removeEventListener('paste', state.paste, true);
    grid.removeEventListener('keydown', state.key, true);
    grid.__sheet = null;
}
