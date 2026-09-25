// Hover/focus tooltip for chart marks marked [data-chart-tooltip].
// Tooltips only enhance: every value is also in the chart's legend table.
(() => {
  "use strict";

  document.querySelectorAll(".chart-card").forEach((card) => {
    const tooltip = card.querySelector(".chart-tooltip");
    if (!tooltip) {
      return;
    }

    const valueElement = tooltip.querySelector(".chart-tooltip__value");
    const labelElement = tooltip.querySelector(".chart-tooltip__label");

    const show = (mark, clientX, clientY) => {
      // Labels are data: always textContent, never innerHTML.
      valueElement.textContent = mark.dataset.tooltipValue ?? "";
      labelElement.textContent = mark.dataset.tooltipLabel ?? "";
      tooltip.hidden = false;

      const cardBox = card.getBoundingClientRect();
      const tipBox = tooltip.getBoundingClientRect();
      const left = Math.min(Math.max(clientX - cardBox.left + 12, 8), cardBox.width - tipBox.width - 8);
      const top = Math.max(clientY - cardBox.top - tipBox.height - 12, 8);
      tooltip.style.left = `${left}px`;
      tooltip.style.top = `${top}px`;
    };

    const hide = () => {
      tooltip.hidden = true;
    };

    card.querySelectorAll("[data-chart-tooltip]").forEach((mark) => {
      mark.addEventListener("pointermove", (event) => show(mark, event.clientX, event.clientY));
      mark.addEventListener("pointerleave", hide);
      mark.addEventListener("focus", () => {
        const box = mark.getBoundingClientRect();
        show(mark, box.left + box.width / 2, box.top + box.height / 2);
      });
      mark.addEventListener("blur", hide);
    });

    card.addEventListener("keydown", (event) => {
      if (event.key === "Escape") {
        hide();
      }
    });
  });
})();
