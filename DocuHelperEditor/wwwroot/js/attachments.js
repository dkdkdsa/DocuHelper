const IMPORT_MESSAGE = 'docuhelper:files';
const IMAGE_EXTENSIONS = ['png', 'jpg', 'jpeg', 'gif', 'webp', 'bmp'];
const TEXT_EXTENSIONS = ['txt', 'csv', 'tsv', 'md'];
const TEXT_TYPES = ['text/plain', 'text/csv', 'text/markdown', 'text/tab-separated-values'];
const DIRECT_TYPES = ['image/png', 'image/jpeg', 'image/gif', 'image/webp'];
const ACCEPT = '.xlsx,.docx,.pptx,.hwp,.hwpx,.xls,.doc,.ppt,.png,.jpg,.jpeg,.gif,.webp,.bmp,.pdf,.txt,.csv,.tsv,.md';
const THUMB_EDGE = 320;
const PNG_SOFT_LIMIT = 1024 * 1024;
const DRAG_TIMEOUT = 1500;

let host = null;
let limits = null;
let input = null;
let dragging = false;
let dragTimer = 0;
let targets = [];
let pasteCount = 0;

function noop() { }

function extensionOf(name) {
    const dot = (name || '').lastIndexOf('.');
    return dot < 0 ? '' : name.slice(dot + 1).toLowerCase();
}

function kindOf(file) {
    const extension = extensionOf(file.name);
    const type = (file.type || '').toLowerCase();
    if (IMAGE_EXTENSIONS.includes(extension) || type.startsWith('image/')) return 'image';
    if (extension === 'pdf' || type === 'application/pdf') return 'pdf';
    if (TEXT_EXTENSIONS.includes(extension) || TEXT_TYPES.includes(type)) return 'text';
    return null;
}

function dragKindOf(type) {
    type = (type || '').toLowerCase();
    if (type.includes('hwp')) return 'hwp';
    if (type.startsWith('image/')) return 'image';
    if (type === 'application/pdf') return 'pdf';
    if (TEXT_TYPES.includes(type)) return 'text';
    if (type.includes('spreadsheetml')) return 'excel';
    if (type.includes('wordprocessingml')) return 'word';
    if (type.includes('presentationml')) return 'ppt';
    return 'other';
}

function hasFiles(e) {
    const types = e.dataTransfer && e.dataTransfer.types;
    return !!types && Array.prototype.indexOf.call(types, 'Files') >= 0;
}

function onDragOver(e) {
    if (!hasFiles(e)) return;
    e.preventDefault();
    e.stopPropagation();
    e.dataTransfer.dropEffect = 'copy';
    if (targets.length === 0) targets.push(e.target);
    clearTimeout(dragTimer);
    dragTimer = setTimeout(endDrag, DRAG_TIMEOUT);
    if (dragging || !host) return;
    dragging = true;
    const kinds = Array.prototype.filter.call(e.dataTransfer.items || [], item => item.kind === 'file')
        .map(item => dragKindOf(item.type));
    host.invokeMethodAsync('OnFilesDragEnter', kinds).catch(noop);
}

function onDragEnter(e) {
    if (!hasFiles(e)) return;
    if (!targets.includes(e.target)) targets.push(e.target);
    onDragOver(e);
}

function onDragLeave(e) {
    if (!hasFiles(e)) return;
    e.stopPropagation();
    targets = targets.filter(target => target !== e.target && target.isConnected);
    if (targets.length === 0) endDrag();
}

function endDrag() {
    clearTimeout(dragTimer);
    targets = [];
    if (!dragging) return;
    dragging = false;
    if (host) host.invokeMethodAsync('OnFilesDragLeave').catch(noop);
}

function onDrop(e) {
    if (!hasFiles(e)) return;
    e.preventDefault();
    e.stopPropagation();
    const entries = collect(e.dataTransfer);
    endDrag();
    take(entries, 'drop');
}

