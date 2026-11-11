const e2ePassword = process.env.DORM_E2E_PASSWORD;
const baseUrl = process.env.DORM_E2E_BASE_URL || "http://127.0.0.1:5001";

if (!e2ePassword) {
  throw new Error("Set DORM_E2E_PASSWORD to the local demo-account password before running browser checks.");
}

module.exports = { baseUrl, e2ePassword };
