import * as THREE from "three";
import { RoundedBoxGeometry } from "three/addons/geometries/RoundedBoxGeometry.js";

// Match the catalogue's Vietnamese labels, with or without accents.
const amenityPatterns = {
    wifi: /\b(wi fi|wifi|internet|mang khong day)\b/,
    air: /\b(dieu hoa|may lanh|ac)\b/,
    desk: /\b(ban hoc|ban lam viec)\b/,
    locker: /\b(tu ca nhan|tu do|tu quan ao|locker)\b/,
    standingFan: /^(quat|may quat)$|\b(quat dung|quat cay)\b/,
    ceilingFan: /^(quat|may quat)$|\b(quat tran)\b/,
    fridge: /\b(tu lanh)\b/,
    balcony: /\b(ban cong|lo gia)\b/,
    heater: /\b(nong lanh|nuoc nong|may nuoc nong)\b/
};

function interpretAmenities(amenities) {
    const names = amenities.map(name => name.toLowerCase().normalize("NFD").replace(/\p{Diacritic}/gu, "")
        .replaceAll("đ", "d").replace(/[^a-z0-9]+/g, " ").trim()).filter(name => !/^(khong|chua)\b/.test(name));
    return {
        bath: true,
        ...Object.fromEntries(Object.entries(amenityPatterns).map(([key, pattern]) =>
            [key, names.some(name => pattern.test(name))]))
    };
}

function surface(draw, repeatX = 1, repeatY = 1) {
    const canvas = document.createElement("canvas");
    canvas.width = canvas.height = 128;
    draw(canvas.getContext("2d"), 128);
    const texture = new THREE.CanvasTexture(canvas);
    texture.colorSpace = THREE.SRGBColorSpace;
    texture.wrapS = texture.wrapT = THREE.RepeatWrapping;
    texture.repeat.set(repeatX, repeatY);
    texture.anisotropy = 4;
    return texture;
}

function createMaterials(width, depth) {
    const tile = surface((ctx, size) => {
        ctx.fillStyle = "#b3afa6";
        ctx.fillRect(0, 0, size, size);
        for (let col = 0; col < 2; col++) {
            for (let row = 0; row < 2; row++) {
                const x = col * 64;
                const y = row * 64;
                const light = ctx.createLinearGradient(x, y, x + 64, y + 64);
                light.addColorStop(0, (col + row) % 2 ? "#f7f4ec" : "#eee9df");
                light.addColorStop(1, "#dedbd2");
                ctx.fillStyle = light;
                ctx.fillRect(x + 1, y + 1, 62, 62);
            }
        }
        for (let i = 0; i < 110; i++) {
            ctx.fillStyle = i % 2 ? "#fff9e733" : "#62625413";
            ctx.fillRect(i * 31 % 128, i * 67 % 128, 2 + i % 3, 1);
        }
    }, Math.round(width), Math.round(depth));
    const wood = surface((ctx, size) => {
        ctx.fillStyle = "#c8a075";
        ctx.fillRect(0, 0, size, size);
        for (let i = 0; i < 64; i++) {
            ctx.strokeStyle = i % 3 ? "#6b442b22" : "#fff3ce40";
            ctx.beginPath();
            ctx.moveTo(0, i * 2);
            ctx.bezierCurveTo(38, i * 2 - 3, 89, i * 2 + 6, size, i * 2);
            ctx.stroke();
        }
    });
    const weave = surface((ctx, size) => {
        ctx.fillStyle = "#eeeae3";
        ctx.fillRect(0, 0, size, size);
        ctx.fillStyle = "#4c514d12";
        for (let i = 0; i < size; i += 4) {
            ctx.fillRect(i, 0, 1, size);
            ctx.fillRect(0, i, size, 1);
        }
    }, 5, 7);
    const windowLight = surface((ctx, size) => {
        const shade = ctx.createLinearGradient(0, 0, 0, size);
        shade.addColorStop(0, "#abcbd9");
        shade.addColorStop(0.56, "#e2eae9");
        shade.addColorStop(1, "#aebfba");
        ctx.fillStyle = shade;
        ctx.fillRect(0, 0, size, size);
        ctx.fillStyle = "#ebf5f36b";
        ctx.beginPath();
        ctx.moveTo(0, 14);
        ctx.lineTo(size, 68);
        ctx.lineTo(size, 83);
        ctx.lineTo(0, 32);
        ctx.fill();
    });
    const bathTile = tile.clone();
    bathTile.repeat.set(2.3, 2.3);
    const mat = (color, options) => new THREE.MeshStandardMaterial({ color, roughness: 0.86, ...options });
    return {
        tile: mat("#ffffff", { map: tile, roughness: 0.58 }),
        wall: mat("#f0ebe1"), plasterCut: mat("#bfcac0"), skirting: mat("#918a7e"), white: mat("#f4f1e7"),
        timber: mat("#fffaf1", { map: wood }), frame: mat("#4a5557", { metalness: 0.3, roughness: 0.58 }),
        sheet: mat("#f5f0e4", { map: weave }), pillow: mat("#fff9eb", { map: weave }),
        blanket: mat("#ffffff", { map: weave }), dark: mat("#334443"), chair: mat("#496c67"), linenEdge: mat("#dfd8c8"),
        glass: mat("#deded4", { map: windowLight, roughness: 0.34, emissive: "#aacbd4", emissiveIntensity: 0.36 }),
        porcelain: mat("#f6f8f4", { roughness: 0.28 }), bath: mat("#c3d6d3", { map: bathTile, roughness: 0.48 }),
        chrome: mat("#adbab7", { metalness: 0.55, roughness: 0.32 }), entryMat: mat("#959188", { map: weave }),
        indicator: mat("#89d5a1", { emissive: "#83d1ab", emissiveIntensity: 0.55 }),
        fanShell: mat("#dbded1", { roughness: 0.5 }),
        fanBlade: mat("#739f94", { roughness: 0.56 }),
        fridgeShell: mat("#a4b0ad", { roughness: 0.56, metalness: 0.2 }),
        fridgeDoor: mat("#e7ede9", { roughness: 0.32, metalness: 0.16 })
    };
}

