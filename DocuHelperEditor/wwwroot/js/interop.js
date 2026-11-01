window.docuHelper = (function () {
    var handler = null;
    var rememberedFocus = null;
    var lastFocused = null;

    document.addEventListener('focusin', function (e) {
        if (e.target && e.target !== document.body) lastFocused = e.target;
    }, true);

    function fitPage(sheet, piece, scale, bottomPx) {
        var factor = scale;
        for (var attempt = 0; attempt < 60; attempt++) {
            var limit = sheet.getBoundingClientRect().top + sheet.clientHeight - bottomPx;
            if (piece.getBoundingClientRect().bottom <= limit && sheet.scrollWidth <= sheet.clientWidth + 1)
                return;

            factor *= 0.97;
            piece.style.zoom = factor;
        }
    }

    function removeSheetPrint() {
        document.querySelectorAll('.docu-sheet-print').forEach(function (node) {
            node.remove();
        });
    }

    function sheetPiece(table, range, layout, zoom) {
        var copy = table.cloneNode(false);
        copy.style.zoom = layout.scale;
        copy.style.tableLayout = 'fixed';
        copy.style.borderCollapse = 'collapse';

        var head = table.querySelector('thead');
        if (head) {
            var headCopy = head.cloneNode(false);
            Array.prototype.forEach.call(head.rows, function (row) {
                headCopy.appendChild(rowPiece(row, range, layout, zoom));
            });
            copy.appendChild(headCopy);
        }

        var bodyCopy = document.createElement('tbody');
        var body = table.tBodies[0];
        if (body) {
            Array.prototype.forEach.call(body.rows, function (row) {
                if (row.dataset.r === undefined)
                    return;

                var r = Number(row.dataset.r);
                if (r < range.r0 || r > range.r1)
                    return;

                bodyCopy.appendChild(rowPiece(row, range, layout, zoom));
            });
        }

        copy.appendChild(bodyCopy);
        return copy;
    }

    function rowPiece(row, range, layout, zoom) {
        var rowCopy = row.cloneNode(false);
        if (row.dataset.r !== undefined) {
            var points = layout.rowPoints[Number(row.dataset.r)];
            if (points === 0)
                rowCopy.style.display = 'none';
            else if (points !== undefined)
                rowCopy.style.height = (points * 4 / 3) + 'px';
        }

        Array.prototype.forEach.call(row.cells, function (cell) {
            var width = cell.getBoundingClientRect().width / zoom;
            if (cell.dataset.c !== undefined) {
                var c = Number(cell.dataset.c);
                if (c < range.c0 || c > range.c1)
                    return;

                if (layout.columnPoints[c] !== undefined)
                    width = layout.columnPoints[c] * 4 / 3;
            }

            var cellCopy = cell.cloneNode(true);
            cellCopy.style.width = width + 'px';
            cellCopy.style.paddingTop = '0px';
            cellCopy.style.paddingBottom = '0px';
            cellCopy.style.lineHeight = '1.2';
            rowCopy.appendChild(cellCopy);
        });
        return rowCopy;
    }

    function isPrintPage(element) {
        return element instanceof HTMLElement && element.matches('.page');
    }

    function hasPrintPage(element) {
        return Array.prototype.some.call(element.querySelectorAll('.page'), isPrintPage);
    }

    function markPrintTree(root) {
        Array.prototype.forEach.call(root.children, function (child) {
            if (isPrintPage(child)) {
                child.classList.add('docu-print-keep', 'docu-print-page');
                return;
            }

            if (hasPrintPage(child)) {
                child.classList.add('docu-print-keep');
                markPrintTree(child);
                return;
            }

            child.classList.add('docu-print-hide');
        });
    }

    function isEditable(el) {
        if (!el) return false;
        var tag = el.tagName;
        return tag === 'INPUT' || tag === 'TEXTAREA' || tag === 'SELECT' || el.isContentEditable;
    }

    function focusable(root) {
        return root.querySelector('[data-region-focus]')
            || root.querySelector('button:not([disabled]), a[href], input:not([disabled]), textarea:not([disabled]), select:not([disabled]), [tabindex]:not([tabindex="-1"])')
            || root;
    }

    function cycleRegion(step) {
        var regions = Array.prototype.slice.call(document.querySelectorAll('[data-region]'));
        if (!regions.length) return;
        var current = -1;
        for (var i = 0; i < regions.length; i++) {
            if (regions[i].contains(document.activeElement)) {
                current = i;
                break;
            }
        }
        var next = regions[(current + step + regions.length) % regions.length];
        focusable(next).focus();
    }

    function blockFileDrop(e) {
        var types = e.dataTransfer && e.dataTransfer.types;
        if (!types || Array.prototype.indexOf.call(types, 'Files') < 0) return;
        e.preventDefault();
        if (e.type === 'dragover') e.dataTransfer.dropEffect = 'none';
    }

    document.addEventListener('dragover', blockFileDrop);
    document.addEventListener('drop', blockFileDrop);

    var composing = null;
    var typed = new WeakMap();
    var echoWindowMs = 1000;

    document.addEventListener('compositionstart', function (e) { composing = e.target; }, true);
    document.addEventListener('compositionend', function () { composing = null; }, true);
    document.addEventListener('input', function (e) {
        var el = e.target;
        if (!(el instanceof HTMLInputElement || el instanceof HTMLTextAreaElement)) return;
        var now = Date.now();
        var list = (typed.get(el) || []).filter(function (t) { return now - t.at < echoWindowMs; });
        list.push({ text: el.value, at: now });
        typed.set(el, list);
    }, true);

    function isLateEcho(el, text) {
        var now = Date.now();
        var list = typed.get(el);
        if (!list) return false;
        return list.some(function (t) { return t.text === text && now - t.at < echoWindowMs; });
    }

    [window.HTMLInputElement, window.HTMLTextAreaElement].forEach(function (type) {
        var value = Object.getOwnPropertyDescriptor(type.prototype, 'value');
        Object.defineProperty(type.prototype, 'value', {
            configurable: true,
            enumerable: value.enumerable,
            get: function () { return value.get.call(this); },
            set: function (text) {
                var next = text == null ? '' : String(text);
                if (this === composing || next === value.get.call(this) || isLateEcho(this, next)) return;
                typed.delete(this);
                value.set.call(this, next);
            }
        });
    });

    return {
        setTextSize: function (size) {
            if (size && size !== 'm') document.documentElement.setAttribute('data-text-size', size);
            else document.documentElement.removeAttribute('data-text-size');
        },
        registerShortcuts: function (dotnet) {
            if (handler) document.removeEventListener('keydown', handler);
            handler = function (e) {
                var key = (e.key || '').toLowerCase();
                if (e.key === 'F6') {
                    e.preventDefault();
                    cycleRegion(e.shiftKey ? -1 : 1);
                } else if ((e.ctrlKey || e.metaKey) && !e.altKey && key === 's') {
                    e.preventDefault();
                    dotnet.invokeMethodAsync('OnSaveShortcut').catch(function () { });
                } else if ((e.ctrlKey || e.metaKey) && !e.altKey && !e.shiftKey && key === 'z' && !isEditable(e.target)) {
                    e.preventDefault();
                    dotnet.invokeMethodAsync('OnUndoShortcut').catch(function () { });
                } else if ((e.ctrlKey || e.metaKey) && !e.altKey && ((!e.shiftKey && key === 'y') || (e.shiftKey && key === 'z')) && !isEditable(e.target)) {
                    e.preventDefault();
                    dotnet.invokeMethodAsync('OnRedoShortcut').catch(function () { });
                }
            };
            document.addEventListener('keydown', handler);
        },
        unregisterShortcuts: function () {
            if (handler) document.removeEventListener('keydown', handler);
            handler = null;
        },
        cycleRegion: cycleRegion,
        selectText: function (selector) {
            var el = document.querySelector(selector);
            if (!el) return;
            el.focus();
            el.select();
        },
        rememberFocus: function () {
            rememberedFocus = lastFocused;
        },
        restoreFocus: function () {
            if (rememberedFocus && document.contains(rememberedFocus)) rememberedFocus.focus();
            rememberedFocus = null;
        },
        focus: function (selector) {
            var el = document.querySelector(selector);
            if (el) el.focus();
        },
        nextFrame: function () {
            return new Promise(function (resolve) {
                requestAnimationFrame(function () {
                    setTimeout(resolve, 30);
                });
            });
        },
        scrollToTop: function (selector) {
            var el = document.querySelector(selector);
            if (el) el.scrollTop = 0;
        },
        focusPaperStart: function () {
            var paper = document.querySelector('[data-region="paper"]');
            if (!paper) return;
            var target = paper.querySelector('input.sink') || paper.querySelector('[contenteditable]:not([contenteditable="false"])');
            if (target) target.focus();
        },
        focusUnlessBusy: function (selector) {
            var active = document.activeElement;
            if (active && active !== document.body && !active.closest('[data-region="assistant"]')) return;
            var el = document.querySelector(selector);
            if (el) el.focus();
        },
        scrollIntoView: function (selector) {
            var el = document.querySelector(selector);
            if (el) el.scrollIntoView({ block: 'nearest', inline: 'nearest' });
        },
        focusById: function (id) {
            var el = document.getElementById(id);
            if (el) el.focus();
        },
        prefersReducedMotion: function () {
            return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        },
        printEnter: function () {
            var pages = Array.prototype.slice.call(document.querySelectorAll('.page')).filter(isPrintPage);
            if (pages.length === 0)
                return 0;

            markPrintTree(document.body);
            pages[pages.length - 1].classList.add('docu-print-last');
            document.body.classList.add('docu-printing');
            return pages.length;
        },
        sheetPrintEnter: function (layout) {
            var table = document.querySelector('table.sheet');
            if (!table || !layout || !layout.pages || layout.pages.length === 0)
                return 0;

            removeSheetPrint();
            var container = document.createElement('div');
            container.className = 'docu-sheet-print';
            document.body.appendChild(container);

            var zoom = parseFloat(table.style.zoom) || 1;
            layout.pages.forEach(function (range) {
                var sheet = document.createElement('div');
                sheet.className = 'page';
                sheet.style.setProperty('width', layout.widthCm + 'cm', 'important');
                sheet.style.setProperty('height', layout.heightCm + 'cm', 'important');
                sheet.style.setProperty('box-sizing', 'border-box', 'important');
                sheet.style.setProperty('padding', layout.margins.top + 'cm ' + layout.margins.right + 'cm 0cm ' + layout.margins.left + 'cm', 'important');
                container.appendChild(sheet);
                var piece = sheetPiece(table, range, layout, zoom);
                sheet.appendChild(piece);
                fitPage(sheet, piece, layout.scale, layout.margins.bottom * 37.8);
            });

            markPrintTree(document.body);
            container.lastElementChild.classList.add('docu-print-last');
            document.body.classList.add('docu-printing');
            return layout.pages.length;
        },
        printExit: function () {
            removeSheetPrint();
            document.body.classList.remove('docu-printing');
            document.querySelectorAll('.docu-print-hide, .docu-print-keep, .docu-print-page, .docu-print-last').forEach(function (el) {
                el.classList.remove('docu-print-hide', 'docu-print-keep', 'docu-print-page', 'docu-print-last');
            });
        }
    };
})();