function collect(dataTransfer) {
    const entries = [];
    const items = dataTransfer.items;
    if (items && items.length) {
        for (let i = 0; i < items.length; i++) {
            const item = items[i];
            if (item.kind !== 'file') continue;
            const file = item.getAsFile();
            if (!file) continue;
            const entry = item.webkitGetAsEntry ? item.webkitGetAsEntry() : null;
            entries.push({ file, folder: !!(entry && entry.isDirectory) });
        }
        return entries;
    }
    Array.prototype.forEach.call(dataTransfer.files || [], file => entries.push({ file, folder: false }));
    return entries;
}

function onPaste(e) {
    if (!host || !e.target || e.target.id !== 'composer-input') return;
    const data = e.clipboardData;
    if (!data || !data.files || data.files.length === 0) return;
    const text = data.getData('text/plain');
    if (text && text.trim()) return;
    e.preventDefault();
    take(Array.prototype.map.call(data.files, file => ({ file, folder: false })), 'paste');
}

function onPicked() {
    const entries = Array.prototype.map.call(input.files || [], file => ({ file, folder: false }));
    input.value = '';
    take(entries, 'pick');
}

async function take(entries, source) {
    if (!host || entries.length === 0) return;
    const attach = [];
    const others = [];
    const problems = [];

    for (const entry of entries) {
        const kind = entry.folder ? null : kindOf(entry.file);
        if (kind) attach.push({ file: entry.file, kind });
        else others.push(entry.file);
    }

    if (others.length && !post(others)) {
        others.forEach(file => problems.push({ name: file.name || '파일', kind: 'other', problem: 'unsupported' }));
    }

    let attached = 0;
    for (const item of attach) {
        const payload = await read(item.file, item.kind, source);
        if (payload.problem) {
            problems.push(payload);
            continue;
        }

        let result;
        try {
            result = await host.invokeMethodAsync('OnFileAttached', payload);
        } catch (err) {
            result = 'read';
        }

        if (!result) attached++;
        else if (result !== 'blocked') problems.push({ name: payload.name, kind: payload.kind, problem: result });
    }

    if (problems.length) {
        try {
            await host.invokeMethodAsync('OnFileProblems', problems);
        } catch (err) {
        }
    }

    if (attached) focusComposer(0);
}

function post(files) {
    const webview = window.chrome && window.chrome.webview;
    if (!webview || typeof webview.postMessageWithAdditionalObjects !== 'function') return false;
    try {
        webview.postMessageWithAdditionalObjects(IMPORT_MESSAGE, files);
        return true;
    } catch (err) {
        return false;
    }
}

async function read(file, kind, source) {
    const pasted = source === 'paste' && kind === 'image' && (!file.name || file.name === 'image.png');
    const name = file.name || '붙인 파일';
    try {
        if (kind === 'image') return await readImage(file, pasted ? null : name);
        const limit = kind === 'pdf' ? limits.pdfBytes : limits.textBytes;
        if (file.size > limit) return { name, kind, problem: 'large' };
        return { name, kind, data: await toBase64(file) };
    } catch (err) {
        return { name: pasted ? '붙여 넣은 그림' : name, kind, problem: 'read' };
    }
}

async function readImage(file, name) {
    if (file.size > limits.imageSourceBytes) return { name: name || '붙여 넣은 그림', kind: 'image', problem: 'large' };

    const picture = await decode(file);
    try {
        const width = picture.width;
        const height = picture.height;
        if (!width || !height) throw new Error('empty image');

        const scale = Math.min(1, limits.maxEdge / Math.max(width, height));
        const w = Math.max(1, Math.round(width * scale));
        const h = Math.max(1, Math.round(height * scale));
        let blob = file;
        if (scale < 1 || !DIRECT_TYPES.includes(file.type) || file.size > limits.imageBytes) {
            blob = await encode(picture.source, w, h, 'image/png');
            if (blob.size > PNG_SOFT_LIMIT) {
                const jpeg = await encode(picture.source, w, h, 'image/jpeg', 0.9);
                if (jpeg.size < blob.size) blob = jpeg;
            }
            for (const quality of [0.8, 0.7, 0.6, 0.5]) {
                if (blob.size <= limits.imageBytes) break;
                blob = await encode(picture.source, w, h, 'image/jpeg', quality);
            }
        }

        if (!name) {
            pasteCount++;
            name = '붙여 넣은 그림 ' + pasteCount + (blob.type === 'image/jpeg' ? '.jpg' : '.png');
        }

        if (blob.size > limits.imageBytes) return { name, kind: 'image', problem: 'large' };

        return {
            name,
            kind: 'image',
            data: await toBase64(blob),
            thumbnail: await thumbnail(picture.source, width, height),
        };
    } finally {
        picture.close();
    }
}

