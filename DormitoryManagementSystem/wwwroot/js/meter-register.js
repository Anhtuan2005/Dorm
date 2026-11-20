// Mechanical counters often have pale digits on a tilted, dark strip behind glass.
// Only enhance a crop when both long edges of that strip are visible.
function canvas(width, height) {
    const result = document.createElement("canvas");
    result.width = Math.max(1, Math.round(width));
    result.height = Math.max(1, Math.round(height));
    return result;
}

function rotateInto(source, target, angle) {
    const ctx = target.getContext("2d", { willReadFrequently: true });
    ctx.fillStyle = "#777";
    ctx.fillRect(0, 0, target.width, target.height);
    ctx.save();
    ctx.translate(target.width / 2, target.height / 2);
    ctx.rotate(angle * Math.PI / 180);
    ctx.drawImage(source, -target.width / 2, -target.height / 2, target.width, target.height);
    ctx.restore();
    return ctx.getImageData(0, 0, target.width, target.height);
}

function gray(data, index) {
    return (data[index] * 3 + data[index + 1] * 6 + data[index + 2]) / 10;
}

function locateStrip(source) {
    const probe = canvas(180, 180 * source.height / source.width);
    let best = null;
    const { width, height } = probe;
    const span = Math.max(2, Math.round(height * .025));
    for (let angle = -16; angle <= 16; angle += 2) {
        const { data } = rotateInto(source, probe, angle);
        const rows = [];
        for (let y = 0; y < height; y++) {
            const samples = [];
            for (let x = Math.floor(width * .1); x < width * .9; x += 4) {
                samples.push(gray(data, (y * width + x) * 4));
            }
            rows.push(samples.sort((a, b) => a - b)[samples.length >> 1]);
        }
        let top = 0, bottom = 0, drop = 0, rise = 0;
        for (let y = span; y < height - span; y++) {
            let difference = 0;
            for (let i = 0; i < span; i++) difference += rows[y + i] - rows[y - i - 1];
            difference /= span;
            if (y < height * .42 && -difference > drop) { top = y; drop = -difference; }
            if (y > height * .58 && difference > rise) { bottom = y; rise = difference; }
        }
        const score = Math.min(drop, rise);
        if (score > 18 && (!best || score > best.score)) {
            best = { score, angle, top: top / height, bottom: bottom / height };
        }
    }
    return best;
}

// Close the large gaps between individual mechanical wheels before recognizing the row.
function packWheels(mask, source) {
    const { width, height, data } = mask;
    const spans = [];
    let start = -1;
    for (let x = 0; x <= width; x++) {
        let ink = 0;
        if (x < width) {
            for (let y = 0; y < height; y++) if (data[(y * width + x) * 4] === 0) ink++;
        }
        if (ink > height * .07) {
            if (start < 0) start = x;
        } else if (start >= 0) {
            // Rotation can leave narrow wedges at the very edges of the crop.
            if (x - start < height * .09 && (start < width * .025 || x > width * .975)) { start = -1; continue; }
            spans.push({ left: start, width: x - start });
            start = -1;
        }
    }
    if (spans.length < 2 || spans.length > 10) return null;
    const pieces = [];
    for (const span of spans) {
        let top = height, bottom = -1;
        for (let y = 0; y < height; y++) {
            let ink = 0;
            for (let x = span.left; x < span.left + span.width; x++) if (!data[(y * width + x) * 4]) ink++;
            if (ink >= 3) { top = Math.min(top, y); bottom = y; }
        }
        const h = bottom - top + 1;
        // A separate short mark may be punctuation. Leave its interpretation to the operator.
        if (h < height * .55 || span.width > height || span.width < 2) return null;
        pieces.push({ ...span, top, h, scaledWidth: Math.round(70 * span.width / h) });
    }
    const packed = canvas(20 + pieces.reduce((sum, p) => sum + p.scaledWidth + 4, 0), 90);
    const ctx = packed.getContext("2d");
    ctx.fillStyle = "white";
    ctx.fillRect(0, 0, packed.width, packed.height);
    let x = 10;
    for (const p of pieces) {
        ctx.drawImage(source, p.left, p.top, p.width, p.h, x, 10, p.scaledWidth, 70);
        x += p.scaledWidth + 4;
    }
    return packed;
}

export function enhanceRegister(source) {
    const aspect = source.width / source.height;
    if (aspect < 1.6 || aspect > 12) return null;
    const strip = locateStrip(source);
    if (!strip) return null;
    const aligned = canvas(Math.min(source.width, 640), Math.min(source.width, 640) / aspect);
    const { data, width, height } = rotateInto(source, aligned, strip.angle);
    const inset = (strip.bottom - strip.top) * .055;
    const top = Math.round((strip.top + inset) * height);
    const bottom = Math.round((strip.bottom - inset) * height);
    const bandHeight = bottom - top;
    const result = canvas(width, bandHeight);
    const ctx = result.getContext("2d");
    ctx.fillStyle = "white";
    ctx.fillRect(0, 0, result.width, result.height);
    const mask = ctx.createImageData(width, bandHeight);
    // Local contrast keeps the first wheels legible even in the shadow of the casing.
    for (let left = 0; left < width; left += 12) {
        const samples = [];
        const radius = Math.round(bandHeight * .45);
        for (let x = Math.max(0, left - radius); x < Math.min(width, left + 12 + radius); x += 3) {
            for (let y = top; y < bottom; y += 3) samples.push(gray(data, (y * width + x) * 4));
        }
        samples.sort((a, b) => a - b);
        const low = samples[Math.floor(samples.length * .15)];
        const high = samples[Math.floor(samples.length * .9)];
        const cutoff = low + .59 * (high - low);
        for (let x = left; x < Math.min(width, left + 12); x++) {
            for (let y = top; y < bottom; y++) {
                const pos = ((y - top) * width + x) * 4;
                const value = gray(data, (y * width + x) * 4) > cutoff ? 0 : 255;
                mask.data[pos] = mask.data[pos + 1] = mask.data[pos + 2] = value;
                mask.data[pos + 3] = 255;
            }
        }
    }
    ctx.putImageData(mask, 0, 0);
    return packWheels(mask, result);
}
