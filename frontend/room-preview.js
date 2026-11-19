import * as THREE from "three";
import { OrbitControls } from "three/addons/controls/OrbitControls.js";
import { bedColors, createRoomModel } from "./dorm-room-model.js";

const page = document.querySelector("[data-room-tour]");
const capacity = Number(page.dataset.capacity);
const available = new Set(page.dataset.available.split(",").filter(Boolean).map(Number));
const select = page.querySelector("[data-tour-bed-select]");
const selection = page.querySelector("[data-tour-selection]");
const stage = page.querySelector("[data-tour-stage]");
const reset = page.querySelector("[data-tour-reset]");
const modes = page.querySelector("[data-tour-modes]");
const overview = page.querySelector("[data-tour-overview]");
const planButton = page.querySelector("[data-tour-plan]");
const view = page.querySelector("[data-tour-view]");
const legend = page.querySelector("[data-tour-legend]");
const instructions = page.querySelector("[data-tour-instructions]");
const fallback = page.querySelector("[data-tour-fallback]");
const canvasHost = page.querySelector("[data-tour-canvas]");
const markersHost = page.querySelector("[data-tour-markers]");
const amenities = [...page.querySelectorAll("[data-room-amenities] li")].map(item => item.textContent.trim());
const bedName = number => `Giường ${String(number).padStart(2, "0")}`;
const bedTier = number => number % 2 ? "Tầng dưới" : "Tầng trên";
const bedState = number => available.has(number) ? "Có thể xếp" : "Không thể xếp";
let active = Number(page.dataset.selected);
let tour;

function chooseBed(number) {
    if (number !== 0 && !available.has(number)) return;
    active = number;
    if (select) select.value = number ? String(number) : "";
    selection.hidden = !number;
    if (number) selection.textContent = `${bedName(number)} · ${bedTier(number)} · ${bedState(number)}`;
    tour?.focus(number);
}

select?.addEventListener("change", () => chooseBed(Number(select.value)));
reset.addEventListener("click", () => tour?.focus(0));
overview?.addEventListener("click", () => tour?.focus(0));
planButton?.addEventListener("click", () => tour?.showPlan());
stage.addEventListener("keydown", event => {
    if (event.key === "Escape") {
        event.preventDefault();
        tour?.focus(0);
        (overview ?? reset).focus({ preventScroll: true });
    }
});

