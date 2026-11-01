window.docuHelperDialog = (function () {
    var FOCUSABLE = 'input:not([disabled]), select:not([disabled]), textarea:not([disabled]), button:not([disabled]), [tabindex]:not([tabindex="-1"])';
    var active = null;
    var returnFocus = null;

    function trap(e) {
        if (e.key !== 'Tab' || !active) return;
        var items = Array.prototype.slice.call(active.querySelectorAll(FOCUSABLE)).filter(function (el) { return el.offsetParent !== null; });
        if (items.length === 0) return;
        var first = items[0];
        var last = items[items.length - 1];
        if (e.shiftKey && (document.activeElement === first || document.activeElement === active)) {
            e.preventDefault();
            last.focus();
        } else if (!e.shiftKey && document.activeElement === last) {
            e.preventDefault();
            first.focus();
        }
    }

    return {
        open: function (dialog) {
            if (!dialog) return;
            returnFocus = document.activeElement;
            active = dialog;
            dialog.addEventListener('keydown', trap);
            var first = dialog.querySelector('input:not([disabled]), select:not([disabled]), textarea:not([disabled])') || dialog.querySelector('button:not([disabled])') || dialog;
            first.focus();
        },
        close: function () {
            if (active) {
                active.removeEventListener('keydown', trap);
                active = null;
            }
            if (returnFocus && typeof returnFocus.focus === 'function') returnFocus.focus();
            returnFocus = null;
        }
    };
})();
