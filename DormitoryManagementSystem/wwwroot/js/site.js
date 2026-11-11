(() => {
  "use strict";

  const body = document.body;
  const sidebar = document.querySelector(".sidebar");
  const scrim = document.querySelector("[data-sidebar-scrim]");
  const desktopToggle = document.querySelector("[data-sidebar-toggle]");
  const mobileToggle = document.querySelector("[data-mobile-menu]");
  const toast = document.querySelector("[data-toast]");
  let toastTimer;

  if (toast?.textContent.trim()) {
    toastTimer = window.setTimeout(() => toast.classList.remove("visible"), 4200);
  }

  function showToast(message) {
    if (!toast || !message) return;
    toast.textContent = message;
    toast.classList.add("visible");
    window.clearTimeout(toastTimer);
    toastTimer = window.setTimeout(() => toast.classList.remove("visible"), 3200);
  }

  function closeMobileMenu() {
    sidebar?.classList.remove("open");
    scrim?.classList.remove("open");
  }

  desktopToggle?.addEventListener("click", () => {
    const collapsed = body.classList.toggle("collapsed");
    desktopToggle.setAttribute("aria-expanded", String(!collapsed));
    window.localStorage.setItem("nep-sidebar", collapsed ? "collapsed" : "expanded");
  });

  if (window.innerWidth > 1140 && window.localStorage.getItem("nep-sidebar") === "collapsed") {
    body.classList.add("collapsed");
    desktopToggle?.setAttribute("aria-expanded", "false");
  }

  mobileToggle?.addEventListener("click", () => {
    sidebar?.classList.add("open");
    scrim?.classList.add("open");
  });
  scrim?.addEventListener("click", closeMobileMenu);
  sidebar?.querySelectorAll("a").forEach((link) => link.addEventListener("click", closeMobileMenu));

  document.querySelectorAll("[data-demo-message]").forEach((element) => {
    element.addEventListener("click", (event) => {
      event.preventDefault();
      showToast(element.dataset.demoMessage);
    });
  });

  document.querySelectorAll("form[data-confirm]").forEach((form) => {
    form.addEventListener("submit", (event) => {
      if (!window.confirm(form.dataset.confirm)) event.preventDefault();
    });
  });

  document.querySelectorAll("[data-open-dialog]").forEach((button) => {
    button.addEventListener("click", () => {
      const dialog = document.getElementById(button.dataset.openDialog);
      if (dialog instanceof HTMLDialogElement) dialog.showModal();
    });
  });

  document.querySelectorAll(".manager-dialog").forEach((dialog) => {
    dialog.querySelectorAll("[data-dialog-close]").forEach((button) => {
      button.addEventListener("click", () => dialog.close());
    });
    dialog.addEventListener("click", (event) => {
      if (event.target === dialog) dialog.close();
    });
  });

  const filterForm = document.querySelector("[data-filter-form]");
  document.querySelectorAll("[data-auto-submit]").forEach((control) => {
    control.addEventListener("change", () => filterForm?.requestSubmit());
  });
  document.querySelector("[data-filter-toggle]")?.addEventListener("click", () => {
    document.querySelector("[data-filter-fields]")?.classList.toggle("open");
  });

  const page = document.querySelector(".room-map-page");
  document.querySelector("[data-detail-close]")?.addEventListener("click", () => {
    if (window.matchMedia("(max-width: 1140px)").matches) {
      document.querySelector(".detail-back")?.click();
      return;
    }
    page?.classList.add("detail-hidden");
  });

  function activateTab(name) {
    document.querySelectorAll("[data-tab]").forEach((tab) => {
      const active = tab.dataset.tab === name;
      tab.classList.toggle("active", active);
      tab.setAttribute("aria-selected", String(active));
    });
    document.querySelectorAll("[data-panel]").forEach((panel) => {
      panel.classList.toggle("active", panel.dataset.panel === name);
    });
  }

  document.querySelectorAll("[data-tab]").forEach((tab) => {
    tab.addEventListener("click", () => activateTab(tab.dataset.tab));
  });
  document.querySelectorAll("[data-tab-target]").forEach((button) => {
    button.addEventListener("click", () => activateTab(button.dataset.tabTarget));
  });

  const globalSearch = document.querySelector("[data-global-search]");
  globalSearch?.addEventListener("keydown", (event) => {
    if (event.key !== "Enter") return;
    event.preventDefault();
    const value = globalSearch.value.trim();
    if (!value) {
      window.location.assign("/Rooms/Map");
      return;
    }
    const isRoomCode = /^[a-z]-?\d{3}$/i.test(value.replace(/\s/g, ""));
    const destination = isRoomCode ? "/Rooms/Map" : "/Students";
    window.location.assign(`${destination}?Search=${encodeURIComponent(value)}`);
  });
})();