function mountTour() {
    const canvas = document.createElement("canvas");
    if (!canvas.getContext("webgl2", { antialias: true, alpha: false })) return;
    const renderer = new THREE.WebGLRenderer({ canvas, antialias: true, alpha: false });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.65));
    renderer.domElement.setAttribute("aria-hidden", "true");
    canvasHost.append(renderer.domElement);

    const scene = new THREE.Scene();
    scene.background = new THREE.Color("#e8ebe4");
    renderer.toneMapping = THREE.NeutralToneMapping;
    renderer.toneMappingExposure = 1.05;
    const room = createRoomModel(scene, capacity, available, amenities);
    stage.dataset.modeledAmenities = Object.keys(room.features).filter(key => room.features[key]).join(",");

    scene.add(new THREE.HemisphereLight("#f2f7fc", "#b09e87", 1.36));
    const fill = new THREE.DirectionalLight("#e4f0ee", 0.74);
    fill.position.set(4, 3, 6);
    scene.add(fill);
    const sun = new THREE.DirectionalLight("#fff0da", 2.52);
    sun.position.set(-3.6, 7.8, -3.2);
    if (capacity <= 32) {
        renderer.shadowMap.enabled = true;
        sun.castShadow = true;
        sun.shadow.mapSize.set(1024, 1024);
        const shadowArea = Math.max(room.width, room.depth) * 0.76;
        Object.assign(sun.shadow.camera, {
            left: -shadowArea, right: shadowArea, top: shadowArea, bottom: -shadowArea, near: 0.2, far: 25
        });
        sun.shadow.camera.updateProjectionMatrix();
        sun.shadow.normalBias = 0.028;
        sun.shadow.radius = 2;
        // The furnishings are static, so camera motion does not require another shadow pass.
        sun.shadow.autoUpdate = false;
        sun.shadow.needsUpdate = true;
    }
    scene.add(sun);
    const camera = new THREE.PerspectiveCamera(43, 1, 0.05, Math.max(90, room.depth * 10));
    const controls = new OrbitControls(camera, canvas);
    controls.enablePan = false;
    controls.minDistance = 0.8;
    controls.minPolarAngle = 0.14;
    controls.maxPolarAngle = Math.PI / 2 - 0.025;
    controls.minAzimuthAngle = -1.38;
    controls.maxAzimuthAngle = 1.38;
    controls.maxDistance = room.depth * 5;
    const raycaster = new THREE.Raycaster();
    const pointer = new THREE.Vector2();
    const projected = new THREE.Vector3();
    const markers = new Map();
    let animation;
    let frame = 0;
    let width = 1;
    let height = 1;
    let cameraBed = 0;
    let inPlan = false;

    for (const [number] of room.beds) {
        const marker = document.createElement("button");
        marker.type = "button";
        marker.hidden = true;
        marker.textContent = String(number).padStart(2, "0");
        marker.className = "room-tour-marker";
        marker.dataset.bed = String(number);
        marker.disabled = !available.has(number);
        marker.dataset.available = String(!marker.disabled);
        const label = `${bedName(number)} · ${bedTier(number)} · ${bedState(number)}`;
        marker.setAttribute("aria-label", label);
        marker.title = label;
        if (!marker.disabled) marker.addEventListener("click", () => chooseBed(number));
        markersHost.append(marker);
        markers.set(number, marker);
    }
    const landmarks = room.landmarks.map(({ label, position }) => {
        const element = document.createElement("span");
        element.className = "room-tour-landmark";
        element.textContent = label;
        element.hidden = true;
        markersHost.append(element);
        return { element, position };
    });

    function screenPoint(position) {
        projected.copy(position).project(camera);
        return {
            x: (projected.x + 1) * width / 2,
            y: (1 - projected.y) * height / 2,
            visible: Math.abs(projected.x) < 0.94 && Math.abs(projected.y) < 0.92 && Math.abs(projected.z) < 1
        };
    }

    function placeLabel(element, point) {
        element.style.left = `${point.x}px`;
        element.style.top = `${point.y}px`;
    }

    function spreadBedMarkers(labels) {
        if (labels.length < 2) return;
        const spacing = 46; // Keep the 44 px touch targets separate.
        labels.sort((a, b) => a.point.y - b.point.y);
        const first = labels[0].point;
        const last = labels.at(-1).point;
        const originalCenter = (first.y + last.y) / 2;
        for (let i = 1; i < labels.length; i++) {
            const point = labels[i].point;
            for (let j = 0; j < i; j++) {
                const other = labels[j].point;
                const dx = point.x - other.x;
                if (Math.abs(dx) < spacing)
                    point.y = Math.max(point.y, other.y + Math.sqrt(spacing * spacing - dx * dx));
            }
        }
        if (last.y - first.y > height - spacing) return;
        const shift = THREE.MathUtils.clamp(originalCenter - (first.y + last.y) / 2, 23 - first.y, height - 23 - last.y);
        for (const { marker, point } of labels) {
            point.y += shift;
            placeLabel(marker, point);
        }
    }

    function draw(now) {
        frame = 0;
        if (animation) {
            // Start at the first drawn frame and avoid jumping over the slide after a slow frame.
            animation.elapsed += Math.min(48, Math.max(0, now - (animation.lastFrame ?? now)));
            animation.lastFrame = now;
            const progress = Math.min(1, animation.elapsed / animation.duration);
            const ease = progress * progress * progress * (10 + progress * (-15 + progress * 6));
            const arc = Math.sin(ease * Math.PI) * animation.arc;
            camera.position.lerpVectors(animation.from, animation.to, ease);
            camera.position.z += arc;
            camera.position.y += arc * 0.3;
            controls.target.lerpVectors(animation.fromTarget, animation.toTarget, ease);
            controls.update();
            if (progress >= 1) stopSlide();
            else schedule();
        }
        renderer.render(scene, camera);
        const compactLabels = width < 460 && !cameraBed ? [] : null;
        for (const [number, marker] of markers) {
            const bed = room.beds.get(number);
            const point = screenPoint(inPlan ? bed.planMarker : bed.marker);
            marker.hidden = !point.visible || (cameraBed > 0 && number !== cameraBed) || (room.beds.size > 24 && !cameraBed);
            if (!marker.hidden && compactLabels) compactLabels.push({ marker, point });
            placeLabel(marker, point);
        }
        if (compactLabels) spreadBedMarkers(compactLabels);
        for (const { element, position } of landmarks) {
            const point = inPlan ? screenPoint(position) : null;
            element.hidden = !point?.visible;
            if (!element.hidden) placeLabel(element, point);
        }
    }

    function schedule() {
        if (!frame) frame = requestAnimationFrame(draw);
    }

    function stopSlide() {
        animation = null;
        stage.classList.remove("is-sliding");
    }

    function targetPose(number) {
        const bed = room.beds.get(number);
        if (bed) return {
            position: new THREE.Vector3(bed.x - bed.side * 2.65, bed.y + 0.76, bed.z + 1.58),
            target: new THREE.Vector3(bed.x, bed.y + 0.17, bed.z)
        };
        const target = room.bounds.getCenter(new THREE.Vector3());
        target.y *= inPlan ? 1 : 0.49;
        const outward = inPlan
            ? new THREE.Vector3(0, 1, 0.022).normalize()
            : new THREE.Vector3(0.76, 0.92, 0.86).normalize();
        const right = new THREE.Vector3(0, 1, 0).cross(outward).normalize();
        const up = outward.clone().cross(right);
        const vertical = Math.tan(THREE.MathUtils.degToRad(camera.fov / 2)) * 0.96;
        let distance = 0;
        for (const x of [room.bounds.min.x, room.bounds.max.x]) {
            for (const y of [room.bounds.min.y, room.bounds.max.y]) {
                for (const z of [room.bounds.min.z, room.bounds.max.z]) {
                    const corner = new THREE.Vector3(x, y, z).sub(target);
                    distance = Math.max(distance, corner.dot(outward) + Math.max(
                        Math.abs(corner.dot(right)) / (vertical * camera.aspect),
                        Math.abs(corner.dot(up)) / vertical));
                }
            }
        }
        return { position: target.clone().addScaledVector(outward, distance), target };
    }

    function focus(number, immediate = false, topDown = false) {
        inPlan = topDown;
        cameraBed = number;
        controls.enableRotate = !inPlan;
        controls.minPolarAngle = inPlan ? 0.001 : 0.14;
        stage.dataset.focusedBed = String(number);
        stage.dataset.tourMode = inPlan ? "plan" : number ? "bed" : "overview";
        overview?.setAttribute("aria-pressed", String(!inPlan && !number));
        planButton?.setAttribute("aria-pressed", String(inPlan));
        view.textContent = number ? bedName(number) : inPlan ? "Mặt bằng" : "Toàn cảnh";
        reset.hidden = Boolean(modes && !number);
        if (instructions) {
            const planAction = room.beds.size > 24
                ? "Chọn số giường trong danh sách" : "Bấm số giường còn chỗ để xem cận cảnh";
            instructions.textContent = inPlan ? `Nhìn từ trên xuống · ${planAction}`
                : "Kéo để xoay · Bấm giường còn chỗ để xem cận cảnh";
        }
        for (const [id, bed] of room.beds) {
            bed.blanket.color.set(id === active ? bedColors.selected : available.has(id) ? bedColors.available : bedColors.unavailable);
            markers.get(id).setAttribute("aria-pressed", String(id === active));
        }
        const { position, target } = targetPose(number);
        if (immediate) {
            stopSlide();
            camera.position.copy(position);
            controls.target.copy(target);
            controls.update();
        } else {
            const distance = camera.position.distanceTo(position);
            animation = {
                from: camera.position.clone(), to: position, fromTarget: controls.target.clone(), toTarget: target,
                lastFrame: null, elapsed: 0, duration: THREE.MathUtils.clamp(660 + distance * 31, 750, 1140),
                arc: Math.min(1.22, distance * 0.17)
            };
            stage.classList.add("is-sliding");
        }
        schedule();
    }

    function resize() {
        const nextWidth = Math.max(1, canvasHost.clientWidth);
        const nextHeight = Math.max(1, canvasHost.clientHeight);
        if (width === nextWidth && height === nextHeight) return;
        width = nextWidth;
        height = nextHeight;
        camera.aspect = width / height;
        camera.updateProjectionMatrix();
        renderer.setSize(width, height, false);
        if (animation) {
            const pose = targetPose(cameraBed);
            animation.to.copy(pose.position);
            animation.toTarget.copy(pose.target);
            schedule();
        } else focus(cameraBed, true, inPlan);
    }

    controls.addEventListener("change", schedule);
    const pointers = new Map();
    canvas.addEventListener("pointerdown", event => {
        pointers.set(event.pointerId, { x: event.clientX, y: event.clientY, canPick: event.isPrimary && event.button === 0 });
        if (pointers.size > 1) {
            for (const pointer of pointers.values()) pointer.canPick = false;
            stopSlide();
        }
    });
    canvas.addEventListener("pointermove", event => {
        const start = pointers.get(event.pointerId);
        if (start && Math.hypot(event.clientX - start.x, event.clientY - start.y) > 6) {
            start.canPick = false;
            stopSlide();
        }
    }, { capture: true });
    canvas.addEventListener("wheel", stopSlide, { capture: true, passive: true });
    canvas.addEventListener("pointerup", event => {
        const start = pointers.get(event.pointerId);
        pointers.delete(event.pointerId);
        if (!start?.canPick || Math.hypot(event.clientX - start.x, event.clientY - start.y) > 6) return;
        if (inPlan) return; // The numbered controls distinguish stacked beds.
        const bounds = canvas.getBoundingClientRect();
        pointer.set((event.clientX - bounds.left) / bounds.width * 2 - 1, 1 - (event.clientY - bounds.top) / bounds.height * 2);
        raycaster.setFromCamera(pointer, camera);
        const hit = raycaster.intersectObjects(room.pickable, false)[0];
        if (hit && available.has(hit.object.userData.bed)) chooseBed(hit.object.userData.bed);
    });
    canvas.addEventListener("pointercancel", event => pointers.delete(event.pointerId));
    canvas.addEventListener("lostpointercapture", event => pointers.delete(event.pointerId));

    stage.hidden = false;
    legend.hidden = false;
    if (modes) modes.hidden = false;
    fallback.hidden = true;
    resize();
    if (active) focus(active, true);
    const watcher = new ResizeObserver(resize);
    watcher.observe(canvasHost);
    canvas.addEventListener("webglcontextlost", event => {
        event.preventDefault();
        watcher.disconnect();
        controls.dispose();
        cancelAnimationFrame(frame);
        tour = null;
        stage.hidden = legend.hidden = true;
        if (modes) modes.hidden = true;
        fallback.hidden = false;
        fallback.textContent = "Không thể tiếp tục hiển thị 3D.";
    }, { once: true });
    return { focus, showPlan: () => focus(0, false, true) };
}

if (active) chooseBed(active);
try {
    tour = mountTour();
} catch {
    stage.hidden = legend.hidden = true;
    if (modes) modes.hidden = true;
    fallback.hidden = false;
}
if (!tour) fallback.textContent = select
    ? "Thiết bị không hiển thị được 3D. Bạn vẫn có thể chọn số giường trong danh sách."
    : "Thiết bị không hiển thị được mô hình 3D.";
