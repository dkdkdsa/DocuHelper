function onKeyDown(e) {
    if (e.key === 'Enter' && !e.shiftKey && !e.ctrlKey && !e.altKey && !e.metaKey && !e.isComposing && e.keyCode !== 229) {
        e.preventDefault();
    }
}

export function setValue(el, text) {
    if (el && el.value !== text) {
        el.value = text;
    }
}

export function guardEnter(el) {
    if (el) {
        el.addEventListener('keydown', onKeyDown);
    }
}
