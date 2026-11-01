window.docuHelperWordSelection = (function () {
    var papers = new Map();

    function blockOf(node) {
        var element = node && node.nodeType === 1 ? node : node && node.parentElement;
        return element ? element.closest('[data-block]') : null;
    }

    function offsetIn(block, node, offset) {
        var range = document.createRange();
        range.selectNodeContents(block);
        range.setEnd(node, offset);
        return range.toString().length;
    }

    function caretOf(sel, root) {
        var block = blockOf(sel.focusNode);
        if (!block || !root.contains(block)) return null;
        return { id: block.dataset.block, offset: offsetIn(block, sel.focusNode, sel.focusOffset) };
    }

    function cellOf(node) {
        var element = node && node.nodeType === 1 ? node : node && node.parentElement;
        return element ? element.closest('td[data-table], th[data-table]') : null;
    }

    function cellsOf(sel, root) {
        var anchor = cellOf(sel.anchorNode);
        var focus = cellOf(sel.focusNode);
        if (!anchor || !focus || !root.contains(focus) || anchor.dataset.table !== focus.dataset.table) return null;
        var r1 = Number(anchor.dataset.row), c1 = Number(anchor.dataset.column);
        var r2 = Number(focus.dataset.row), c2 = Number(focus.dataset.column);
        return { table: focus.dataset.table, row1: Math.min(r1, r2), column1: Math.min(c1, c2), row2: Math.max(r1, r2), column2: Math.max(c1, c2) };
    }

    function read(root) {
        var sel = document.getSelection();
        var none = { kind: 'none', caret: null, ranges: [] };
        if (!sel || sel.rangeCount === 0) return none;
        var range = sel.getRangeAt(0);
        if (!root.contains(range.commonAncestorContainer)) return range.collapsed ? none : null;
        var cells = cellsOf(sel, root);
        if (cells) return { kind: 'cells', caret: null, cells: cells, ranges: [] };
        var caret = caretOf(sel, root);
        if (range.collapsed) {
            return caret ? { kind: 'caret', caret: caret, ranges: [] } : null;
        }

        var ranges = [];
        root.querySelectorAll('[data-block]').forEach(function (block) {
            if (!range.intersectsNode(block)) return;
            var start = block.contains(range.startContainer) ? offsetIn(block, range.startContainer, range.startOffset) : 0;
            var end = block.contains(range.endContainer) ? offsetIn(block, range.endContainer, range.endOffset) : block.textContent.length;
            if (end > start) ranges.push({ id: block.dataset.block, start: start, end: end });
        });

        return { kind: ranges.length > 0 ? 'text' : 'caret', caret: caret, ranges: ranges };
    }

    function locate(block, offset) {
        var walker = document.createTreeWalker(block, NodeFilter.SHOW_TEXT);
        var remaining = offset;
        var node = walker.nextNode();
        while (node) {
            if (remaining <= node.textContent.length) return { node: node, offset: remaining };
            remaining -= node.textContent.length;
            node = walker.nextNode();
        }
        return { node: block, offset: block.childNodes.length };
    }

    function restore(ranges, caret) {
        var selection = document.getSelection();
        if (!selection) return;
        var first = ranges.length > 0 ? ranges[0] : caret;
        var last = ranges.length > 0 ? ranges[ranges.length - 1] : caret;
        if (!first) return;
        var firstBlock = document.querySelector('[data-block="' + first.id + '"]');
        var lastBlock = document.querySelector('[data-block="' + last.id + '"]');
        if (!firstBlock || !lastBlock) return;
        var start = locate(firstBlock, first.start !== undefined ? first.start : first.offset);
        var end = locate(lastBlock, last.end !== undefined ? last.end : last.offset);
        var range = document.createRange();
        range.setStart(start.node, start.offset);
        range.setEnd(end.node, end.offset);
        selection.removeAllRanges();
        selection.addRange(range);
    }

    function restoreCell(table, row, column) {
        var selection = document.getSelection();
        if (!selection) return;
        var selector = '[data-table="' + table + '"][data-row="' + row + '"][data-column="' + column + '"]';
        var cell = document.querySelector('td' + selector + ', th' + selector);
        if (!cell) return;
        var range = document.createRange();
        range.selectNodeContents(cell);
        range.collapse(false);
        selection.removeAllRanges();
        selection.addRange(range);
    }

    function attach(root, reference) {
        if (!root || !reference || papers.has(root)) return;
        var state = { reference: reference, frame: 0, last: '', handler: null };
        state.handler = function () {
            if (state.frame) return;
            state.frame = requestAnimationFrame(function () {
                state.frame = 0;
                var data = read(root);
                if (data === null) return;
                var json = JSON.stringify(data);
                if (json === state.last) return;
                state.last = json;
                reference.invokeMethodAsync('Selected', json).catch(function () { });
            });
        };
        state.shortcut = function (e) {
            if (!(e.ctrlKey || e.metaKey) || e.altKey) return;
            var key = (e.key || '').toLowerCase();
            var clip = key === 'c' || key === 'x' || key === 'v';
            if (!clip && (e.shiftKey || (key !== 'b' && key !== 'i' && key !== 'u'))) return;
            if (clip && key !== 'v' && e.shiftKey) return;
            if (e.target && e.target.closest && e.target.closest('input, textarea, select')) return;
            var sel = document.getSelection();
            var inside = sel && sel.rangeCount > 0 && root.contains(sel.getRangeAt(0).commonAncestorContainer);
            if (!inside && !(clip && root.contains(document.activeElement))) return;
            e.preventDefault();
            var name = clip && e.shiftKey ? 'shift+' + key : key;
            reference.invokeMethodAsync('Shortcut', name).catch(function () { });
        };
        document.addEventListener('keydown', state.shortcut);
        document.addEventListener('selectionchange', state.handler);
        papers.set(root, state);
    }

    function detach(root) {
        var state = papers.get(root);
        if (!state) return;
        document.removeEventListener('selectionchange', state.handler);
        document.removeEventListener('keydown', state.shortcut);
        papers.delete(root);
    }

    function keepSelection(e) {
        var target = e.target;
        if (target && target.closest && target.closest('[data-ribbon]') && !target.closest('input, select, textarea')) e.preventDefault();
    }

    document.addEventListener('mousedown', keepSelection, true);

    function guardObjectKey(e) {
        var target = e.target;
        if (target && target.classList && target.classList.contains('obj') && (e.key === ' ' || e.key === 'Delete' || e.key === 'Backspace')) e.preventDefault();
    }

    document.addEventListener('keydown', guardObjectKey, true);

    return {
        attach: attach,
        detach: detach,
        restore: function (ranges, caret) {
            setTimeout(function () { restore(ranges || [], caret || null); }, 60);
        },
        restoreCell: function (table, row, column) {
            setTimeout(function () { restoreCell(table, row, column); }, 60);
        },
    };
})();
