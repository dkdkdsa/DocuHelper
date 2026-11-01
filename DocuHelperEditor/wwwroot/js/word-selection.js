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

    function read(root) {
        var sel = document.getSelection();
        var none = { kind: 'none', caret: null, ranges: [] };
        if (!sel || sel.rangeCount === 0) return none;
        var range = sel.getRangeAt(0);
        if (!root.contains(range.commonAncestorContainer)) return range.collapsed ? none : null;
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
        document.addEventListener('selectionchange', state.handler);
        papers.set(root, state);
    }

    function detach(root) {
        var state = papers.get(root);
        if (!state) return;
        document.removeEventListener('selectionchange', state.handler);
        papers.delete(root);
    }

    function keepSelection(e) {
        var target = e.target;
        if (target && target.closest && target.closest('[data-ribbon]') && !target.closest('input, select, textarea')) e.preventDefault();
    }

    document.addEventListener('mousedown', keepSelection, true);

    return {
        attach: attach,
        detach: detach,
        restore: function (ranges, caret) {
            setTimeout(function () { restore(ranges || [], caret || null); }, 60);
        },
    };
})();