function createBuilder() {
    const cube = new THREE.BoxGeometry();
    const rounded = new Map();
    function place(parent, geometry, material, x, y, z) {
        const mesh = new THREE.Mesh(geometry, material);
        mesh.position.set(x, y, z);
        mesh.castShadow = mesh.receiveShadow = true;
        parent.add(mesh);
        return mesh;
    }
    function box(parent, material, w, h, d, x, y, z) {
        const mesh = place(parent, cube, material, x, y, z);
        mesh.scale.set(w, h, d);
        return mesh;
    }
    function soft(parent, material, w, h, d, x, y, z, radius) {
        const key = [w, h, d, radius].join(",");
        if (!rounded.has(key)) rounded.set(key, new RoundedBoxGeometry(w, h, d, 3, radius));
        return place(parent, rounded.get(key), material, x, y, z);
    }
    return { box, soft, place };
}

function addStandingFans(scene, locations, m, { box, soft, place }) {
    const base = new THREE.CylinderGeometry(0.235, 0.268, 0.055, 32);
    const post = new THREE.CylinderGeometry(0.029, 0.036, 1.24, 12);
    const motor = new THREE.CylinderGeometry(0.074, 0.091, 0.126, 20);
    const rim = new THREE.TorusGeometry(0.3, 0.014, 8, 40);
    const hub = new THREE.SphereGeometry(0.062, 16, 10);
    const blade = new THREE.Shape();
    blade.moveTo(-0.022, 0.034);
    blade.quadraticCurveTo(-0.122, 0.13, -0.059, 0.253);
    blade.quadraticCurveTo(0.015, 0.278, 0.077, 0.225);
    blade.quadraticCurveTo(0.089, 0.118, 0.046, 0.035);
    blade.closePath();
    const bladeGeometry = new THREE.ExtrudeGeometry(blade, {
        depth: 0.012, bevelEnabled: true, bevelThickness: 0.004, bevelSize: 0.004, bevelSegments: 1, curveSegments: 12
    });
    const guardWires = [];
    for (let i = 0; i < 12; i++) {
        const angle = i * Math.PI / 6;
        guardWires.push(Math.cos(angle) * 0.065, Math.sin(angle) * 0.065, 0.102,
            Math.cos(angle) * 0.295, Math.sin(angle) * 0.295, 0.073);
    }
    const wireGeometry = new THREE.BufferGeometry();
    wireGeometry.setAttribute("position", new THREE.Float32BufferAttribute(guardWires, 3));
    const wireMaterial = new THREE.LineBasicMaterial({ color: "#55696a" });

    for (const [x, z, heading] of locations) {
        const fan = new THREE.Group();
        fan.position.set(x, 0, z);
        fan.rotation.y = heading;
        scene.add(fan);
        place(fan, base, m.fanShell, 0, 0.028, 0);
        place(fan, post, m.frame, 0, 0.65, 0);
        soft(fan, m.fanShell, 0.097, 0.215, 0.079, 0, 0.9, 0.013, 0.018);
        box(fan, m.dark, 0.048, 0.071, 0.014, 0, 0.926, 0.06);

        const head = new THREE.Group();
        head.position.y = 1.32;
        head.rotation.x = -0.09;
        fan.add(head);
        place(head, motor, m.fanShell, 0, 0, -0.059).rotation.x = Math.PI / 2;
        for (let i = 0; i < 3; i++)
            place(head, bladeGeometry, m.fanBlade, 0, 0, 0.025).rotation.z = i * 2 * Math.PI / 3;
        for (const z of [-0.022, 0.074]) place(head, rim, m.frame, 0, 0, z);
        for (const radius of [0.132, 0.218])
            place(head, new THREE.TorusGeometry(radius, 0.005, 5, 32), m.frame, 0, 0, 0.09);
        head.add(new THREE.LineSegments(wireGeometry, wireMaterial));
        place(head, hub, m.fanShell, 0, 0, 0.105).scale.z = 0.48;
    }
}

