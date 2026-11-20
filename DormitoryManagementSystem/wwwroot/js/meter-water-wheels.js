// Locate a dark mechanical register with red fractional wheels, independently of
// printed serial numbers, dial labels, and the register's position on the meter.
function surface(width, height) {
    const canvas = document.createElement("canvas");
    canvas.width = Math.max(1, Math.round(width));
    canvas.height = Math.max(1, Math.round(height));
    return canvas;
}

function red(pixels, i) {
    const r = pixels[i], g = pixels[i + 1], b = pixels[i + 2];
    return r - g > 18 && r - b > 14 && r > g * 1.25;
}

function median(values) {
    return values.sort((a, b) => a - b)[values.length >> 1];
}

function findRegisters(image, threshold) {
    const { data, width, height } = image;
    const dark = new Uint8Array(width * height);
    for (let i = 0; i < dark.length; i++) {
        const j = i * 4;
        dark[i] = (data[j] * .299 + data[j + 1] * .587 + data[j + 2] * .114) < threshold;
    }
    const queue = new Int32Array(dark.length);
    const results = [];
    for (let i = 0; i < dark.length; i++) {
        if (!dark[i]) continue;
        let count = 1, redCount = 0, sx = 0, sy = 0, xx = 0, yy = 0, xy = 0;
        queue[0] = i;
        dark[i] = 0;
        for (let j = 0; j < count; j++) {
            const pos = queue[j], x = pos % width, y = Math.floor(pos / width);
            sx += x; sy += y; xx += x*x; yy += y*y; xy += x*y;
            if (red(data, pos * 4)) redCount++;
            if (x > 0) visit(pos - 1);
            if (x + 1 < width) visit(pos + 1);
            if (y > 0) visit(pos - width);
            if (y + 1 < height) visit(pos + width);
        }
        function visit(pos) {
            if (dark[pos]) { dark[pos] = 0; queue[count++] = pos; }
        }
        if (count < 90 || redCount < .06 * count || redCount > .75 * count) continue;
        // Principal axis of this connected region gives the rotation of the strip.
        const angle = .5 * Math.atan2(2 * (xy - sx*sy/count), xx - sx*sx/count - yy + sy*sy/count);
        if (Math.abs(angle) > Math.PI / 4) continue;
        const c = Math.cos(angle), s = Math.sin(angle);
        const columns = new Map();
        for (let j = 0; j < count; j++) {
            const x = queue[j] % width, y = Math.floor(queue[j] / width);
            const u = Math.round(c*x + s*y), v = -s*x + c*y;
            const edges = columns.get(u);
            if (edges) { edges[0] = Math.min(edges[0], v); edges[1] = Math.max(edges[1], v); }
            else columns.set(u, [v, v]);
        }
        const left = Math.min(...columns.keys()), w = Math.max(...columns.keys()) + 1 - left;
        const top = median(Array.from(columns.values(), edges => edges[0]));
        const h = median(Array.from(columns.values(), edges => edges[1])) + 1 - top;
        const fill = count / (w*h);
        if (h < 7 || w/h < 2.8 || w/h > 12 || fill < .52 || fill > 1.15) continue;
        results.push({ left, top, w, h, c, s });
    }
    return results;
}

function splitRegister(source, probe, box) {
    const {left, top, w, h, c, s} = box;
    const scale = 80/h;
    const row = surface(w * scale, 80);
    const ctx = row.getContext("2d", {willReadFrequently:true});
    ctx.scale(scale, scale);
    ctx.translate(-left, -top);
    ctx.transform(c, -s, s, c, 0, 0);
    ctx.drawImage(source, 0, 0, probe.width, probe.height);
    const ink = ctx.getImageData(0, 8, row.width, 66);
    const {data, width, height} = ink;
    const share = new Float32Array(width);
    for (let x = 0; x < width; x++) {
        for (let y = 0; y < height; y++) if (red(data, (y*width+x)*4)) share[x] += 1/height;
    }
    const split = share.findIndex(fraction => fraction > .45);
    if (split < width * .28 || split > width * .94) return null;
    const colored = share.slice(split).filter(fraction => fraction > .4).length;
    if (colored < (width - split) * .72) return null;
    // Threshold the green channel locally: pale numerals stay light on both
    // black and red wheels, even with uneven light on the protective glass.
    const mask = ctx.createImageData(width, height);
    const radius = Math.round(row.height / 3);
    for (let left = 0; left < width; left += 4) {
        const samples = [];
        for (let x = Math.max(0, left-radius); x < Math.min(width,left+radius+4); x += 2) {
            for (let y = 0; y < height; y += 2) samples.push(data[(y*width+x)*4+1]);
        }
        samples.sort((a,b) => a-b);
        const low = samples[Math.floor(samples.length*.12)], high = samples[Math.floor(samples.length*.94)];
        const cutoff = Math.max(low + 17, low + .60 * (high - low));
        for (let x = left; x < Math.min(width,left+4); x++) {
            for (let y = 0; y < height; y++) {
                const i = (y*width+x)*4;
                mask.data.fill(data[i+1] > cutoff ? 0 : 255, i, i+3);
                mask.data[i+3] = 255;
            }
        }
    }
    ctx.putImageData(mask, 0, 0);
    function slice(x, w) {
        const out = surface(w + 20, height + 20);
        const copy = out.getContext("2d");
        copy.fillStyle = "white";
        copy.fillRect(0, 0, out.width, out.height);
        copy.drawImage(row, x, 0, w, height, 10, 10, w, height);
        return out;
    }
    return {whole:slice(0, split), fraction:slice(split, width-split), line:slice(0,width)};
}

export function locateWaterWheels(source) {
    const scale = Math.min(1, 480 / Math.max(source.width, source.height));
    const probe = surface(source.width * scale, source.height * scale);
    const ctx = probe.getContext("2d");
    ctx.drawImage(source, 0, 0, probe.width, probe.height);
    const image = ctx.getImageData(0, 0, probe.width, probe.height);
    for (const threshold of [98, 75, 120]) {
        const matches = findRegisters(image, threshold).map(box => splitRegister(source, probe, box)).filter(Boolean);
        if (matches.length > 1) return null;
        if (matches.length === 1) return matches[0];
    }
    return null;
}

export async function readWaterWheels(worker, register, mode, current) {
    await worker.setParameters({tessedit_pageseg_mode:mode, tessedit_char_whitelist:"0123456789"});
    const results = [];
    for (const part of [register.whole, register.fraction, register.line]) {
        if (!current()) return null;
        const { data } = await worker.recognize(part);
        results.push({ digits:data.text.trim(), confidence:data.confidence });
    }
    const [whole, fraction, line] = results;
    if (!/^\d{1,8}$/.test(whole.digits) || !/^\d{1,3}$/.test(fraction.digits) ||
        line.digits !== whole.digits + fraction.digits || Math.min(whole.confidence, fraction.confidence) < 60) return null;
    return { whole:whole.digits, fraction:fraction.digits };
}
