// Auto-refresh: when a notification matches a [data-live-refresh] section on this page, re-fetch
// the page and swap in the new main content. No full reload, so toasts, scroll position, and the
// SignalR connection survive. A refresh waits while someone is mid-edit (modal open, or unsaved
// input on the page) and runs as soon as they are done.
//
//   data-live-refresh="Aircraft"      entity type to follow, or "*" for any change
//   data-live-refresh-id="<guid>"     optional: only this entity
//   data-live-refresh-gone="/url"     optional: where to go if the page no longer exists (404)
(() => {
  "use strict";

  const main = document.getElementById("main-content");
  if (!main) {
    return;
  }

  const modal = document.getElementById("app-modal");
  const debounceMs = 300;
  let timer = 0;
  let pending = false;
  let refreshing = false;

  const follows = (notification) =>
    [...main.querySelectorAll("[data-live-refresh]")].some((section) => {
      const type = section.dataset.liveRefresh;
      const id = section.dataset.liveRefreshId;
      return (type === "*" || type === notification.entityType) && (!id || id === notification.entityId);
    });

  const hasUnsavedInput = () =>
    [...main.querySelectorAll("input, textarea, select")].some((field) => {
      if (field.type === "hidden" || field.type === "submit" || field.type === "button") {
        return false;
      }
      if (field.tagName === "SELECT") {
        if (field.multiple) {
          return [...field.options].some((option) => option.selected !== option.defaultSelected);
        }
        // With no option marked selected in the HTML, the browser selects the first one by default.
        const defaultIndex = Math.max(0, [...field.options].findIndex((option) => option.defaultSelected));
        return field.selectedIndex !== defaultIndex;
      }
      if (field.type === "checkbox" || field.type === "radio") {
        return field.checked !== field.defaultChecked;
      }
      return field.value !== field.defaultValue;
    });

  // Someone about to type (cursor in a text field) would lose focus if the field were replaced.
  const isTyping = () => {
    const active = document.activeElement;
    return Boolean(active && main.contains(active) && active.matches("input:not([type=hidden]), textarea"));
  };

  const isBusy = () => Boolean(modal?.classList.contains("show")) || isTyping() || hasUnsavedInput();

  const refresh = async () => {
    if (refreshing) {
      pending = true;
      return;
    }
    if (isBusy()) {
      pending = true;
      return;
    }

    pending = false;
    refreshing = true;
    try {
      const response = await fetch(window.location.href, { headers: { Accept: "text/html" } });

      if (response.status === 404) {
        const gone = main.querySelector("[data-live-refresh-gone]")?.dataset.liveRefreshGone;
        if (gone) {
          window.location.assign(gone);
        }
        return;
      }
      if (!response.ok) {
        return;
      }

      const next = new DOMParser().parseFromString(await response.text(), "text/html").getElementById("main-content");
      // Someone may have started editing while the request was in flight; keep their input.
      if (!next || isBusy()) {
        pending = true;
        return;
      }

      main.replaceChildren(...next.childNodes);
      document.dispatchEvent(new CustomEvent("app:content-replaced", { detail: { root: main } }));
    } catch {
      // Network hiccup: the next notification or the next finished edit tries again.
      pending = true;
    } finally {
      refreshing = false;
    }
  };

  const schedule = () => {
    window.clearTimeout(timer);
    timer = window.setTimeout(refresh, debounceMs);
  };

  document.addEventListener("app:notification", (event) => {
    if (follows(event.detail)) {
      schedule();
    }
  });

  // Run a deferred refresh once the edit is finished.
  const retryIfPending = () => {
    if (pending) {
      schedule();
    }
  };
  modal?.addEventListener("hidden.bs.modal", retryIfPending);
  main.addEventListener("change", retryIfPending);
  main.addEventListener("focusout", retryIfPending);
})();
