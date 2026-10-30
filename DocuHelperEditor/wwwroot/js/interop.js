window.docuHelper = (function () {
    var handler = null;

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
        focus: function (selector) {
            var el = document.querySelector(selector);
            if (el) el.focus();
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
        prefersReducedMotion: function () {
            return window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        }
    };
})();
