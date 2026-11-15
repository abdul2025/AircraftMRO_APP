// Dependent selects: <select data-depends-on="ParentId"> shows only the options whose
// data-parent matches the parent select's value, and clears a selection that no longer fits.
// The server renders the initial state, so this only reacts to changes. Delegated, so it also
// covers forms loaded into a modal or swapped in by live-refresh.js.
(() => {
  "use strict";

  document.addEventListener("change", (event) => {
    const parent = event.target;
    if (!(parent instanceof HTMLSelectElement) || !parent.id) {
      return;
    }

    const scope = parent.form ?? document;
    scope.querySelectorAll(`select[data-depends-on="${CSS.escape(parent.id)}"]`).forEach((child) => {
      for (const option of child.options) {
        if (option.dataset.parent === undefined) {
          continue;
        }

        const hide = parent.value !== "" && option.dataset.parent !== parent.value;
        option.hidden = hide;
        option.disabled = hide;
      }

      if (child.selectedOptions[0]?.disabled) {
        child.value = "";
      }
    });
  });
})();
