(() => {
    "use strict";

    document.querySelectorAll(".main-nav a.active, .student-nav a.active, .mobile-bottom-nav a.active, .student-bottom-nav a.active")
        .forEach(link => link.setAttribute("aria-current", "page"));
    document.querySelectorAll(".main-nav a").forEach(link => {
        const label = link.querySelector("span")?.textContent.trim();
        if (label) { link.setAttribute("aria-label", label); link.title = label; }
    });
    document.querySelectorAll(".queue-summary, .billing-summary, .maintenance-summary").forEach(summary => {
        const active = summary.querySelector("a.active");
        if (active && summary.scrollWidth > summary.clientWidth)
            summary.scrollLeft = active.offsetLeft - summary.offsetLeft - summary.clientWidth / 2 + active.clientWidth / 2;
    });
    document.querySelectorAll("a.disabled").forEach(link => {
        link.setAttribute("aria-disabled", "true");
        link.tabIndex = -1;
        link.addEventListener("click", event => event.preventDefault());
    });
    document.querySelectorAll(".nav-count, .notification-badge, .student-notification span").forEach(count => {
        if (count.textContent.trim() === "0") count.hidden = true;
    });
    document.querySelectorAll("[data-flash-dismiss]").forEach(button => button.addEventListener("click", () => button.parentElement.remove()));
    const firstError = document.querySelector(".workspace-flash.error, .validation-summary-errors")
        ?? document.querySelector(".password-panel .input-validation-error, .auth-form .input-validation-error");
    firstError?.focus();

    const search = document.querySelector("[data-global-search]");
    document.addEventListener("keydown", event => {
        if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === "k" && search?.getClientRects().length) {
            event.preventDefault();
            search.focus();
            search.select();
        }
    });

    document.querySelectorAll("[data-password-toggle]").forEach(button => button.addEventListener("click", () => {
        const input = button.parentElement.querySelector("input");
        const visible = input.type === "password";
        input.type = visible ? "text" : "password";
        button.textContent = visible ? "Ẩn" : "Hiện";
        const label = button.dataset.passwordLabel ?? "mật khẩu";
        button.setAttribute("aria-label", `${button.textContent} ${label}`);
    }));

    const newPassword = document.querySelector("[data-new-password]");
    const confirmPassword = document.querySelector("[data-confirm-password]");
    const passwordMatch = document.querySelector("[data-password-match]");
    if (newPassword && confirmPassword && passwordMatch) {
        const checkMatch = () => {
            const mismatch = confirmPassword.value && confirmPassword.value !== newPassword.value;
            const message = mismatch ? "Hai mật khẩu chưa khớp." : "";
            confirmPassword.setCustomValidity(message);
            if (passwordMatch.textContent !== message) passwordMatch.textContent = message;
        };
        newPassword.addEventListener("input", checkMatch);
        confirmPassword.addEventListener("input", checkMatch);
    }

    document.querySelectorAll(".billing-balance-filter").forEach(disclosure => {
        document.addEventListener("pointerdown", event => {
            if (disclosure.open && !disclosure.contains(event.target)) disclosure.open = false;
        });
        disclosure.addEventListener("keydown", event => {
            if (event.key === "Escape") {
                disclosure.open = false;
                disclosure.querySelector("summary")?.focus();
            }
        });
    });

    const navigation = document.querySelector(".sidebar, .student-sidebar");
    const opener = document.querySelector("[data-mobile-menu], [data-student-menu]");
    const scrim = document.querySelector("[data-sidebar-scrim], [data-student-scrim]");
    if (navigation && opener) {
        const mobile = matchMedia(document.body.classList.contains("student-body") ? "(max-width: 980px)" : "(max-width: 1140px)");
        const sync = () => {
            const open = mobile.matches && navigation.classList.contains("open");
            navigation.inert = mobile.matches && !open;
            document.querySelectorAll("#main-content, .topbar, .student-topbar, .mobile-bottom-nav, .student-bottom-nav")
                .forEach(element => { element.inert = open; });
            opener.setAttribute("aria-expanded", String(open));
            if (open) { navigation.setAttribute("role", "dialog"); navigation.setAttribute("aria-modal", "true"); }
            else { navigation.removeAttribute("role"); navigation.removeAttribute("aria-modal"); }
            document.body.classList.toggle("navigation-open", open);
        };
        const close = () => {
            navigation.classList.remove("open");
            scrim?.classList.remove("open");
            sync();
            opener.focus();
        };
        new MutationObserver(sync).observe(navigation, { attributes: true, attributeFilter: ["class"] });
        mobile.addEventListener("change", sync);
        document.querySelector("[data-navigation-close]")?.addEventListener("click", close);
        scrim?.addEventListener("click", close);
        opener.addEventListener("click", () => requestAnimationFrame(() => navigation.querySelector("[data-navigation-close]")?.focus()));
        document.addEventListener("keydown", event => {
            if (!mobile.matches || !navigation.classList.contains("open") || document.querySelector("dialog[open]")) return;
            if (event.key === "Escape") { event.preventDefault(); close(); }
            if (event.key === "Tab") {
                const items = [...navigation.querySelectorAll("a, button")].filter(element => element.getClientRects().length && !element.disabled);
                const first = items[0], last = items.at(-1);
                if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
                else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
            }
        });
        sync();
    }

    document.querySelectorAll(".billing-toolbar, .maintenance-toolbar, .student-directory-toolbar, .student-room-filters").forEach(form => {
        let fields = [...form.children].filter(child => child.matches("label:not(.queue-search), .billing-balance-filter"));
        if (form.classList.contains("student-room-filters")) fields = fields.slice(1);
        if (fields.length < 2) return;
        const disclosure = document.createElement("details");
        disclosure.className = "filter-options";
        const summary = document.createElement("summary");
        const active = fields.some(field => [...field.querySelectorAll("select,input")].some(control =>
            control.value && (control.tagName !== "SELECT" || control.value !== control.options[0]?.value)));
        summary.textContent = active ? "Bộ lọc đang áp dụng" : "Bộ lọc";
        const group = document.createElement("div");
        group.className = "filter-options-fields";
        fields.forEach(field => group.append(field));
        disclosure.append(summary, group);
        form.insertBefore(disclosure, form.querySelector(':scope > button[type="submit"]'));
        form.classList.add("has-filter-disclosure");
        const narrow = matchMedia("(max-width: 620px)");
        disclosure.open = !narrow.matches || active;
        narrow.addEventListener("change", () => { disclosure.open = !narrow.matches || active; });
    });

    const workbench = document.querySelector("[data-record-workbench]");
    const recordView = document.querySelector("[data-record-view]");
    const detail = workbench?.querySelector("[data-record-detail]");
    if (detail && recordView) {
        const narrow = matchMedia("(max-width: 800px)");
        const storageKey = `record-view:${workbench.dataset.recordWorkbench}`;
        const buttons = [...recordView.querySelectorAll("[data-record-mode]")];
        const title = detail.querySelector("h2");
        if (title) {
            title.id ||= "selected-case-title";
            detail.setAttribute("aria-labelledby", title.id);
        }

        let preferredMode;
        try { preferredMode = sessionStorage.getItem(storageKey); } catch { /* Storage may be unavailable. */ }
        const setMode = mode => {
            workbench.dataset.recordLayout = mode;
            detail.hidden = mode === "list";
            buttons.forEach(button => button.setAttribute("aria-pressed", String(button.dataset.recordMode === mode)));
        };
        const focusCase = () => requestAnimationFrame(() => {
            recordView.scrollIntoView({ block: "nearest" });
            detail.focus({ preventScroll: true });
        });
        const updateLabels = () => {
            buttons[1].textContent = narrow.matches ? "Chi tiết" : "Danh sách và chi tiết";
        };
        const deepLink = [...new URLSearchParams(location.search).keys()].some(key => key.toLowerCase() === "selectedid");
        const caseHash = "#selected-case";
        const listHash = "#record-list";
        const showCase = () => {
            if (location.hash !== caseHash) return;
            setMode("split");
            focusCase();
        };

        recordView.hidden = false;
        updateLabels();
        const openDetail = deepLink || location.hash === caseHash || detail.contains(document.getElementById(location.hash.slice(1)));
        const hasMultiplePages = workbench.querySelector("[data-record-queue] .billing-pagination") !== null;
        const defaultMode = preferredMode ?? (narrow.matches || hasMultiplePages ? "list" : "split");
        setMode(location.hash === listHash ? "list" : openDetail ? "split" : defaultMode);
        showCase();
        narrow.addEventListener("change", updateLabels);
        window.addEventListener("hashchange", showCase);
        buttons.forEach(button => button.addEventListener("click", () => {
            const mode = button.dataset.recordMode;
            setMode(mode);
            try { sessionStorage.setItem(storageKey, mode); } catch { /* Storage may be unavailable. */ }
            history.replaceState(history.state, "", mode === "list" ? listHash : caseHash);
        }));
    }

    document.querySelectorAll('form[method="post"]:not([data-chat-form])').forEach(form => form.addEventListener("submit", event => {
        if (event.defaultPrevented) return;
        if (form.dataset.pending === "true") { event.preventDefault(); return; }
        form.dataset.pending = "true";
        form.setAttribute("aria-busy", "true");
    }));
    window.addEventListener("pageshow", () => document.querySelectorAll("form[data-pending]").forEach(form => {
        delete form.dataset.pending;
        form.removeAttribute("aria-busy");
    }));
})();
