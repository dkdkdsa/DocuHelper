export function draw(host, model) {
    if (!host || !model || model.pages.length === 0)
        return;

    const table = host.querySelector('table.sheet');
    const first = table && table.querySelector('tbody tr[data-r="0"] td[data-c="0"]');
    if (!first)
        return;

    const scroller = scrollerOf(table);
    if (getComputedStyle(scroller).position === 'static')
        scroller.style.position = 'relative';

    scroller.querySelectorAll(':scope > .pf-overlay').forEach(node => node.remove());

    const widths = new Map();
    table.querySelectorAll('thead th[data-c]').forEach(th => widths.set(Number(th.dataset.c), th.getBoundingClientRect().width));

    const heights = new Map();
    table.querySelectorAll('tbody tr[data-r]').forEach(tr => heights.set(Number(tr.dataset.r), tr.getBoundingClientRect().height));

    const frame = scroller.getBoundingClientRect();
    const origin = first.getBoundingClientRect();
    const originX = origin.left - frame.left - scroller.clientLeft + scroller.scrollLeft;
    const originY = origin.top - frame.top - scroller.clientTop + scroller.scrollTop;

    const lastColumn = Math.max(...model.pages.map(page => page.right + 1));
    const lastRow = Math.max(...model.pages.map(page => page.bottom + 1));
    const columnOffsets = offsets(lastColumn + 1, column => widths.has(column) ? widths.get(column) : model.columnFallback * model.zoom);
    const rowOffsets = offsets(lastRow + 1, row => heights.has(row) ? heights.get(row) : model.rowFallback * model.zoom);

    const layer = document.createElement('div');
    layer.className = 'pf-overlay';
    layer.style.cssText = 'position:absolute;left:0;top:0;width:0;height:0;pointer-events:none;z-index:3;';
    scroller.appendChild(layer);

    for (const page of model.pages) {
        const left = originX + columnOffsets[page.left];
        const top = originY + rowOffsets[page.top];
        const width = columnOffsets[page.right + 1] - columnOffsets[page.left];
        const height = rowOffsets[page.bottom + 1] - rowOffsets[page.top];

        const box = document.createElement('div');
        box.className = 'pf-page' + (page.manualTop || page.manualLeft ? ' manual' : '');
        box.style.cssText = 'left:' + left + 'px;top:' + top + 'px;width:' + width + 'px;height:' + height + 'px;';

        if (model.mode === 'pageBreak') {
            const mark = document.createElement('span');
            mark.className = 'pf-page-mark';
            mark.textContent = page.number + ' 페이지';
            box.appendChild(mark);
        }

        layer.appendChild(box);
    }

    if (model.notice) {
        const notice = document.createElement('div');
        notice.className = 'pf-notice';
        notice.textContent = model.notice;
        layer.appendChild(notice);
    }
}

function scrollerOf(node) {
    for (let element = node.parentElement; element; element = element.parentElement) {
        if (/(auto|scroll)/.test(getComputedStyle(element).overflow))
            return element;
    }

    return node.parentElement;
}

function offsets(count, size) {
    const result = new Float64Array(count + 1);
    for (let index = 0; index < count; index++)
        result[index + 1] = result[index] + size(index);

    return result;
}
