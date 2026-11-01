window.docuHelperWordCanvas = (function () {
    var observers = new Map();

    function measure(canvas) {
        if (!canvas) return null;
        var page = canvas.querySelector('article.page');
        if (!page) return null;
        var sheet = page.querySelector('.sheet') || page;
        var box = page.getBoundingClientRect();
        var origin = canvas.getBoundingClientRect();
        var style = getComputedStyle(sheet);
        return {
            viewportWidth: canvas.clientWidth,
            viewportHeight: canvas.clientHeight,
            pageWidth: box.width,
            pageHeight: box.height,
            pageLeft: box.left - origin.left,
            pageTop: box.top - origin.top,
            paddingLeft: parseFloat(style.paddingLeft) || 0,
            paddingRight: parseFloat(style.paddingRight) || 0,
            paddingTop: parseFloat(style.paddingTop) || 0,
            paddingBottom: parseFloat(style.paddingBottom) || 0
        };
    }

    return {
        measure: function (canvas) {
            return JSON.stringify(measure(canvas));
        },
        observe: function (canvas, reference) {
            if (!canvas || !window.ResizeObserver || observers.has(canvas)) return;
            var observer = new ResizeObserver(function () {
                reference.invokeMethodAsync('Resized').catch(function () { });
            });
            observer.observe(canvas);
            observers.set(canvas, observer);
        },
        unobserve: function (canvas) {
            var observer = observers.get(canvas);
            if (observer) {
                observer.disconnect();
                observers.delete(canvas);
            }
        },
        layoutBalloons: function (canvas) {
            if (!canvas) return;
            canvas.querySelectorAll('article.page').forEach(function (page) {
                var balloons = Array.prototype.slice.call(page.querySelectorAll('.bal'));
                balloons.forEach(function (balloon) { balloon.style.transform = ''; });
                var placed = balloons.map(function (balloon) {
                    var box = balloon.getBoundingClientRect();
                    return { element: balloon, top: box.top, height: box.height };
                }).sort(function (a, b) { return a.top - b.top; });
                var floor = -Infinity;
                placed.forEach(function (item) {
                    var shift = Math.max(0, floor + 8 - item.top);
                    if (shift > 0) item.element.style.transform = 'translateY(' + shift + 'px)';
                    floor = item.top + shift + item.height;
                });
            });
        },
        scrollToBlock: function (canvas, id) {
            var target = canvas && canvas.querySelector('[data-block="' + id + '"]');
            if (target) target.scrollIntoView({ block: 'start' });
        },
        scrollToPage: function (canvas, page) {
            var target = canvas && canvas.querySelector('article[aria-label="' + page + '쪽"]');
            if (target) target.scrollIntoView({ block: 'start' });
        }
    };
})();
