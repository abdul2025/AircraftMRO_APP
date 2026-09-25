// Live notifications: SignalR push, catch-up after (re)connecting, the topbar bell, and toasts.
// Unread state is per browser (localStorage) until notifications are tied to signed-in users.
(() => {
  "use strict";

  const bell = document.querySelector("[data-notification-bell]");
  if (!bell || !window.signalR) {
    return;
  }

  const storageKey = "aircraft-mro-last-seen-notification";
  const maxListItems = 10;
  const list = bell.querySelector("[data-notification-list]");
  const badge = bell.querySelector("[data-notification-badge]");
  const empty = bell.querySelector("[data-notification-empty]");
  const status = bell.querySelector("[data-notification-status]");
  const toastHost = document.querySelector("[data-toast-host]");

  const seen = new Set([...list.querySelectorAll("[data-notification-id]")].map((item) => Number(item.dataset.notificationId)));
  let latestId = Number(bell.dataset.latestId) || 0;

  const readLastSeen = () => {
    try {
      return Number(localStorage.getItem(storageKey)) || 0;
    } catch {
      return 0;
    }
  };

  const writeLastSeen = (id) => {
    try {
      localStorage.setItem(storageKey, String(id));
    } catch {
      // Unread counts reset on the next page when storage is unavailable.
    }
  };

  // First visit: treat what is already listed as seen rather than flagging old history as unread.
  let lastSeen = readLastSeen();
  if (!lastSeen) {
    lastSeen = latestId;
    writeLastSeen(lastSeen);
  }

  // Same "25 Sep 2026, 08:00 UTC" shape the server renders; built by hand because locale data varies ("Sep" vs "Sept").
  const months = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
  const pad = (value) => String(value).padStart(2, "0");
  const formatTime = (iso) => {
    const date = new Date(iso);
    return `${date.getUTCDate()} ${months[date.getUTCMonth()]} ${date.getUTCFullYear()}, ${pad(date.getUTCHours())}:${pad(date.getUTCMinutes())} UTC`;
  };

  const updateBadge = () => {
    const unread = [...seen].filter((id) => id > lastSeen).length;
    badge.hidden = unread === 0;
    badge.textContent = unread > 9 ? "9+" : String(unread);
    bell.querySelector("button").setAttribute("aria-label", unread ? `Notifications, ${unread} unread` : "Notifications");
  };

  const setStatus = (text) => {
    status.textContent = text;
  };

  // Notification text is data: build DOM with textContent only.
  const renderItem = (notification) => {
    const item = document.createElement("li");
    item.className = "notification-item";
    item.dataset.notificationId = String(notification.id);

    const message = document.createElement(notification.url ? "a" : "span");
    message.className = "notification-item__message";
    message.textContent = notification.message;
    if (notification.url) {
      message.href = notification.url;
    }

    const time = document.createElement("time");
    time.className = "notification-item__time";
    time.dateTime = notification.occurredAtUtc;
    time.textContent = formatTime(notification.occurredAtUtc);

    item.append(message, time);
    return item;
  };

  const showToast = (notification) => {
    if (!toastHost || !window.bootstrap) {
      return;
    }

    const toast = document.createElement("div");
    toast.className = "toast notification-toast";
    toast.setAttribute("role", "status");

    const body = document.createElement("div");
    body.className = "toast-body";
    const text = document.createElement(notification.url ? "a" : "span");
    text.textContent = notification.message;
    if (notification.url) {
      text.href = notification.url;
    }

    const close = document.createElement("button");
    close.type = "button";
    close.className = "btn-close";
    close.dataset.bsDismiss = "toast";
    close.setAttribute("aria-label", "Dismiss");

    body.append(text, close);
    toast.append(body);
    toastHost.append(toast);
    toast.addEventListener("hidden.bs.toast", () => toast.remove());
    bootstrap.Toast.getOrCreateInstance(toast, { delay: 6000 }).show();
  };

  const add = (notification, { toast }) => {
    if (seen.has(notification.id)) {
      return;
    }

    seen.add(notification.id);
    latestId = Math.max(latestId, notification.id);
    list.prepend(renderItem(notification));
    while (list.children.length > maxListItems) {
      const removed = list.lastElementChild;
      seen.delete(Number(removed.dataset.notificationId));
      removed.remove();
    }

    empty.hidden = true;
    updateBadge();
    if (toast) {
      showToast(notification);
    }

    // Pages showing this entity can react (e.g. offer a refresh).
    document.dispatchEvent(new CustomEvent("app:notification", { detail: notification }));
  };

  const catchUp = async () => {
    const response = await fetch(`${bell.dataset.sinceUrl}?afterId=${encodeURIComponent(latestId)}`, {
      headers: { Accept: "application/json" }
    });
    if (response.ok) {
      (await response.json()).forEach((notification) => add(notification, { toast: false }));
    }
  };

  const connection = new signalR.HubConnectionBuilder()
    .withUrl(bell.dataset.hubUrl)
    .withAutomaticReconnect()
    .build();

  connection.on("NotificationReceived", (notification) => add(notification, { toast: true }));
  connection.onreconnecting(() => setStatus("Reconnecting…"));
  connection.onreconnected(async () => {
    setStatus("Live");
    await catchUp();
  });

  const start = async () => {
    try {
      await connection.start();
      setStatus("Live");
      await catchUp();
    } catch {
      setStatus("Offline, retrying…");
      window.setTimeout(start, 5000);
    }
  };

  // Automatic reconnect gives up after a few attempts; keep trying on a slower cadence.
  connection.onclose(() => {
    setStatus("Offline, retrying…");
    window.setTimeout(start, 5000);
  });

  bell.addEventListener("shown.bs.dropdown", () => {
    lastSeen = Math.max(lastSeen, latestId);
    writeLastSeen(lastSeen);
    updateBadge();
  });

  updateBadge();
  start();
})();
