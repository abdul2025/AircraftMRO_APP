// Topbar clock: local date and time plus UTC, refreshed on each minute boundary.
(() => {
  "use strict";

  const clock = document.querySelector("[data-clock]");
  if (!clock) {
    return;
  }

  const dateElement = clock.querySelector("[data-clock-date]");
  const localElement = clock.querySelector("[data-clock-local]");
  const utcElement = clock.querySelector("[data-clock-utc]");

  const dateFormat = new Intl.DateTimeFormat("en-GB", { weekday: "short", day: "numeric", month: "short", year: "numeric" });
  const localFormat = new Intl.DateTimeFormat("en-GB", { hour: "2-digit", minute: "2-digit", hourCycle: "h23", timeZoneName: "short" });
  const utcFormat = new Intl.DateTimeFormat("en-GB", { hour: "2-digit", minute: "2-digit", hourCycle: "h23", timeZone: "UTC" });
  const pad = (value) => String(value).padStart(2, "0");

  const render = () => {
    const now = new Date();
    const localDate = `${now.getFullYear()}-${pad(now.getMonth() + 1)}-${pad(now.getDate())}`;

    dateElement.textContent = dateFormat.format(now);
    dateElement.dateTime = localDate;
    localElement.textContent = localFormat.format(now);
    localElement.dateTime = `${localDate}T${pad(now.getHours())}:${pad(now.getMinutes())}`;
    localElement.hidden = false;
    utcElement.textContent = `${utcFormat.format(now)} UTC`;
    utcElement.dateTime = now.toISOString().slice(0, 16) + "Z";
  };

  const scheduleNextMinute = () => {
    const now = new Date();
    const delay = (60 - now.getSeconds()) * 1000 - now.getMilliseconds();
    window.setTimeout(() => {
      render();
      scheduleNextMinute();
    }, delay + 50);
  };

  render();
  scheduleNextMinute();
})();
