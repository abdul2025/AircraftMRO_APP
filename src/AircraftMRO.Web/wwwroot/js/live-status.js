// Live indicators outside the notification bell: mirror the SignalR connection state that
// notifications.js broadcasts, and keep "Updated … ago" relative to when the server rendered it.
// Both re-apply after live-refresh.js swaps the page content, since the new markup starts fresh.
//
//   data-live-status                   text and data-state follow the connection (live, pending, offline)
//   <time data-updated-at datetime>    when the server rendered it, shown as "Updated 30s ago"
(() => {
  "use strict";

  let connection = null;

  const applyState = (root) => {
    if (!connection) {
      return;
    }
    root.querySelectorAll("[data-live-status]").forEach((element) => {
      element.textContent = connection.text;
      element.dataset.state = connection.state;
    });
  };

  const ago = (seconds) => {
    if (seconds < 10) {
      return "just now";
    }
    if (seconds < 60) {
      return `${Math.floor(seconds / 10) * 10}s ago`;
    }
    if (seconds < 3600) {
      return `${Math.floor(seconds / 60)} min ago`;
    }
    return `${Math.floor(seconds / 3600)} h ago`;
  };

  // Measured from when this browser received the markup, so a skewed server clock can't show
  // "Updated 5 min ago" on a page that just loaded.
  const receivedAt = new WeakMap();

  const applyTimes = (root) => {
    const now = Date.now();
    root.querySelectorAll("time[data-updated-at]").forEach((element) => {
      if (!receivedAt.has(element)) {
        receivedAt.set(element, now);
      }
      element.textContent = `Updated ${ago((now - receivedAt.get(element)) / 1000)}`;
    });
  };

  document.addEventListener("app:live-state", (event) => {
    connection = event.detail;
    applyState(document);
  });

  document.addEventListener("app:content-replaced", (event) => {
    applyState(event.detail.root);
    applyTimes(event.detail.root);
  });

  applyTimes(document);
  window.setInterval(() => applyTimes(document), 10_000);
})();
