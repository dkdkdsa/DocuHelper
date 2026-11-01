window.docuHelperPptCanvas = (function () {
    var sessions = new Map();
    var MIN_SIZE = 0.2;
    var EDIT_KEYS = ['Delete', 'Backspace', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Escape'];

    function send(session, payload) {
        if (!session.reference) return Promise.resolve();
        payload.slideId = session.slide.dataset.slide || null;
        return session.reference.invokeMethodAsync('Receive', payload).catch(function () { });
    }

    function percentOf(el, name) {
        return parseFloat(el.dataset[name]) || 0;
    }

    function itemOf(el) {
        return {
            el: el,
            x: percentOf(el, 'x'),
            y: percentOf(el, 'y'),
            w: percentOf(el, 'w'),
            h: percentOf(el, 'h'),
            rot: percentOf(el, 'rot'),
            stamp: [el.dataset.x, el.dataset.y, el.dataset.w, el.dataset.h, el.dataset.rot].join('|')
        };
    }

    function selectedMovable(slide) {
        return Array.prototype.slice.call(slide.querySelectorAll('.shape.on[data-movable="true"]'));
    }

    function moveGroup(slide, shape, shift) {
        var group;
        if (shape.classList.contains('on')) {
            group = selectedMovable(slide);
        } else if (shift) {
            group = selectedMovable(slide).concat([shape]);
        } else {
            group = [shape];
        }

        return group.filter(function (el, index) { return group.indexOf(el) === index; });
    }

    function angleOf(el, event) {
        var box = el.getBoundingClientRect();
        var cx = box.left + box.width / 2;
        var cy = box.top + box.height / 2;
        return Math.atan2(event.clientY - cy, event.clientX - cx) * 180 / Math.PI;
    }

    function restoreOverrides(item) {
        item.el.style.left = item.x + '%';
        item.el.style.top = item.y + '%';
        item.el.style.width = item.w + '%';
        item.el.style.height = item.h + '%';
        item.el.style.setProperty('--rot', item.rot + 'deg');
    }

    function settle(items, pending) {
        pending.then(function () {
            requestAnimationFrame(function () {
                requestAnimationFrame(function () {
                    items.forEach(function (item) {
                        if (item.el.isConnected && itemOf(item.el).stamp === item.stamp) restoreOverrides(item);
                    });
                });
            });
        });
    }

    function cancelDrag(session) {
        var drag = session.drag;
        if (!drag) return;
        session.drag = null;
        if (drag.moved) drag.items.forEach(restoreOverrides);
    }

    function startDrag(session, slide, e, kind, shape, handle) {
        var items = (kind === 'move' ? moveGroup(slide, shape, e.shiftKey) : [shape]).map(itemOf);
        if (items.length === 0) return;

        session.drag = {
            kind: kind,
            id: shape.dataset.shape,
            handle: handle,
            items: items,
            pointerId: e.pointerId,
            box: slide.getBoundingClientRect(),
            startX: e.clientX,
            startY: e.clientY,
            startAngle: kind === 'rotate' ? angleOf(shape, e) : 0,
            moved: false,
            dx: 0,
            dy: 0,
            angle: 0
        };
    }

    function focusSlide(slide) {
        slide.focus({ preventScroll: true });
    }

    function whenSelected(shape, limit, callback) {
        if (shape.classList.contains('on')) {
            callback();
            return;
        }

        var done = false;
        var timer = null;
        var observer = new MutationObserver(function () {
            if (shape.classList.contains('on')) finish();
        });

        function finish() {
            if (done) return;
            done = true;
            observer.disconnect();
            clearTimeout(timer);
            callback();
        }

        observer.observe(shape, { attributes: true, attributeFilter: ['class'] });
        timer = setTimeout(finish, limit);
    }

    function startEditing(shape) {
        if (!shape || !shape.classList.contains('on')) return false;

        var text = shape.querySelector('[contenteditable="plaintext-only"]');
        if (!text) return false;

        text.focus();
        var range = document.createRange();
        range.selectNodeContents(text);
        range.collapse(false);
        var selection = window.getSelection();
        selection.removeAllRanges();
        selection.addRange(range);
        return true;
    }

    function onDown(slide, session, e) {
        if (session.drag || e.button !== 0 || slide.dataset.locked === 'true') return;

        var target = e.target;
        var handle = target.closest ? target.closest('[data-handle]') : null;
        var shape = target.closest ? target.closest('[data-shape]') : null;

        if (handle && shape && slide.contains(shape) && shape.classList.contains('on') && shape.dataset.movable === 'true') {
            var kind = handle.dataset.handle === 'rot' ? 'rotate' : 'resize';
            startDrag(session, slide, e, kind, shape, handle.dataset.handle);
            e.preventDefault();
            focusSlide(slide);
            return;
        }

        if (!shape || !slide.contains(shape)) {
            focusSlide(slide);
            if (slide.querySelector('.shape.on')) {
                send(session, { kind: 'select', id: null, additive: false });
            }
            return;
        }

        if (target.isContentEditable === true) {
            var selected = shape.classList.contains('on');
            if (selected || shape.dataset.movable !== 'true') {
                if (!selected) {
                    send(session, { kind: 'select', id: shape.dataset.shape, additive: e.shiftKey });
                }
                return;
            }

            e.preventDefault();
            send(session, { kind: 'select', id: shape.dataset.shape, additive: e.shiftKey });
            focusSlide(slide);
            startDrag(session, slide, e, 'move', shape, null);
            return;
        }

        if (!shape.classList.contains('on')) {
            send(session, { kind: 'select', id: shape.dataset.shape, additive: e.shiftKey });
        }

        e.preventDefault();
        focusSlide(slide);
        if (shape.dataset.movable !== 'true') return;
        startDrag(session, slide, e, 'move', shape, null);
    }

    function onMove(session, e) {
        var drag = session.drag;
        if (!drag || e.pointerId !== drag.pointerId) return;

        var dx = e.clientX - drag.startX;
        var dy = e.clientY - drag.startY;
        if (!drag.moved && Math.abs(dx) + Math.abs(dy) < 3) return;
        drag.moved = true;

        var fx = dx / drag.box.width * 100;
        var fy = dy / drag.box.height * 100;
        var first = drag.items[0];

        if (drag.kind === 'move') {
            drag.items.forEach(function (item) {
                item.el.style.left = (item.x + fx) + '%';
                item.el.style.top = (item.y + fy) + '%';
            });
            drag.dx = fx / 100;
            drag.dy = fy / 100;
            return;
        }

        if (drag.kind === 'resize') {
            var left = first.x;
            var top = first.y;
            var width = first.w;
            var height = first.h;
            var h = drag.handle || '';
            if (h.indexOf('w') >= 0) { left = first.x + fx; width = first.w - fx; }
            if (h.indexOf('e') >= 0) { width = first.w + fx; }
            if (h.indexOf('n') >= 0) { top = first.y + fy; height = first.h - fy; }
            if (h.indexOf('s') >= 0) { height = first.h + fy; }
            width = Math.max(width, MIN_SIZE);
            height = Math.max(height, MIN_SIZE);
            first.el.style.left = left + '%';
            first.el.style.top = top + '%';
            first.el.style.width = width + '%';
            first.el.style.height = height + '%';
            drag.dx = fx / 100;
            drag.dy = fy / 100;
            return;
        }

        if (drag.kind === 'rotate') {
            drag.angle = angleOf(first.el, e) - drag.startAngle;
            first.el.style.setProperty('--rot', (first.rot + drag.angle) + 'deg');
        }
    }

    function onUp(session, e) {
        var drag = session.drag;
        if (!drag || e.pointerId !== drag.pointerId) return;
        session.drag = null;
        if (!drag.moved) return;

        var payload;
        if (drag.kind === 'move') {
            payload = { kind: 'move', id: drag.id, dx: drag.dx, dy: drag.dy };
        } else if (drag.kind === 'resize') {
            payload = { kind: 'resize', id: drag.id, handle: drag.handle, dx: drag.dx, dy: drag.dy };
        } else {
            payload = { kind: 'rotate', id: drag.id, angle: drag.angle };
        }

        settle(drag.items, send(session, payload));
    }

    function onCancel(session, e) {
        if (session.drag && e.pointerId === session.drag.pointerId) cancelDrag(session);
    }

    function cycle(slide, session, e) {
        var shapes = Array.prototype.slice.call(slide.querySelectorAll('.shape[data-shape]'));
        if (shapes.length === 0) return;

        var current = shapes.indexOf(slide.querySelector('.shape.on'));
        var step = e.shiftKey ? -1 : 1;
        var next = shapes[(current + step + shapes.length) % shapes.length];
        if (current < 0) next = shapes[step > 0 ? 0 : shapes.length - 1];

        e.preventDefault();
        next.focus({ preventScroll: true });
        send(session, { kind: 'select', id: next.dataset.shape, additive: false });
    }

    function onKey(slide, session, e) {
        if (e.ctrlKey || e.altKey || e.metaKey || slide.dataset.locked === 'true') return;

        var active = document.activeElement;
        if (!active || !slide.contains(active)) return;
        if (active.isContentEditable || active.tagName === 'INPUT' || active.tagName === 'TEXTAREA' || active.tagName === 'SELECT') return;

        if (session.drag) {
            if (e.key === 'Escape') {
                cancelDrag(session);
                e.preventDefault();
            }
            return;
        }

        if (e.key === 'Tab') {
            cycle(slide, session, e);
            return;
        }

        if (e.key === 'F2' || e.key === 'Enter') {
            var current = active.dataset && active.dataset.shape ? active : slide.querySelector('.shape.on');
            if (startEditing(current)) {
                e.preventDefault();
                return;
            }
        }

        if ((e.key === 'Enter' || e.key === ' ') && active.dataset && active.dataset.shape) {
            e.preventDefault();
            send(session, { kind: 'select', id: active.dataset.shape, additive: e.shiftKey });
            return;
        }

        if (EDIT_KEYS.indexOf(e.key) < 0) return;
        if (!slide.querySelector('.shape.on')) return;

        e.preventDefault();
        send(session, { kind: 'key', key: e.key, shift: e.shiftKey });
    }

    function dispose(slide) {
        var session = sessions.get(slide);
        if (!session) return;

        slide.removeEventListener('pointerdown', session.onDown);
        window.removeEventListener('pointermove', session.onMove);
        window.removeEventListener('pointerup', session.onUp);
        window.removeEventListener('pointercancel', session.onCancel);
        document.removeEventListener('keydown', session.onKey);
        slide.removeEventListener('dblclick', session.onDblClick);
        sessions.delete(slide);
    }

    return {
        attach: function (slide, reference) {
            if (!slide) return;

            sessions.forEach(function (_, element) {
                if (!element.isConnected) dispose(element);
            });

            var session = sessions.get(slide);
            if (session) {
                session.reference = reference;
                return;
            }

            session = { reference: reference, slide: slide, drag: null };
            session.onDown = function (e) { onDown(slide, session, e); };
            session.onMove = function (e) { onMove(session, e); };
            session.onUp = function (e) { onUp(session, e); };
            session.onCancel = function (e) { onCancel(session, e); };
            session.onKey = function (e) { onKey(slide, session, e); };
            session.onDblClick = function (e) {
                var shape = e.target.closest ? e.target.closest('[data-shape]') : null;
                if (!shape || !slide.contains(shape) || slide.dataset.locked === 'true') return;
                var text = shape.querySelector('[contenteditable="plaintext-only"]');
                if (text && document.activeElement === text) return;

                if (shape.classList.contains('on')) {
                    startEditing(shape);
                    return;
                }

                send(session, { kind: 'select', id: shape.dataset.shape, additive: false });
                whenSelected(shape, 500, function () { startEditing(shape); });
            };

            sessions.set(slide, session);
            slide.addEventListener('dblclick', session.onDblClick);
            slide.addEventListener('pointerdown', session.onDown);
            window.addEventListener('pointermove', session.onMove);
            window.addEventListener('pointerup', session.onUp);
            window.addEventListener('pointercancel', session.onCancel);
            document.addEventListener('keydown', session.onKey);
        },
        detach: function (slide) {
            dispose(slide);
        }
    };
})();

window.docuHelperPptGrip = (function () {
    var MIN = 48;
    var MAX = 600;
    var RATIO = 0.4;
    var STEP = 16;
    var PAGE = 64;
    var grips = new Map();

    function limit(host) {
        return Math.round(Math.min(MAX, Math.max(MIN, host.clientHeight * RATIO)));
    }

    function clamp(host, height) {
        return Math.round(Math.min(limit(host), Math.max(MIN, height)));
    }

    function current(record) {
        return record.notes.getBoundingClientRect().height;
    }

    function apply(record, height) {
        record.notes.style.height = height + 'px';
        record.grip.setAttribute('aria-valuenow', String(height));
        record.grip.setAttribute('aria-valuemax', String(limit(record.host)));
    }

    function commit(record, height) {
        record.reference.invokeMethodAsync('Resized', height).catch(function () { });
    }

    function setHeight(record, height) {
        var next = clamp(record.host, height);
        apply(record, next);
        commit(record, next);
    }

    function onDown(record, e) {
        if (e.button !== 0) return;

        e.preventDefault();
        var grip = record.grip;
        var pointer = e.pointerId;
        var startY = e.clientY;
        var start = current(record);
        var height = start;
        grip.setPointerCapture(pointer);

        function move(ev) {
            if (ev.pointerId !== pointer) return;
            height = clamp(record.host, start - (ev.clientY - startY));
            apply(record, height);
        }

        function up(ev) {
            if (ev.pointerId !== pointer) return;
            grip.removeEventListener('pointermove', move);
            grip.removeEventListener('pointerup', up);
            grip.removeEventListener('pointercancel', up);
            if (grip.hasPointerCapture(pointer)) grip.releasePointerCapture(pointer);
            commit(record, height);
        }

        grip.addEventListener('pointermove', move);
        grip.addEventListener('pointerup', up);
        grip.addEventListener('pointercancel', up);
    }

    function onKey(record, e) {
        var height = null;
        if (e.key === 'ArrowUp') height = current(record) + STEP;
        else if (e.key === 'ArrowDown') height = current(record) - STEP;
        else if (e.key === 'PageUp') height = current(record) + PAGE;
        else if (e.key === 'PageDown') height = current(record) - PAGE;
        else if (e.key === 'Home') height = MIN;
        else if (e.key === 'End') height = limit(record.host);
        if (height === null) return;

        e.preventDefault();
        setHeight(record, height);
    }

    function dispose(grip) {
        var record = grips.get(grip);
        if (!record) return;
        grip.removeEventListener('pointerdown', record.onDown);
        grip.removeEventListener('keydown', record.onKey);
        grips.delete(grip);
    }

    window.addEventListener('resize', function () {
        grips.forEach(function (record, grip) {
            if (!grip.isConnected) {
                dispose(grip);
                return;
            }

            var height = current(record);
            var next = clamp(record.host, height);
            apply(record, next);
            if (next !== Math.round(height)) commit(record, next);
        });
    });

    return {
        register: function (grip, reference) {
            if (!grip) return;

            var record = grips.get(grip);
            if (record) {
                record.reference = reference;
                return;
            }

            var notes = grip.closest('.notes');
            if (!notes || !notes.parentElement) return;

            record = { grip: grip, notes: notes, host: notes.parentElement, reference: reference };
            record.onDown = function (e) { onDown(record, e); };
            record.onKey = function (e) { onKey(record, e); };
            grips.set(grip, record);
            grip.addEventListener('pointerdown', record.onDown);
            grip.addEventListener('keydown', record.onKey);
            apply(record, clamp(record.host, current(record)));
        },
        unregister: function (grip) {
            dispose(grip);
        }
    };
})();

window.docuHelperPptSorter = (function () {
    var bound = new WeakSet();

    function cellsOf(section) {
        return Array.prototype.slice.call(section.querySelectorAll('.cell'));
    }

    function columnsOf(cells) {
        if (cells.length === 0) return 1;
        var top = cells[0].offsetTop;
        var count = 0;
        for (var i = 0; i < cells.length && cells[i].offsetTop === top; i++) count++;
        return Math.max(1, count);
    }

    function onKey(section, e) {
        var cell = e.target && e.target.closest ? e.target.closest('.cell') : null;
        if (!cell || !section.contains(cell) || e.ctrlKey || e.altKey || e.metaKey) return;

        var cells = cellsOf(section);
        var index = cells.indexOf(cell);
        var columns = columnsOf(cells);
        var target;
        switch (e.key) {
            case 'ArrowRight': target = index + 1; break;
            case 'ArrowLeft': target = index - 1; break;
            case 'ArrowDown': target = index + columns; break;
            case 'ArrowUp': target = index - columns; break;
            case 'Home': target = 0; break;
            case 'End': target = cells.length - 1; break;
            default: return;
        }

        e.preventDefault();
        target = Math.min(cells.length - 1, Math.max(0, target));
        if (target !== index) cells[target].focus();
    }

    return {
        bind: function (section) {
            if (!section || bound.has(section)) return;
            bound.add(section);
            section.addEventListener('keydown', function (e) { onKey(section, e); });
        }
    };
})();
