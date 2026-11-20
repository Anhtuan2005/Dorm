const dialog = document.querySelector("#meter-photo");
const entry = document.querySelector("#meter-entry");
const file = dialog.querySelector("#meter-file");
const review = dialog.querySelector(".meter-review");
const area = dialog.querySelector(".meter-image-area");
const image = area.querySelector("img");
const cropBox = area.querySelector(".meter-crop");
const fullImage = dialog.querySelector("[data-full-image]");
const draft = dialog.querySelector("#meter-draft");
const apply = dialog.querySelector("[data-use-reading]");
const status = dialog.querySelector("[role=status]");
const assets = new URL("./vendor/meter-ocr/", import.meta.url);
let target;
let photoUrl;
let generation = 0;
let worker;
let timeout;
let anchor;
let selection;

function stopReading() {
    generation++;
    clearTimeout(timeout);
    if (worker) worker.terminate();
    worker = null;
}

function forgetPhoto() {
    stopReading();
    image.removeAttribute("src");
    if (photoUrl) URL.revokeObjectURL(photoUrl);
    photoUrl = null;
    anchor = null;
    selection = null;
    cropBox.hidden = true;
    fullImage.hidden = true;
    review.hidden = true;
    dialog.querySelector("#meter-file-name").textContent = "";
    draft.value = "";
    apply.disabled = true;
    status.textContent = "";
}

function numberValue(text, places = 2) {
    const normalized = text.trim().replace(",", ".");
    if (!/^\d+(?:\.\d+)?$/.test(normalized) || (normalized.split(".")[1]?.length ?? 0) > places || Number(normalized) > 99999999) return null;
    return normalized.replace(/^0+(?=\d)/, "");
}

function updateEstimate() {
    const items = [];
    for (const [prefix, label, unit] of [["power", "Điện", "kWh"], ["water", "Nước", "m³"]]) {
        const scale = prefix === "water" ? 1000 : 100;
        const values = ["Start", "End", "Rate"].map(suffix =>
            numberValue(entry.elements.namedItem(prefix + suffix).value, prefix === "water" && suffix !== "Rate" ? 3 : 2));
        if (values.includes(null)) continue;
        const [start, end, rate] = values.map((value, index) => Math.round(Number(value) * (index === 2 ? 100 : scale)));
        const cost = (end - start) * rate;
        if (end < start || cost > 999999999 * scale * 100) continue;
        items.push(label + " " + ((end - start) / scale).toLocaleString("vi-VN") + " " + unit +
            " × " + (rate / 100).toLocaleString("vi-VN") + " đ = " + Math.round(cost / (scale * 100)).toLocaleString("vi-VN") + " đ");
    }
    entry.querySelector(".meter-estimate").hidden = !items.length;
    entry.querySelector("#meter-estimate").textContent = items.join(" · ");
}
if (entry) {
    entry.addEventListener("input", updateEstimate);
    updateEstimate();
}

// Only accept one numeric line. Never join digit groups or silently round a dial.
function suggestedValue(text) {
    const lines = text.trim().split(/\r?\n/);
    if (/\b(?:serial|s\/?n)\b/i.test(text)) return null;
    const hasElectricUnit = /k\s*w\s*h\b/i.test(text);
    const hasWaterUnit = /m\s*(?:³|3\b)/i.test(text);
    if (hasWaterUnit && (hasElectricUnit || target?.name.startsWith("power"))) return null;
    if (hasElectricUnit && target?.name.startsWith("water")) return null;
    const numbers = lines.map(line => line.replace(/(?:k\s*w\s*h|m\s*[³3])\s*$/i, "").trim())
        .filter(line => /\d/.test(line));
    return numbers.length === 1 ? numberValue(numbers[0], target?.name.startsWith("power") || hasElectricUnit ? 2 : 3) : null;
}

function scanCanvas(rect) {
    const width = image.naturalWidth;
    const height = image.naturalHeight;
    const source = rect || { x: 0, y: 0, w: 1, h: 1 };
    const w = width * source.w;
    const h = height * source.h;
    const scale = Math.min(2, 1800 / Math.max(w, h));
    const canvas = document.createElement("canvas");
    canvas.width = Math.max(1, Math.round(w * scale));
    canvas.height = Math.max(1, Math.round(h * scale));
    const ctx = canvas.getContext("2d");
    ctx.fillStyle = "white";
    ctx.fillRect(0, 0, canvas.width, canvas.height);
    ctx.drawImage(image, width * source.x, height * source.y, w, h, 0, 0, canvas.width, canvas.height);
    return canvas;
}

