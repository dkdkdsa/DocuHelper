window.docuHelperDashCanvas = (function () {
    var sessions = new Map();

    function shapeOf(stage, target) {
        var shape = target && target.closest ? target.closest('[data-shape]') : null;
        return shape && stage.contains(shape) ? shape : null;
    }

    function editable(shape) {
        return shape && shape.dataset.text === 'true' && shape.dataset.movable === 'true';
    }

    function dispose(stage) {
        var session = sessions.get(stage);
        if (!session) return;
        stage.removeEventListener('dblclick', session.onDblClick);
        document.removeEventListener('keydown', session.onKey);
        sessions.delete(stage);
    }

    return {
        attach: function (stage, reference) {
            if (!stage) return;
            sessions.forEach(function (_, element) {
                if (!element.isConnected) dispose(element);
            });

            var session = sessions.get(stage);
            if (session) {
                session.reference = reference;
                return;
            }

            session = { reference: reference };
            session.onDblClick = function (e) {
                if (stage.dataset.locked === 'true') return;
                var shape = shapeOf(stage, e.target);
                if (!editable(shape) || e.target.closest('textarea')) return;
                session.reference.invokeMethodAsync('EditText', shape.dataset.shape).catch(function () { });
            };
            session.onKey = function (e) {
                if (e.key !== 'Enter' && e.key !== 'F2') return;
                if (e.ctrlKey || e.altKey || e.metaKey || stage.dataset.locked === 'true') return;
                var active = document.activeElement;
                if (!active || !stage.contains(active) || active.tagName === 'TEXTAREA' || active.tagName === 'INPUT') return;
                var shape = active.dataset && active.dataset.shape ? active : null;
                if (!shape || !shape.classList.contains('on') || !editable(shape)) return;
                e.preventDefault();
                session.reference.invokeMethodAsync('EditText', shape.dataset.shape).catch(function () { });
            };

            sessions.set(stage, session);
            stage.addEventListener('dblclick', session.onDblClick);
            document.addEventListener('keydown', session.onKey);
        },
        detach: function (stage) {
            dispose(stage);
        },
        pointOnPage: function (root, clientX, clientY) {
            var page = root ? root.querySelector('[data-slide="dash"]') : null;
            if (!page) return null;
            var rect = page.getBoundingClientRect();
            if (rect.width <= 0 || rect.height <= 0) return null;
            var x = (clientX - rect.left) / rect.width;
            var y = (clientY - rect.top) / rect.height;
            return [Math.min(Math.max(x, 0), 1), Math.min(Math.max(y, 0), 1)];
        },
        focusEditor: function (stage) {
            var editor = stage ? stage.querySelector('textarea.editor') : null;
            if (!editor) return false;
            editor.focus({ preventScroll: true });
            var end = editor.value.length;
            editor.setSelectionRange(end, end);
            return true;
        }
    };
})();