function addCeiling(scene, width, depth, height) {
    const panel = new THREE.Mesh(new THREE.PlaneGeometry(width, depth), new THREE.MeshBasicMaterial({
        color: "#b9d8d1", opacity: 0.057, transparent: true, depthWrite: false,
        side: THREE.DoubleSide, forceSinglePass: true
    }));
    panel.rotation.x = -Math.PI / 2;
    panel.position.y = height;
    scene.add(panel);

    const edge = new THREE.LineSegments(new THREE.EdgesGeometry(panel.geometry), new THREE.LineBasicMaterial({
        color: "#72968c", opacity: 0.28, transparent: true, depthWrite: false
    }));
    panel.add(edge);
}

function addGroundShadow(scene, width, depth) {
    const shadow = surface((ctx, size) => {
        const gradient = ctx.createRadialGradient(size / 2, size / 2, size * 0.12, size / 2, size / 2, size / 2);
        gradient.addColorStop(0, "#34423973");
        gradient.addColorStop(0.55, "#34423940");
        gradient.addColorStop(1, "#34423900");
        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, size, size);
    });
    const plane = new THREE.Mesh(new THREE.PlaneGeometry(width * 1.85, depth * 1.64),
        new THREE.MeshBasicMaterial({ map: shadow, transparent: true, depthWrite: false }));
    plane.rotation.x = -Math.PI / 2;
    plane.position.set(0.15, -0.29, 0.15);
    scene.add(plane);
}

function addCeilingFans(scene, locations, ceilingHeight, m, { box, soft, place }) {
    const ceilingPlate = new THREE.CylinderGeometry(0.176, 0.176, 0.025, 28);
    const mount = new THREE.CylinderGeometry(0.106, 0.084, 0.056, 20);
    const rod = new THREE.CylinderGeometry(0.021, 0.021, 0.17, 12);
    const motor = new THREE.CylinderGeometry(0.112, 0.079, 0.114, 20);
    for (const z of locations) {
        place(scene, ceilingPlate, m.wall, 0, ceilingHeight - 0.0125, z).castShadow = false;
        place(scene, mount, m.fanShell, 0, ceilingHeight - 0.052, z);
        place(scene, rod, m.frame, 0, ceilingHeight - 0.156, z);
        place(scene, motor, m.fanShell, 0, ceilingHeight - 0.288, z);
        for (let i = 0; i < 3; i++) {
            const arm = new THREE.Group();
            arm.position.set(0, ceilingHeight - 0.28, z);
            arm.rotation.y = i * 2 * Math.PI / 3 + 0.24;
            scene.add(arm);
            box(arm, m.frame, 0.063, 0.029, 0.16, 0, 0, 0.122);
            soft(arm, m.timber, 0.154, 0.024, 0.5, 0, 0, 0.375, 0.01);
        }
    }
}