// A change to red wheels can stand for a decimal separator that is never printed.
function hasRedFraction(canvas, box) {
    const sample = document.createElement("canvas");
    sample.width = 80;
    sample.height = 30;
    const ctx = sample.getContext("2d");
    const { x0, y0, x1, y1 } = box ?? {x0: 0, y0: 0, x1: canvas.width, y1: canvas.height};
    ctx.drawImage(canvas, x0, y0, x1 - x0, y1 - y0, 0, 0, sample.width, sample.height);
    const { data } = ctx.getImageData(0, 0, sample.width, sample.height);
    let left = 0, right = 0;
    for (let i = 0; i < data.length; i += 4) {
        const [r, g, b] = data.subarray(i, i + 3);
        if (r - g > 18 && r - b > 14 && r > g * 1.25) {
            if ((i / 4) % sample.width < sample.width / 2) left++;
            else right++;
        }
    }
    return right > 85 && right > left * 3;
}

async function recognize(rect) {
    stopReading();
    const job = generation;
    draft.value = "";
    apply.disabled = true;
    status.textContent = "Đang đọc chỉ số…";
    const failed = () => {
        if (job !== generation) return;
        stopReading();
        status.textContent = "Chưa đọc được ảnh. Bạn có thể khoanh dãy số hoặc nhập chỉ số theo ảnh.";
    };
    timeout = setTimeout(failed, 45000);
    try {
        const canvas = scanCanvas(rect);
        const { default: ocr } = await import(new URL("tesseract.esm.min.js", assets).href);
        if (job !== generation) return;
        const ready = await ocr.createWorker("eng", ocr.OEM.LSTM_ONLY, {
            workerPath: new URL("worker.min.js", assets).href,
            corePath: new URL("tesseract-core-lstm.wasm.js", assets).href,
            langPath: assets.href,
            workerBlobURL: false,
            errorHandler: failed
        });
        if (job !== generation) { await ready.terminate(); return; }
        worker = ready;
        await ready.setParameters({ tessedit_pageseg_mode: rect ? ocr.PSM.SINGLE_LINE : ocr.PSM.SPARSE_TEXT });
        if (job !== generation) return;
        const { data } = await ready.recognize(canvas, {}, { text: true, blocks: true });
        if (job !== generation) return;
        if (!target?.name.startsWith("power") && !/k\s*w\s*h\b/i.test(data.text)) {
            const { locateWaterWheels, readWaterWheels } = await import("./meter-water-wheels.js");
            if (job !== generation) return;
            const register = locateWaterWheels(canvas);
            if (register) {
                const digits = await readWaterWheels(ready, register, ocr.PSM.SINGLE_WORD, () => job === generation);
                if (job !== generation) return;
                const decimal = digits && numberValue(digits.whole + "." + digits.fraction, 3);
                if (decimal !== null) {
                    draft.value = decimal;
                    apply.disabled = false;
                    status.textContent = "Dãy số đen " + digits.whole + " · dãy số đỏ " + digits.fraction + ". Hãy đối chiếu với ảnh.";
                } else {
                    status.textContent = "Đã tìm thấy dãy số đen và đỏ nhưng chưa đọc chắc từng chữ số. Hãy khoanh sát cửa sổ chỉ số hoặc nhập theo ảnh.";
                }
                stopReading();
                return;
            }
        }
        let candidate = suggestedValue(data.text);
        let value = data.confidence >= 55 ? candidate : null;
        if (value === null && rect && !/\d[.,]\d/.test(data.text) && !/(?:k\s*w\s*h|m\s*[³3])/i.test(data.text)) {
            const { enhanceRegister } = await import("./meter-register.js");
            if (job !== generation) return;
            const strip = enhanceRegister(canvas);
            if (strip) {
                await ready.setParameters({ tessedit_pageseg_mode: ocr.PSM.SINGLE_LINE, tessedit_char_whitelist: "0123456789.," });
                if (job !== generation) return;
                const enhanced = await ready.recognize(strip);
                if (job !== generation) return;
                candidate = suggestedValue(enhanced.data.text) ?? candidate;
                if (enhanced.data.confidence >= 55) value = suggestedValue(enhanced.data.text);
            }
        }
        const digits = data.blocks?.flatMap(block => block.paragraphs.flatMap(p => p.lines.flatMap(l => l.words)))
            .find(word => /\d/.test(word.text));
        if (candidate !== null && !candidate.includes(".") && hasRedFraction(canvas, rect ? null : digits?.bbox)) {
            status.textContent = "Dãy số đổi sang màu đỏ. Hãy đối chiếu vị trí thập phân trên đồng hồ rồi nhập chỉ số.";
        } else if (value === null && candidate !== null) {
            status.textContent = "Kết quả chưa chắc chắn (" + candidate + "). Hãy đối chiếu ảnh rồi tự nhập chỉ số.";
        } else if (value === null) {
            status.textContent = "Chưa xác định được một chỉ số rõ ràng. Hãy khoanh riêng dãy số hoặc nhập theo ảnh.";
        } else {
            draft.value = value;
            apply.disabled = false;
            status.textContent = "Đã đọc được chỉ số. Hãy đối chiếu kết quả với ảnh.";
        }
        stopReading();
    } catch { failed(); }
}

