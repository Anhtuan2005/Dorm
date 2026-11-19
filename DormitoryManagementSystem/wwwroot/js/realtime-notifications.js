(() => {
    const root = document.querySelector("[data-live-hub][data-live-snapshot]");
    const client = window.signalR;
    if (!root?.dataset.liveHub || !root.dataset.liveSnapshot) return;
    const studentPortal = root.dataset.livePortal === "student";
    const sessionChannel = studentPortal && typeof BroadcastChannel === "function"
        ? new BroadcastChannel("nepktx-student-session") : null;
    if (sessionChannel) sessionChannel.onmessage = event => {
        if (event.data === "locked") endSession(true, false);
    };

    const connection = client ? new client.HubConnectionBuilder()
        .withUrl(root.dataset.liveHub)
        .withAutomaticReconnect()
        .configureLogging(client.LogLevel.None)
        .build() : null;
    let retryTimer;
    let snapshotTimer;
    let stopped = false;
    let fetching = false;
    let queued = false;
    let operationsRevision;
    const operationsWorklist = root.querySelector("[data-operations-worklist]");

    function updateBadge(badge, value, abbreviate = false) {
        const text = abbreviate && value > 99 ? "99+" : String(value);
        if (badge.textContent.trim() !== text) badge.textContent = text;
        badge.hidden = value === 0;
    }

    async function updateStudent(response, panel) {
        const header = response.headers.get("X-Notifications-Unread");
        const value = panel ? (header === null ? NaN : Number(header)) : (await response.json()).unread;
        if (!Number.isSafeInteger(value) || value < 0) return;

        const badge = root.querySelector("[data-live-unread]");
        if (badge) {
            updateBadge(badge, value, true);
            badge.parentElement.setAttribute("aria-label", value ? `Thông báo · ${value} chưa đọc` : "Thông báo");
        }
        const markAll = root.querySelector("[data-live-mark-all]");
        if (markAll) markAll.hidden = value === 0;
        if (!panel) return;

        const html = new DOMParser().parseFromString(await response.text(), "text/html");
        const replacement = html.querySelector("[data-live-notices]");
        if (!replacement || replacement.dataset.liveNotices !== panel.dataset.liveNotices ||
            replacement.innerHTML === panel.innerHTML) return;

        const active = panel.contains(document.activeElement) ? document.activeElement : null;
        const activeNotice = active?.dataset.noticeId;
        const activeHref = active?.getAttribute("href");
        panel.replaceWith(replacement);
        if (active) {
            const target = [...replacement.querySelectorAll("a")].find(link =>
                activeNotice ? link.dataset.noticeId === activeNotice : link.getAttribute("href") === activeHref);
            target?.focus({ preventScroll: true });
        }
    }

    async function updateOperations(response) {
        const counts = await response.json();
        for (const badge of root.querySelectorAll("[data-live-attention]")) {
            const key = badge.dataset.liveAttention;
            const count = counts[key];
            if (!Number.isSafeInteger(count) || count < 0) continue;
            updateBadge(badge, count, key === "unread");
            if (key === "unread") badge.parentElement.setAttribute("aria-label",
                count ? `Thông báo · ${count} chưa đọc` : "Thông báo");
        }
        const markAll = root.querySelector("[data-operations-mark-read]");
        if (markAll) markAll.hidden = counts.unread === 0;
        const overdue = root.querySelector("[data-live-overdue-invoices]");
        if (overdue && Number.isSafeInteger(counts.overdueInvoices)) {
            overdue.textContent = String(counts.overdueInvoices);
            const total = root.querySelector("[data-live-overdue-total]");
            if (total) total.textContent = `${Number(total.dataset.liveOverdueTotal) + counts.overdueInvoices} việc ưu tiên`;
        }

        const revision = counts[`${operationsWorklist?.dataset.operationsWorklist}Revision`];
        if (revision !== undefined && revision !== operationsRevision && await refreshOperationsRegions())
            operationsRevision = revision;
    }

    async function refreshOperationsRegions() {
        const url = new URL(location.href);
        const selected = operationsWorklist.dataset.operationsSelected;
        if (selected) url.searchParams.set("SelectedId", selected);
        const response = await fetch(url, { cache: "no-store", redirect: "manual", headers: { "X-Operations-Refresh": "true" } });
        if (!response.ok) return false;
        const html = new DOMParser().parseFromString(await response.text(), "text/html");
        const freshWorklist = html.querySelector("[data-operations-worklist]");
        const caseChanged = operationsWorklist.querySelector("[data-operations-case-changed]");
        if (caseChanged && freshWorklist && operationsWorklist.dataset.operationsVersion !== freshWorklist.dataset.operationsVersion)
            caseChanged.hidden = false;
        let updated = true;
        for (const region of operationsWorklist.querySelectorAll("[data-operations-region]")) {
            const next = html.querySelector(`[data-operations-region="${region.dataset.operationsRegion}"]`);
            if (!next) continue;
            if (region.contains(document.activeElement)) { updated = false; continue; }

            const listSelector = "[data-record-list]";
            const oldList = region.querySelector(listSelector);
            const scroll = oldList?.scrollTop ?? 0;
            region.replaceChildren(...next.childNodes);
            const list = region.querySelector(listSelector);
            if (list) list.scrollTop = scroll;
        }
        return updated;
    }

    async function fetchSnapshot() {
        const panel = studentPortal ? root.querySelector("[data-live-notices]") : null;
        const url = new URL(root.dataset.liveSnapshot, location.origin);
        if (panel) url.searchParams.set("panel", panel.dataset.liveNotices);
        if (panel?.dataset.noticePage) url.searchParams.set("page", panel.dataset.noticePage);
        const response = await fetch(url, { cache: "no-store", credentials: "same-origin", redirect: "manual" });
        if (response.type === "opaqueredirect" || response.status === 401 || response.status === 403) {
            endSession(response.headers.get("X-Student-Session") === "locked");
            return;
        }
        if (!response.ok) return;
        if (studentPortal) await updateStudent(response, panel);
        else await updateOperations(response);
    }

    function endSession(locked = false, broadcast = true) {
        if (stopped) return;
        stopped = true;
        // Tabs share the cookie, so its removal can precede a sibling's next poll.
        if (locked && broadcast) sessionChannel?.postMessage("locked");
        sessionChannel?.close();
        clearTimeout(retryTimer);
        clearTimeout(snapshotTimer);
        if (root.dataset.liveLogin) {
            const login = new URL(root.dataset.liveLogin, location.origin);
            if (locked) login.searchParams.set("reason", "locked");
            location.replace(login);
        } else void connection?.stop();
    }

    async function drain() {
        fetching = true;
        try {
            while (queued && !stopped) {
                queued = false;
                try { await fetchSnapshot(); }
                catch { /* Keep the server-rendered state until the next refresh. */ }
            }
        } finally {
            fetching = false;
        }
    }

    function refresh() {
        if (stopped) return;
        queued = true;
        if (!fetching) void drain();
    }

    function retry() {
        if (!stopped && retryTimer === undefined) {
            retryTimer = setTimeout(() => {
                retryTimer = undefined;
                void connect();
            }, 5000);
        }
    }

    async function connect() {
        if (stopped || !connection || connection.state !== client.HubConnectionState.Disconnected) return;
        try {
            await connection.start();
            refresh();
            scheduleSnapshot();
        } catch (error) {
            if (error?.statusCode === 401 || error?.statusCode === 403) refresh();
            else retry();
        }
    }

    function scheduleSnapshot() {
        clearTimeout(snapshotTimer);
        if (stopped) return;
        const connected = connection && connection.state === client.HubConnectionState.Connected;
        const delay = studentPortal || connected ? 15000 : 4000;
        snapshotTimer = setTimeout(() => {
            if (studentPortal || document.visibilityState === "visible") refresh();
            scheduleSnapshot();
        }, delay);
    }

    connection?.on(studentPortal ? "StudentNotificationsChanged" : "OperationsAttentionChanged", refresh);
    const supplementNotice = root.querySelector("[data-housing-supplement-notice]");
    if (supplementNotice) {
        const link = supplementNotice.querySelector("a");
        const caseUrl = new URL(link.href);
        connection?.on("HousingRequestSupplemented", (id, code) => {
            if (stopped || !/^\d+$/.test(id) || typeof code !== "string") return;
            caseUrl.searchParams.set("SelectedId", id);
            link.href = caseUrl.href;
            link.textContent = `Xem đơn ${code}`;
            supplementNotice.hidden = false;
        });
    }
    connection?.onreconnecting(scheduleSnapshot);
    connection?.onreconnected(() => {
        refresh();
        scheduleSnapshot();
    });
    connection?.onclose(retry);
    if (studentPortal) {
        connection?.on("StudentSessionRevoked", () => endSession(true));
    }
    scheduleSnapshot();
    window.addEventListener("pageshow", event => { if (event.persisted) refresh(); });
    document.addEventListener("visibilitychange", () => {
        if (document.visibilityState === "visible") {
            refresh();
            void connect();
        }
    });
    window.addEventListener("online", () => {
        refresh();
        void connect();
    });
    if (!studentPortal) refresh();
    void connect();
})();
