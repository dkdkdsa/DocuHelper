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

    return {
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
                }
            };
            document.addEventListener('keydown', handler);
        },
        unregisterShortcuts: function () {
            if (handler) document.removeEventListener('keydown', handler);
            handler = null;
        },
        cycleRegion: cycleRegion,
        focus: function (selector) {
            var el = document.querySelector(selector);
            if (el) el.focus();
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