apply.hidden = !entry;
for (const button of document.querySelectorAll("[data-read-meter], [data-preview-meter]")) {
    button.hidden = false;
    button.addEventListener("click", () => {
        if (entry) {
            target = entry.elements.namedItem(button.dataset.readMeter);
            const room = entry.elements.namedItem("roomCode").value.trim();
            dialog.querySelector("#meter-photo-context").textContent = [room, button.dataset.caption].filter(Boolean).join(" · ");
            dialog.querySelector("#meter-unit").textContent = target.name.startsWith("power") ? "(kWh)" : "(m³)";
        } else {
            dialog.querySelector("#meter-photo-context").textContent = "Đọc thử · Chưa ghi vào sổ";
        }
        dialog.showModal();
    });
}

dialog.querySelector("[data-dialog-close]").addEventListener("click", () => dialog.close());
dialog.addEventListener("close", () => {
    forgetPhoto();
    file.value = "";
    target = null;
});

file.addEventListener("change", async () => {
    forgetPhoto();
    const selected = file.files[0];
    if (!selected) return;
    dialog.querySelector("#meter-file-name").textContent = selected.name;
    if (!["image/jpeg", "image/png", "image/webp"].includes(selected.type) || selected.size > 12 * 1024 * 1024) {
        status.textContent = "Hãy chọn ảnh JPG, PNG hoặc WebP tối đa 12 MB.";
        return;
    }
    const job = generation;
    photoUrl = URL.createObjectURL(selected);
    image.src = photoUrl;
    try {
        await image.decode();
        if (job !== generation) return;
        review.hidden = false;
        recognize(null);
    } catch {
        if (job === generation) status.textContent = "Không mở được ảnh này. Hãy chọn ảnh khác.";
    }
});

function point(event) {
    const bounds = area.getBoundingClientRect();
    const clamp = value => Math.min(1, Math.max(0, value));
    return { x: clamp((event.clientX - bounds.left) / bounds.width), y: clamp((event.clientY - bounds.top) / bounds.height) };
}

function rectangle(end) {
    return { x: Math.min(anchor.x, end.x), y: Math.min(anchor.y, end.y),
        w: Math.abs(anchor.x - end.x), h: Math.abs(anchor.y - end.y) };
}

function showCrop(rect) {
    cropBox.hidden = !rect;
    if (!rect) return;
    Object.assign(cropBox.style, { left: rect.x * 100 + "%", top: rect.y * 100 + "%",
        width: rect.w * 100 + "%", height: rect.h * 100 + "%" });
}

area.addEventListener("pointerdown", event => {
    if (!event.isPrimary || event.button !== 0) return;
    anchor = point(event);
    area.setPointerCapture(event.pointerId);
});
area.addEventListener("pointermove", event => {
    if (anchor && event.isPrimary) showCrop(rectangle(point(event)));
});
area.addEventListener("pointerup", event => {
    if (!anchor || !event.isPrimary) return;
    const rect = rectangle(point(event));
    anchor = null;
    if (rect.w * area.clientWidth < 8 || rect.h * area.clientHeight < 6) {
        showCrop(selection);
        return;
    }
    selection = rect;
    showCrop(selection);
    fullImage.hidden = false;
    recognize(rect);
});
area.addEventListener("pointercancel", () => { anchor = null; showCrop(selection); });
fullImage.addEventListener("click", () => {
    selection = null;
    showCrop(selection);
    fullImage.hidden = true;
    recognize(null);
});

draft.addEventListener("input", () => {
    stopReading();
    const places = target?.name.startsWith("power") ? 2 : 3;
    apply.disabled = numberValue(draft.value, places) === null;
    status.textContent = apply.disabled ? `Nhập số từ 0 đến 99.999.999, tối đa ${places} chữ số thập phân.` : "";
});
apply.addEventListener("click", () => {
    const value = numberValue(draft.value, target?.name.startsWith("water") ? 3 : 2);
    if (!target || value === null) return;
    target.value = value;
    target.dispatchEvent(new Event("input", { bubbles: true }));
    target.dispatchEvent(new Event("change", { bubbles: true }));
    dialog.close();
    target.focus();
});
