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