function addMiniFridge(scene, x, z, m, { box, soft }) {
    const fridge = new THREE.Group();
    fridge.position.set(x, 0, z);
    fridge.rotation.y = 0.36;
    scene.add(fridge);
    soft(fridge, m.fridgeShell, 0.65, 0.96, 0.598, 0, 0.515, -0.045, 0.042);
    box(fridge, m.dark, 0.62, 0.856, 0.024, 0, 0.537, 0.263);
    soft(fridge, m.fridgeDoor, 0.607, 0.568, 0.07, 0, 0.39, 0.299, 0.027);
    soft(fridge, m.fridgeDoor, 0.607, 0.259, 0.07, 0, 0.822, 0.299, 0.027);
    for (const y of [0.544, 0.804]) {
        soft(fridge, m.frame, 0.033, 0.144, 0.036, -0.215, y, 0.346, 0.012);
    }
    box(fridge, m.dark, 0.582, 0.05, 0.016, 0, 0.074, 0.261);
    for (const x of [-0.237, 0.237])
        for (const z of [-0.253, 0.185])
            box(fridge, m.dark, 0.07, 0.037, 0.07, x, 0.019, z);
}

function addFixtures(scene, features, sizes, m, builder) {
    const { box, soft, place } = builder;
    const { width, back, front, rowCount, ceilingHeight } = sizes;
    const wallDepth = back + 0.11;
    const windowX = 0;
    for (const y of [1.14, 2.36]) box(scene, m.white, 2.48, 0.059, 0.125, windowX, y, wallDepth);
    for (const x of [-1.22, 1.22]) box(scene, m.white, 0.059, 1.26, 0.125, x, 1.75, wallDepth);
    const windowGlass = box(scene, m.glass, 2.4, 1.18, 0.018, windowX, 1.75, wallDepth);
    windowGlass.castShadow = false;
    for (const x of [windowX - 0.77, windowX, windowX + 0.77])
        box(scene, m.white, 0.043, 1.2, 0.085, x, 1.75, wallDepth + 0.034);
    box(scene, m.white, 2.5, 0.07, 0.27, windowX, 1.10, wallDepth + 0.08);
    box(scene, m.chrome, 0.045, 0.14, 0.045, 0.074, 1.68, wallDepth + 0.076);
    if (features.air) {
        soft(scene, m.white, 1.1, 0.27, 0.27, windowX, 2.61, wallDepth + 0.12, 0.055);
        box(scene, m.dark, 0.85, 0.026, 0.045, windowX, 2.525, wallDepth + 0.267);
        box(scene, m.indicator, 0.045, 0.024, 0.025, windowX + 0.4, 2.62, wallDepth + 0.262);
    }
    if (features.desk) {
        const z = back + 0.53;
        soft(scene, m.timber, 2.55, 0.058, 0.76, 0, 0.77, z, 0.023);
        for (const dx of [-1.17, 1.17])
            for (const dz of [-0.28, 0.28])
                box(scene, m.frame, 0.052, 0.73, 0.052, dx, 0.37, z + dz);
        box(scene, m.timber, 0.38, 0.108, 0.6, 0, 0.684, z - 0.033);
        for (const x of [-0.63, 0.63]) {
            const chairZ = z + 0.64;
            soft(scene, m.chair, 0.43, 0.065, 0.44, x, 0.44, chairZ, 0.027);
            soft(scene, m.chair, 0.44, 0.32, 0.06, x, 0.68, chairZ + 0.205, 0.026);
            for (const dx of [-0.17, 0.17])
                for (const dz of [-0.16, 0.16])
                    box(scene, m.frame, 0.035, 0.43, 0.035, x + dx, 0.215, chairZ + dz);
        }
        soft(scene, m.dark, 0.31, 0.026, 0.22, -0.65, 0.815, z - 0.12, 0.007).rotation.y = 0.2;
        soft(scene, m.white, 0.22, 0.02, 0.29, 0.63, 0.81, z + 0.07, 0.006).rotation.y = -0.13;
    }
    if (features.locker) {
        const positions = [-width / 2 + 0.65, width / 2 - 0.65];
        for (const x of positions) {
            box(scene, m.timber, 0.86, 1.93, 0.58, x, 0.965, back + 0.41);
            for (const dx of [-0.21, 0.21]) {
                box(scene, m.white, 0.396, 1.83, 0.035, x + dx, 0.97, back + 0.713);
                soft(scene, m.frame, 0.033, 0.14, 0.041, x + dx * 0.27, 1.08, back + 0.749, 0.012);
            }
        }
    }
    {
        const x = width / 2 - 1.02;
        const z = front - 1.06;
        const split = z - 0.95;
        const inside = x - 0.96;

        // The left-hand wall is interrupted by a real doorway. The door opens inside the WC.
        const doorBack = z - 0.065;
        const doorFront = z + 0.79;
        box(scene, m.bath, 1.92, 0.03, 1.94, x, 0.019, z);
        box(scene, m.wall, 1.96, 1.21, 0.1, x, 0.605, split);
        box(scene, m.bath, 1.94, 0.62, 0.034, x, 0.31, split + 0.065);
        for (const [from, to] of [[split, doorBack], [doorFront, z + 0.97]]) {
            box(scene, m.wall, 0.106, 0.92, to - from, inside, 0.46, (from + to) / 2);
            box(scene, m.plasterCut, 0.116, 0.035, to - from, inside, 0.926, (from + to) / 2);
        }
        box(scene, m.wall, 0.1, 0.7, 1.94, x + 0.94, 0.35, z);
        box(scene, m.plasterCut, 0.11, 0.035, 1.94, x + 0.94, 0.71, z);
        box(scene, m.wall, 1.96, 0.48, 0.1, x, 0.24, z + 0.97);
        for (const frameZ of [doorBack, doorFront])
            box(scene, m.timber, 0.121, 1.43, 0.043, inside, 0.715, frameZ);
        box(scene, m.white, 0.15, 0.023, doorFront - doorBack, inside, 0.018, (doorBack + doorFront) / 2);
        const hinge = new THREE.Group();
        hinge.position.set(inside + 0.015, 0, doorFront - 0.023);
        hinge.rotation.y = -1.26;
        scene.add(hinge);
        box(hinge, m.timber, 0.048, 1.35, 0.79, 0, 0.69, -0.395);
        box(hinge, m.frame, 0.09, 0.036, 0.105, 0, 0.73, -0.7);
        const bowl = place(scene, new THREE.SphereGeometry(1, 18, 12), m.porcelain, x + 0.46, 0.31, split + 0.62);
        bowl.scale.set(0.215, 0.22, 0.315);
        soft(scene, m.porcelain, 0.39, 0.42, 0.17, x + 0.46, 0.49, split + 0.25, 0.05);
        const seat = place(scene, new THREE.TorusGeometry(0.151, 0.026, 12, 32), m.porcelain, x + 0.46, 0.518, split + 0.62);
        seat.rotation.x = -Math.PI / 2;
        seat.scale.y = 1.32;
        const bowlWater = place(scene, new THREE.CircleGeometry(0.116, 28), m.glass, x + 0.46, 0.50, split + 0.62);
        bowlWater.rotation.x = -Math.PI / 2;
        bowlWater.scale.y = 1.28;
        box(scene, m.chrome, 0.055, 0.012, 0.04, x + 0.46, 0.71, split + 0.273);
        box(scene, m.white, 0.42, 0.67, 0.35, x - 0.44, 0.36, split + 0.28);
        soft(scene, m.porcelain, 0.47, 0.12, 0.4, x - 0.44, 0.755, split + 0.28, 0.04);
        box(scene, m.frame, 0.03, 0.098, 0.042, x - 0.44, 0.84, split + 0.14);
        box(scene, m.glass, 0.36, 0.31, 0.03, x - 0.44, 1.015, split + 0.07);
        box(scene, m.chrome, 0.04, 0.032, 0.085, x - 0.44, 0.88, split + 0.18);
        box(scene, m.chrome, 0.205, 0.022, 0.025, x - 0.62, 0.93, split + 0.091);
        box(scene, m.linenEdge, 0.16, 0.22, 0.041, x - 0.62, 0.82, split + 0.115);
        box(scene, m.chrome, 0.105, 0.014, 0.105, x + 0.24, 0.04, z + 0.42);
    }
    if (features.wifi) {
        const x = -1.73;
        box(scene, m.white, 0.35, 0.08, 0.13, x, 2.24, wallDepth + 0.11);
        for (const dx of [-0.137, 0.137]) box(scene, m.dark, 0.017, 0.18, 0.016, x + dx, 2.35, wallDepth + 0.14);
        for (const dx of [-0.05, 0, 0.05]) box(scene, m.indicator, 0.016, 0.015, 0.01, x + dx, 2.24, wallDepth + 0.176);
    }
    if (features.standingFan) {
        const locations = [[-width / 2 + 0.59, front - 1.42, 0.78]];
        if (rowCount > 1) locations.push([width / 2 - 1.08, back + 1.48, -0.59]);
        addStandingFans(scene, locations, m, builder);
    }
    if (features.ceilingFan) {
        const locations = Array.from({ length: Math.ceil(rowCount / 2) }, (_, i) =>
            back + 2.1 + (i * 2 + Math.min(1, rowCount - i * 2)) * 2.46);
        addCeilingFans(scene, locations, ceilingHeight, m, builder);
    }
    if (features.fridge) addMiniFridge(scene, -width / 2 + 1.55, back + 0.5, m, builder);
    if (features.heater) {
        const x = width / 2 - 0.67;
        const z = front - 1.7;
        const y = 1.04;
        soft(scene, m.white, 0.37, 0.36, 0.22, x, y, z, 0.042);
        box(scene, m.frame, 0.026, 0.24, 0.025, x - 0.11, y - 0.28, z);
    }
    if (features.balcony) {
        const x = -width / 2 + 1.11;
        box(scene, m.tile, 1.86, 0.13, 0.76, x, -0.08, front + 0.39);
        for (const dx of [-0.88, -0.45, 0, 0.45, 0.88])
            box(scene, m.frame, 0.04, 0.75, 0.04, x + dx, 0.37, front + 0.74);
        box(scene, m.frame, 1.84, 0.045, 0.045, x, 0.74, front + 0.74);
    }
}