async function decode(file) {
    if (window.createImageBitmap) {
        try {
            const bitmap = await createImageBitmap(file);
            return { source: bitmap, width: bitmap.width, height: bitmap.height, close: () => bitmap.close() };
        } catch (err) {
        }
    }

    const url = URL.createObjectURL(file);
    const image = new Image();
    image.src = url;
    try {
        await image.decode();
    } catch (err) {
        URL.revokeObjectURL(url);
        throw err;
    }
    return { source: image, width: image.naturalWidth, height: image.naturalHeight, close: () => URL.revokeObjectURL(url) };
}

function encode(source, width, height, type, quality) {
    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const context = canvas.getContext('2d');
    if (type === 'image/jpeg') {
        context.fillStyle = '#ffffff';
        context.fillRect(0, 0, width, height);
    }
    context.imageSmoothingQuality = 'high';
    context.drawImage(source, 0, 0, width, height);
    return new Promise((resolve, reject) => canvas.toBlob(blob => blob ? resolve(blob) : reject(new Error('encode failed')), type, quality));
}

async function thumbnail(source, width, height) {
    const scale = Math.min(1, THUMB_EDGE / Math.max(width, height));
    const blob = await encode(source, Math.max(1, Math.round(width * scale)), Math.max(1, Math.round(height * scale)), 'image/jpeg', 0.85);
    return await toDataUrl(blob);
}

function toDataUrl(blob) {
    return new Promise((resolve, reject) => {
        const reader = new FileReader();
        reader.onload = () => resolve(String(reader.result));
        reader.onerror = () => reject(reader.error);
        reader.readAsDataURL(blob);
    });
}

async function toBase64(blob) {
    const url = await toDataUrl(blob);
    const comma = url.indexOf(',');
    return comma < 0 ? '' : url.slice(comma + 1);
}

function focusComposer(tries) {
    const box = document.getElementById('composer-input');
    if (box && !box.disabled) {
        box.focus();
        return;
    }
    if (tries < 10) requestAnimationFrame(() => focusComposer(tries + 1));
}

export function start(dotnet, options) {
    stop();
    host = dotnet;
    limits = options;
    window.addEventListener('dragenter', onDragEnter, true);
    window.addEventListener('dragover', onDragOver, true);
    window.addEventListener('dragleave', onDragLeave, true);
    window.addEventListener('drop', onDrop, true);
    document.addEventListener('paste', onPaste, true);

    input = document.createElement('input');
    input.type = 'file';
    input.multiple = true;
    input.accept = ACCEPT;
    input.hidden = true;
    input.id = 'attachment-picker';
    input.addEventListener('change', onPicked);
    document.body.appendChild(input);
}

export function stop() {
    clearTimeout(dragTimer);
    dragging = false;
    targets = [];
    window.removeEventListener('dragenter', onDragEnter, true);
    window.removeEventListener('dragover', onDragOver, true);
    window.removeEventListener('dragleave', onDragLeave, true);
    window.removeEventListener('drop', onDrop, true);
    document.removeEventListener('paste', onPaste, true);
    if (input) {
        input.removeEventListener('change', onPicked);
        input.remove();
        input = null;
    }
    host = null;
}

export function pick() {
    if (!input) return;
    input.value = '';
    input.click();
}
