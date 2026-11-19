import * as THREE from "three";

// Coordinates are in metres. Each shape is shared by all matching beds in the room.
function duvetGeometry(phase, simplified) {
    const cloth = new THREE.PlaneGeometry(1, 1, simplified ? 18 : 38, simplified ? 24 : 48);
    const vertices = cloth.attributes.position;
    const colors = [];
    for (let i = 0; i < vertices.count; i++) {
        const u = cloth.attributes.uv.getX(i) * 2 - 1;
        const v = 1 - cloth.attributes.uv.getY(i);
        const side = Math.max(0, (Math.abs(u) - 0.79) / 0.21);
        const foot = Math.max(0, (v - 0.92) / 0.08);
        const x = Math.sign(u) * (Math.min(Math.abs(u) / 0.79, 1) * 0.451 + 0.046 * Math.sin(side * Math.PI / 2));
        const z = -0.41 + Math.min(v / 0.92, 1) * 1.35 + 0.048 * Math.sin(foot * Math.PI / 2);
        // Broad, slightly skewed folds settle into smaller ripples where the duvet hangs over the rail.
        const crossFold = v + 0.075 * u + 0.026 * Math.sin(5 * u + phase);
        const waves = 0.014 * Math.sin(22 * crossFold + phase) + 0.01 * Math.cos(11 * u + 9 * v + phase);
        const turnback = 0.038 * Math.exp(-(((v - 0.135) / 0.056) ** 2));
        const drop = 0.15 * side ** 1.7 + 0.095 * foot ** 1.8;
        const y = 0.197 + (waves + turnback + 0.022 * (1 - u * u)) * (1 - 0.65 * side) - drop;
        vertices.setXYZ(i, x + side * 0.009 * Math.sin(27 * v), y, z + 0.013 * Math.sin(4 * u + phase));
        const shade = v < 0.14 ? 1.16 : 1;
        colors.push(shade, shade, shade);
    }
    cloth.setAttribute("color", new THREE.Float32BufferAttribute(colors, 3));
    cloth.computeVertexNormals();
    return cloth;
}

function pillowGeometry(simplified) {
    const pillow = new THREE.SphereGeometry(1, simplified ? 24 : 56, simplified ? 12 : 24);
    const vertices = pillow.attributes.position;
    const toCorner = a => Math.sign(a) * Math.abs(a) ** 0.42;
    for (let i = 0; i < vertices.count; i++) {
        const sx = vertices.getX(i), sy = vertices.getY(i), sz = vertices.getZ(i);
        const radius = Math.hypot(sx, sz);
        const x = radius > 1e-6 ? toCorner(sx / radius) * radius : 0;
        const z = radius > 1e-6 ? toCorner(sz / radius) * radius : 0;
        const tuck = Math.abs(x * z) ** 3;
        const crease = tuck * Math.cos(15 * x + 8 * z);
        vertices.setXYZ(i, 0.382 * x, sy * (0.093 - 0.018 * crease) + 0.019 * tuck, 0.229 * z);
    }
    pillow.computeVertexNormals();
    return pillow;
}

export function bedTextiles(simplified) {
    return { pillow: pillowGeometry(simplified), duvets: [duvetGeometry(0.2, simplified), duvetGeometry(1.85, simplified)] };
}

export function windowCurtain(side) {
    const cloth = new THREE.PlaneGeometry(1, 1, 46, 12);
    const vertices = cloth.attributes.position;
    for (let i = 0; i < vertices.count; i++) {
        const u = cloth.attributes.uv.getX(i);
        const v = 1 - cloth.attributes.uv.getY(i);
        const angle = 9 * Math.PI * u + 0.24 * v * v;
        const spread = (side < 0 ? 0.54 : 0.39) + 0.115 * v;
        const x = side * (1.35 - spread * u);
        vertices.setXYZ(i, x, 2.44 - 1.28 * v + 0.019 * Math.cos(angle) * v,
            0.17 + 0.049 * Math.cos(angle) + 0.012 * Math.sin(3 * v + u));
    }
    cloth.computeVertexNormals();
    return cloth;
}
