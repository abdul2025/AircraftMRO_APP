// Opens links marked [data-modal] in the shared #app-modal and submits their forms in place.
// The server returns form HTML (initial load or validation errors) or JSON { redirectUrl } on success.
// Without JavaScript the same links open full pages, so this is a progressive enhancement.
(() => {
  "use strict";

  const modalElement = document.getElementById("app-modal");
  if (!modalElement || !window.bootstrap) {
    return;
  }

  const content = modalElement.querySelector(".modal-content");
  const modal = bootstrap.Modal.getOrCreateInstance(modalElement);
  const loadError = "This form could not be loaded. Refresh the page and try again.";
  const saveError = "Your changes could not be saved. Check your connection and try again.";

  const modalHeaders = () => ({
    "X-Modal-Request": "true",
    "X-Modal-Return-Url": window.location.pathname + window.location.search
  });

  const render = (html) => {
    content.innerHTML = html;
    const title = content.querySelector(".modal-title");
    if (title?.id) {
      modalElement.setAttribute("aria-labelledby", title.id);
    }
  };

  const renderError = (message) => {
    const alert = document.createElement("p");
    alert.className = "notice notice--danger";
    alert.setAttribute("role", "alert");
    alert.textContent = message;

    const body = content.querySelector(".modal-body");
    if (body) {
      body.prepend(alert);
      return;
    }

    content.replaceChildren();
    const wrapper = document.createElement("div");
    wrapper.className = "modal-body";
    const close = document.createElement("button");
    close.type = "button";
    close.className = "button button--secondary";
    close.dataset.bsDismiss = "modal";
    close.textContent = "Close";
    wrapper.append(alert, close);
    content.append(wrapper);
  };

  const focusFirstField = () => {
    const target = content.querySelector(".input-validation-error, [aria-invalid='true']")
      ?? content.querySelector(".modal-body input:not([type='hidden']), .modal-body select")
      ?? content.querySelector("[type='submit']");
    target?.focus();
  };

  document.addEventListener("click", async (event) => {
    const link = event.target.closest("a[data-modal]");
    if (!link || event.defaultPrevented || event.button !== 0 || event.ctrlKey || event.metaKey || event.shiftKey) {
      return;
    }

    event.preventDefault();
    try {
      const response = await fetch(link.href, { headers: modalHeaders() });
      if (!response.ok) {
        throw new Error(String(response.status));
      }

      render(await response.text());
    } catch {
      render("");
      renderError(loadError);
    }

    modal.show();
  });

  modalElement.addEventListener("shown.bs.modal", focusFirstField);
  modalElement.addEventListener("hidden.bs.modal", () => content.replaceChildren());

  content.addEventListener("submit", async (event) => {
    const form = event.target.closest("form[data-modal-form]");
    if (!form) {
      return;
    }

    event.preventDefault();
    const submit = form.querySelector("[type='submit']");
    submit?.setAttribute("disabled", "");
    submit?.setAttribute("aria-busy", "true");

    try {
      const response = await fetch(form.action, {
        method: "POST",
        headers: modalHeaders(),
        body: new FormData(form)
      });
      const contentType = response.headers.get("content-type") ?? "";

      if (response.ok && contentType.includes("application/json")) {
        const { redirectUrl } = await response.json();
        window.location.assign(redirectUrl);
        return;
      }

      if (contentType.includes("text/html")) {
        render(await response.text());
        focusFirstField();
        return;
      }

      throw new Error(String(response.status));
    } catch {
      renderError(saveError);
      submit?.removeAttribute("disabled");
      submit?.removeAttribute("aria-busy");
    }
  });
})();
