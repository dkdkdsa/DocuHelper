window.docuHelperPptCanvas = (function () {
    var sessions = new Map();
    var MIN_SIZE = 0.2;
    var KEYS = ['Delete', 'Backspace', 'ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown', 'Escape'];

    function send(session, payload) {
        if (!session.reference) return;
        session.reference.invokeMethodAsync('Receive', payload).catch(function () { });
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
            rot: percentOf(el, 'rot')
        };
    }

    function selectedMovable(slide) {
        return Array.prototype.slice.call(slide.querySelectorAll('.shape.on[data-movable="true"]'));
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

    function startDrag(session, slide, e, kind, shape, handle) {
        var group = kind === 'move' && shape.classList.contains('on')
            ? selectedMovable(slide)
            : [shape];
        var items = group.map(itemOf);
        if (items.length === 0) return;

        session.drag = {
            kind: kind,
            id: shape.dataset.shape,
            handle: handle,
            items: items,
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

    function onDown(slide, session, e) {
        if (e.button !== 0 || slide.dataset.locked === 'true') return;

        var target = e.target;
        var handle = target.closest ? target.closest('[data-handle]') : null;
        var shape = target.closest ? target.closest('[data-shape]') : null;

        if (handle && shape && slide.contains(shape) && shape.classList.contains('on') && shape.dataset.movable === 'true') {
            var kind = handle.dataset.handle === 'rot' ? 'rotate' : 'resize';
            startDrag(session, slide, e, kind, shape, handle.dataset.handle);
            e.preventDefault();
            return;
        }

        if (!shape || !slide.contains(shape)) {
            if (slide.querySelector('.shape.on')) {
                send(session, { kind: 'select', id: null, additive: false });
            }
            return;
        }

        var editing = target.isContentEditable === true;
        if (!shape.classList.contains('on')) {
            send(session, { kind: 'select', id: shape.dataset.shape, additive: e.shiftKey });
        }

        if (shape.dataset.movable !== 'true') return;
        if (editing && shape.classList.contains('on')) return;

        if (!editing) e.preventDefault();
        startDrag(session, slide, e, 'move', shape, null);
    }

    function onMove(session, e) {
        var drag = session.drag;
        if (!drag) return;

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

    function onUp(session) {
        var drag = session.drag;
        if (!drag) return;
        session.drag = null;

        drag.items.forEach(function (item) { restoreOverrides(item); });
        if (!drag.moved) return;

        if (drag.kind === 'move') {
            send(session, { kind: 'move', id: drag.id, dx: drag.dx, dy: drag.dy });
        } else if (drag.kind === 'resize') {
            send(session, { kind: 'resize', id: drag.id, handle: drag.handle, dx: drag.dx, dy: drag.dy });
        } else if (drag.kind === 'rotate') {
            send(session, { kind: 'rotate', id: drag.id, angle: drag.angle });
        }
    }

    function onKey(slide, session, e) {
        if (slide.dataset.locked === 'true' || KEYS.indexOf(e.key) < 0) return;
        if (!slide.querySelector('.shape.on')) return;

        var active = document.activeElement;
        if (active && (active.isContentEditable || active.tagName === 'INPUT' || active.tagName === 'TEXTAREA' || active.tagName === 'SELECT')) return;
        if (active && active !== document.body && !slide.contains(active)) return;

        e.preventDefault();
        send(session, { kind: 'key', key: e.key, shift: e.shiftKey });
    }

    function dispose(slide) {
        var session = sessions.get(slide);
        if (!session) return;

        slide.removeEventListener('pointerdown', session.onDown);
        window.removeEventListener('pointermove', session.onMove);
        window.removeEventListener('pointerup', session.onUp);
        window.removeEventListener('pointercancel', session.onUp);
        document.removeEventListener('keydown', session.onKey);
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

            session = { reference: reference, drag: null };
            session.onDown = function (e) { onDown(slide, session, e); };
            session.onMove = function (e) { onMove(session, e); };
            session.onUp = function (e) { onUp(session, e); };
            session.onKey = function (e) { onKey(slide, session, e); };

            sessions.set(slide, session);
            slide.addEventListener('pointerdown', session.onDown);
            window.addEventListener('pointermove', session.onMove);
            window.addEventListener('pointerup', session.onUp);
            window.addEventListener('pointercancel', session.onUp);
            document.addEventListener('keydown', session.onKey);
        },
        detach: function (slide) {
            dispose(slide);
        }
    };
})();
