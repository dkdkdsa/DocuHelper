export function watch(element, dotnet, breakpoints) {
    let last = 0;
    const levelOf = (width) => {
        let level = 1;
        for (const point of breakpoints) {
            if (width >= point) level++;
        }
        return level;
    };
    const report = () => {
        const panel = element.querySelector('[data-ribbon-panel]');
        const bar = element.querySelector('[data-ribbon-tabs]');
        const width = (panel || bar || element).clientWidth;
        const level = levelOf(width);
        if (level === last) return;
        last = level;
        dotnet.invokeMethodAsync('SetLevel', level).catch(() => { });
    };
    const observer = new ResizeObserver(report);
    observer.observe(element);
    report();
    return { dispose() { observer.disconnect(); } };
}