export const bedColors = { available: "#83a999", unavailable: "#a4a29d", selected: "#b58a58" };

export function createRoomModel(scene, count, available, amenities) {
    const features = interpretAmenities(amenities);
    const rowCount = Math.ceil(count / 4);
    const rearSpace = 2.1;
    const entrySpace = 2.29;
    const width = 7.2;
    const ceilingHeight = 2.78;
    const depth = rearSpace + rowCount * 2.46 + entrySpace;
    const back = -depth / 2;
    const front = depth / 2;
    const m = createMaterials(width, depth);
    const builder = createBuilder();
    const { box, soft } = builder;
    const beds = new Map();
    const pickable = [];

    box(scene, m.plasterCut, width + 0.14, 0.22, depth + 0.14, 0, -0.16, 0);
    const floor = box(scene, m.tile, width, 0.07, depth, 0, -0.036, 0);
    floor.castShadow = false;
    const windowHalf = 1.22;
    const wallWing = width / 2 - windowHalf;
    for (const side of [-1, 1])
        box(scene, m.wall, wallWing, ceilingHeight, 0.12, side * (windowHalf + wallWing / 2), ceilingHeight / 2, back);
    box(scene, m.wall, windowHalf * 2, 1.12, 0.12, 0, 0.56, back);
    box(scene, m.wall, windowHalf * 2, ceilingHeight - 2.36, 0.12, 0, (ceilingHeight + 2.36) / 2, back);
    box(scene, m.skirting, width, 0.105, 0.04, 0, 0.054, back + 0.078);
    for (const side of [-1, 1]) {
        box(scene, m.wall, 0.115, 0.31, depth, side * width / 2, 0.155, 0);
        box(scene, m.plasterCut, 0.15, 0.049, depth, side * width / 2, 0.31, 0);
        box(scene, m.skirting, 0.032, 0.1, depth, side * (width / 2 - 0.07), 0.052, 0);
    }
    box(scene, m.entryMat, 1.03, 0.019, 0.56, 0, 0.012, front - 0.31);
    for (const x of [-0.64, 0.64]) box(scene, m.plasterCut, 0.105, 0.73, 0.112, x, 0.365, front);
    addCeiling(scene, width, depth, ceilingHeight);
    addFixtures(scene, features, { width, back, front, rowCount, ceilingHeight }, m, builder);

    // The same template maps sequential bed numbers onto lower and upper bunks.
    for (let bunk = 0; bunk < Math.ceil(count / 2); bunk++) {
        const side = bunk % 2 === 0 ? -1 : 1;
        const x = side * (width / 2 - 0.83);
        const z = back + rearSpace + 1.11 + Math.floor(bunk / 2) * 2.46;
        const upper = bunk * 2 + 2 <= count;
        const post = upper ? 1.96 : 0.71;
        for (const dx of [-0.52, 0.52]) {
            for (const dz of [-1.02, 1.02]) {
                soft(scene, m.frame, 0.06, post, 0.06, x + dx, post / 2, z + dz, 0.014);
                box(scene, m.dark, 0.082, 0.036, 0.082, x + dx, 0.024, z + dz);
            }
        }
        if (upper) {
            const ladderX = x - side * 0.59;
            for (const dz of [0.63, 0.94]) box(scene, m.frame, 0.055, 1.75, 0.054, ladderX, 0.87, z + dz);
            for (const rung of [0.25, 0.55, 0.85, 1.15, 1.45])
                soft(scene, m.timber, 0.059, 0.053, 0.31, ladderX, rung, z + 0.785, 0.011);
            for (const y of [1.8, 1.93]) {
                box(scene, m.frame, 0.04, 0.041, 2, x + side * 0.51, y, z);
                box(scene, m.frame, 0.04, 0.041, 1.49, x - side * 0.51, y, z - 0.245);
            }
        }
        for (let level = 0; level < (upper ? 2 : 1); level++) {
            const number = bunk * 2 + level + 1;
            const y = level ? 1.55 : 0.42;
            const group = new THREE.Group();
            group.position.set(x, y, z);
            scene.add(group);
            const blanket = m.blanket.clone();
            blanket.color.set(available.has(number) ? bedColors.available : bedColors.unavailable);
            box(group, m.frame, 1.06, 0.075, 2.08, 0, -0.038, 0);
            box(group, m.timber, 0.96, 0.042, 1.99, 0, 0.009, 0);
            soft(group, m.sheet, 0.95, 0.143, 1.96, 0, 0.096, 0, 0.046);
            soft(group, m.linenEdge, 0.948, 0.023, 1.961, 0, 0.131, 0, 0.009);
            const pillow = soft(group, m.pillow, 0.7, 0.111, 0.39, 0, 0.195, -0.66, 0.052);
            pillow.rotation.y = side * 0.06;
            soft(group, blanket, 0.96, 0.059, 1.28, 0, 0.18, 0.29, 0.027);
            soft(group, blanket, 0.955, 0.036, 0.12, 0, 0.215, -0.3, 0.016);
            soft(group, blanket, 0.06, 0.141, 1.12, -side * 0.45, 0.12, 0.34, 0.027);
            group.children.forEach(mesh => { mesh.userData.bed = number; pickable.push(mesh); });
            beds.set(number, {
                blanket, side, x, y, z,
                marker: new THREE.Vector3(x - side * 0.47, y + 0.21, z + 0.31),
                planMarker: new THREE.Vector3(x + side * (level ? 0.23 : -0.38), 1.76, z + (level ? -0.64 : 0.64))
            });
        }
    }
    const bounds = new THREE.Box3().setFromObject(scene);
    addGroundShadow(scene, width, depth);
    const landmarks = [
        { label: "Cửa sổ", position: new THREE.Vector3(0, 1.65, back + 0.55) },
        { label: "Cửa vào", position: new THREE.Vector3(0, 0.05, front - 0.48) },
        { label: "WC", position: new THREE.Vector3(width / 2 - 1.02, 0.65, front - 1.06) }
    ];
    return { width, depth, features, beds, pickable, landmarks, bounds };
}
