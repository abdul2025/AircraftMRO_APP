(() => {
  "use strict";

  const storageKey = "aircraft-mro-theme";
  const root = document.documentElement;
  const toggle = document.querySelector("[data-theme-toggle]");

  if (!toggle) {
    return;
  }

  const getTheme = () => root.dataset.theme === "dark" ? "dark" : "light";

  const syncToggle = () => {
    const isDark = getTheme() === "dark";
    const label = isDark ? "Switch to light theme" : "Switch to dark theme";
    toggle.setAttribute("aria-pressed", String(isDark));
    toggle.setAttribute("aria-label", label);
    toggle.setAttribute("title", label);
  };

  toggle.addEventListener("click", () => {
    const nextTheme = getTheme() === "dark" ? "light" : "dark";
    root.dataset.theme = nextTheme;

    try {
      localStorage.setItem(storageKey, nextTheme);
    } catch {
      // The selected theme still applies for this page when storage is unavailable.
    }

    syncToggle();
  });

  syncToggle();
})();