window.docuHelperDragHooks = window.docuHelperDragHooks || {};
window.docuHelperDragHooks.dash = (function () {
    var PURPLE = '#5B3FA0';
    var TOLERANCE = 6;
    var EPSILON = 0.001;
    var states = new Map();

    function pageSize(slide) {
        return { w: parseFloat(slide.dataset.pw) || 1123, h: parseFloat(slide.dataset.ph) || 794 };
    }

    function release(slide) {
        var state = states.get(slide);
        if (!state) return;
        if (state.layer) state.layer.remove();
        states.delete(slide);
    }

    function stateOf(slide, drag) {
        var state = states.get(slide);
        if (state && state.drag === drag) return state;
        release(slide);

        var size = pageSize(slide);
        var grabbed = drag.items.map(function (item) { return item.el; });
        var xs = [0, 56, size.w / 2, size.w - 56, size.w].map(function (v) { return v / size.w * 100; });
        var ys = [0, 48, size.h / 2, size.h - 48, size.h].map(function (v) { return v / size.h * 100; });
        Array.prototype.forEach.call(slide.children, function (el) {
            if (!el.dataset || el.dataset.shape === undefined || grabbed.indexOf(el) >= 0) return;
            var x = parseFloat(el.dataset.x);
            var y = parseFloat(el.dataset.y);
            var w = parseFloat(el.dataset.w);
            var h = parseFloat(el.dataset.h);
            if (!isFinite(x) || !isFinite(y) || !isFinite(w) || !isFinite(h)) return;
            xs.push(x, x + w / 2, x + w);
            ys.push(y, y + h / 2, y + h);
        });

        state = { drag: drag, size: size, xs: xs, ys: ys, layer: null, badge: null, lines: [] };
        states.set(slide, state);
        return state;
    }

    function layerOf(state) {
        if (state.layer) return state.layer;
        var layer = document.createElement('div');
        layer.setAttribute('aria-hidden', 'true');
        layer.style.cssText = 'position:fixed;left:0;top:0;width:0;height:0;pointer-events:none;z-index:2147483000';
        document.body.appendChild(layer);
        state.layer = layer;
        return layer;
    }

    function drawGuides(state, rect, vertical, horizontal) {
        state.lines.forEach(function (line) { line.remove(); });
        state.lines = [];
        vertical.forEach(function (pct) {
            var x = rect.left + pct / 100 * rect.width;
            addLine(state, 'left:' + x + 'px;top:' + rect.top + 'px;width:1px;height:' + rect.height + 'px');
        });
        horizontal.forEach(function (pct) {
            var y = rect.top + pct / 100 * rect.height;
            addLine(state, 'left:' + rect.left + 'px;top:' + y + 'px;width:' + rect.width + 'px;height:1px');
        });
    }

    function addLine(state, css) {
        var line = document.createElement('div');
        line.style.cssText = 'position:absolute;background:' + PURPLE + ';' + css;
        layerOf(state).appendChild(line);
        state.lines.push(line);
    }

    function showBadge(state, rect, leftPct, bottomPct, text) {
        var badge = state.badge;
        if (!badge) {
            badge = document.createElement('div');
            badge.style.cssText = 'position:absolute;padding:2px 7px;border-radius:5px;background:' + PURPLE + ';color:#fff;font:700 11px/1.5 sans-serif;white-space:nowrap;font-variant-numeric:tabular-nums';
            layerOf(state).appendChild(badge);
            state.badge = badge;
        }
        badge.textContent = text;
        badge.style.left = (rect.left + leftPct / 100 * rect.width) + 'px';
        badge.style.top = (rect.top + bottomPct / 100 * rect.height + 8) + 'px';
    }

    function nearest(edges, targets, tolerance) {
        var best = null;
        edges.forEach(function (edge) {
            targets.forEach(function (target) {
                var d = target - edge;
                if (Math.abs(d) <= tolerance && (best === null || Math.abs(d) < Math.abs(best))) best = d;
            });
        });
        return best;
    }

    function matches(edges, targets) {
        var found = [];
        targets.forEach(function (target) {
            if (found.indexOf(target) >= 0) return;
            for (var i = 0; i < edges.length; i++) {
                if (Math.abs(edges[i] - target) < EPSILON) {
                    found.push(target);
                    return;
                }
            }
        });
        return found;
    }

    function enabled(slide, e) {
        return slide.dataset.magnet !== 'false' && !(e && e.altKey);
    }

    return {
        move: function (slide, drag, fx, fy, e) {
            var state = stateOf(slide, drag);
            var rect = slide.getBoundingClientRect();
            var minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
            drag.items.forEach(function (item) {
                minX = Math.min(minX, item.x);
                maxX = Math.max(maxX, item.x + item.w);
                minY = Math.min(minY, item.y);
                maxY = Math.max(maxY, item.y + item.h);
            });
            var width = maxX - minX;
            var height = maxY - minY;
            var rotated = drag.items.some(function (item) { return item.rot !== 0; });
            var vertical = [];
            var horizontal = [];

            if (enabled(slide, e) && !rotated) {
                var left = minX + fx;
                var top = minY + fy;
                var dx = nearest([left, left + width / 2, left + width], state.xs, TOLERANCE / drag.box.width * 100);
                var dy = nearest([top, top + height / 2, top + height], state.ys, TOLERANCE / drag.box.height * 100);
                if (dx !== null) fx += dx;
                if (dy !== null) fy += dy;
                left = minX + fx;
                top = minY + fy;
                vertical = matches([left, left + width / 2, left + width], state.xs);
                horizontal = matches([top, top + height / 2, top + height], state.ys);
            }

            drawGuides(state, rect, vertical, horizontal);
            var x = minX + fx;
            var y = minY + fy;
            showBadge(state, rect, x, y + height, Math.round(x / 100 * state.size.w) + ', ' + Math.round(y / 100 * state.size.h));
            return { fx: fx, fy: fy };
        },
        resize: function (slide, drag, lx, ly, e) {
            var state = stateOf(slide, drag);
            var rect = slide.getBoundingClientRect();
            var first = drag.items[0];
            var handle = drag.handle || '';
            var toleranceX = TOLERANCE / drag.box.width * 100;
            var toleranceY = TOLERANCE / drag.box.height * 100;
            var vertical = [];
            var horizontal = [];

            if (enabled(slide, e) && first.rot === 0) {
                if (handle.indexOf('e') >= 0) {
                    var de = nearest([first.x + first.w + lx], state.xs, toleranceX);
                    if (de !== null) lx += de;
                    vertical = matches([first.x + first.w + lx], state.xs);
                } else if (handle.indexOf('w') >= 0) {
                    var dw = nearest([first.x + lx], state.xs, toleranceX);
                    if (dw !== null) lx += dw;
                    vertical = matches([first.x + lx], state.xs);
                }
                if (handle.indexOf('s') >= 0) {
                    var ds = nearest([first.y + first.h + ly], state.ys, toleranceY);
                    if (ds !== null) ly += ds;
                    horizontal = matches([first.y + first.h + ly], state.ys);
                } else if (handle.indexOf('n') >= 0) {
                    var dn = nearest([first.y + ly], state.ys, toleranceY);
                    if (dn !== null) ly += dn;
                    horizontal = matches([first.y + ly], state.ys);
                }
            }

            var width = Math.max(first.w + (handle.indexOf('e') >= 0 ? lx : handle.indexOf('w') >= 0 ? -lx : 0), 0);
            var height = Math.max(first.h + (handle.indexOf('s') >= 0 ? ly : handle.indexOf('n') >= 0 ? -ly : 0), 0);
            var left = handle.indexOf('w') >= 0 ? first.x + lx : first.x;
            var top = handle.indexOf('n') >= 0 ? first.y + ly : first.y;
            drawGuides(state, rect, vertical, horizontal);
            showBadge(state, rect, left, top + height, Math.round(width / 100 * state.size.w) + ' × ' + Math.round(height / 100 * state.size.h));
            return { lx: lx, ly: ly };
        },
        end: function (slide) {
            release(slide);
        }
    };
})();
