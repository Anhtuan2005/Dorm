const { copyFileSync, mkdirSync } = require("node:fs");
const path = require("node:path");

const vendor = path.resolve(__dirname, "../DormitoryManagementSystem/wwwroot/js/vendor/meter-ocr");
mkdirSync(vendor, { recursive: true });

// The portable LSTM build also runs on browsers without WebAssembly SIMD.
const assets = {
    "tesseract.js/dist/tesseract.esm.min.js": "tesseract.esm.min.js",
    "tesseract.js/dist/tesseract.min.js.LICENSE.txt": "tesseract.min.js.LICENSE.txt",
    "tesseract.js/dist/worker.min.js": "worker.min.js",
    "tesseract.js/dist/worker.min.js.LICENSE.txt": "worker.min.js.LICENSE.txt",
    "tesseract.js/LICENSE.md": "LICENSE.txt",
    "tesseract.js-core/tesseract-core-lstm.wasm.js": "tesseract-core-lstm.wasm.js",
    "@tesseract.js-data/eng/4.0.0_best_int/eng.traineddata.gz": "eng.traineddata.gz"
};
for (const [source, filename] of Object.entries(assets)) {
    copyFileSync(require.resolve(source), path.join(vendor, filename));
}
