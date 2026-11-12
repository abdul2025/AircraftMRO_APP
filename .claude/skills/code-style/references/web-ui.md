# Web UI conventions (Razor MVC host)

Views live beside their controller in `Features/{Feature}/Views/`, found by
`FeatureViewLocationExpander`. Cross-feature pieces live in `Views/Shared/` and `Models/`.
UI framework: Bootstrap (vendored with libman) + one `site.css` with BEM-style classes.
JavaScript is progressive enhancement only — every page and form works without it.

## Files per feature

| File | Model | Purpose |
| --- | --- | --- |
| `_ViewImports.cshtml` | — | `@using` the feature's Models, Application DTOs, Domain enums |
| `Index.cshtml` | `{Entity}ListViewModel` | heading + New button, messages, statistics, filter bar, table, pager |
| `Details.cshtml` | `{Entity}DetailsViewModel` | heading + actions, summary, audit panel, back link |
| `Create.cshtml` / `Edit.cshtml` / `Delete.cshtml` | form VM / details VM | full-page fallback wrapping the matching `_…Form` partial |
| `_CreateForm` / `_EditForm` / `_DeleteForm` | same | the real forms, written as modal markup |
| `_{Entity}Form.cshtml` | form VM | fields shared by create and edit |
| `_{Entity}Summary.cshtml` | details VM | `<dl class="detail-list">` used by Details and Delete |
| `_{Entity}StatusBadge.cshtml` | enum | label + CSS modifier from `{Entity}Labels` |
| `_{Entity}Actions.cshtml` | small actions VM | edit / delete icon links (`data-modal`), shown per business rule |
| `_{Entity}Statistics.cshtml` | list VM | optional stat tiles linking to facets |

`ModalFormController.FormView(action, …)` renders `_{action}Form` for modal requests and
`{action}` otherwise, so both files must exist for Create, Edit and Delete (and any extra form action).

## Full-page wrapper

```cshtml
@model {Entity}FormViewModel
@{ ViewData["Title"] = "New record"; }

@* Full-page fallback when JavaScript is unavailable; the same form opens in a modal otherwise. *@
<section class="form-card form-card--page" aria-labelledby="{entity}-form-title">
    <partial name="_CreateForm" model="Model" />
</section>
```

## Modal form

```cshtml
@model {Entity}FormViewModel

<form asp-action="Create" method="post" novalidate data-modal-form>
    <div class="modal-header">
        <div>
            <p class="eyebrow">{Feature section}</p>
            <h2 class="modal-title" id="{entity}-form-title">New record</h2>
        </div>
        <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button>
    </div>
    <div class="modal-body">
        <partial name="_{Entity}Form" model="Model" />
    </div>
    <div class="modal-footer">
        <a class="button button--secondary" asp-action="Index" data-bs-dismiss="modal">Cancel</a>
        <button type="submit" class="button button--primary">Save</button>
    </div>
</form>
```

- `_EditForm`: `asp-action="Edit" asp-route-id="@ViewContext.RouteData.Values["id"]"`, Cancel → Details.
- `_DeleteForm`: hidden `<input type="hidden" name="rowVersion" value="@Model.RowVersion" />`, `_StatusMessages` (shows the 409 reason on re-render), `_{Entity}Summary`; when a rule forbids deletion, explain it in `notice notice--danger` and omit the submit button.
- `novalidate`: the server is the source of truth; errors return as re-rendered HTML.
- Antiforgery is a global filter; tag helpers emit the token.

## Shared fields partial

```cshtml
@model {Entity}FormViewModel

<div asp-validation-summary="ModelOnly" class="notice notice--danger" role="alert"></div>
<input type="hidden" asp-for="RowVersion" />

<div class="form-grid">
    <div class="form-field">
        <label asp-for="Code"></label>
        <input asp-for="Code" autocomplete="off" />
        <span asp-validation-for="Code" class="field-error"></span>
    </div>
    <div class="form-field">
        <label asp-for="Status"></label>
        <select asp-for="Status" asp-items="{Entity}Labels.StatusOptions()"></select>
        <span asp-validation-for="Status" class="field-error"></span>
    </div>
    <div class="form-field form-field--full">
        <label asp-for="Description"></label>
        <textarea asp-for="Description" rows="4" aria-describedby="description-hint"></textarea>
        <span id="description-hint" class="field-hint">Explain the consequence of the field, not its format.</span>
        <span asp-validation-for="Description" class="field-error"></span>
    </div>
</div>
```

## List page

```cshtml
@model {Entity}ListViewModel
@{
    ViewData["Title"] = "{Feature title}";
    var results = Model.Page;
    var filter = Model.Filter;
}

<section aria-labelledby="{feature}-title" data-live-refresh="{Entity}">
    <div class="section-heading">
        <div>
            <p class="eyebrow">{Section}</p>
            <h1 id="{feature}-title">{Feature title}</h1>
        </div>
        <a class="button button--primary" asp-action="Create" data-modal>New record</a>
    </div>

    <partial name="_StatusMessages" />
    <partial name="_{Entity}Statistics" model="Model" />

    <form class="filter-bar" asp-action="Index" method="get" role="search" aria-label="Filter records">
        <div class="filter-bar__field filter-bar__field--search">
            <label for="{entity}-search">Search</label>
            <input id="{entity}-search" type="search" name="search" value="@filter.Search"
                   maxlength="@{Entity}ListFilter.MaxSearchLength" autocomplete="off" />
        </div>
        <div class="filter-bar__field">
            <label for="{entity}-status">Status</label>
            <select id="{entity}-status" name="status" data-auto-submit>
                <option value="">All statuses</option>
                @foreach (var option in {Entity}Labels.StatusOptions())
                {
                    <option value="@option.Value" selected="@(filter.Status?.ToString() == option.Value)">@option.Text</option>
                }
            </select>
        </div>
        <div class="filter-bar__actions">
            <button type="submit" class="button button--primary">Apply</button>
            @if (!filter.IsEmpty)
            {
                <a class="button button--secondary" asp-action="Index">Clear</a>
            }
        </div>
    </form>

    @if (Model.Statistics.TotalCount == 0)
    {
        <p class="empty-state">No records yet.</p>
    }
    else if (results.TotalCount == 0)
    {
        <p class="empty-state">No records match these filters. <a asp-action="Index">Clear filters</a></p>
    }
    else
    {
        <div class="table-card">
            <table class="data-table">
                <thead>
                    <tr>
                        <th scope="col">Code</th>
                        <th scope="col">Name</th>
                        <th scope="col">Status</th>
                        <th scope="col"><span class="visually-hidden">Actions</span></th>
                    </tr>
                </thead>
                <tbody>
                    @foreach (var item in results.Items)
                    {
                        <tr>
                            <th scope="row"><a asp-action="Details" asp-route-id="@item.Id">@item.Code</a></th>
                            <td>@item.Name</td>
                            <td><partial name="_{Entity}StatusBadge" model="item.Status" /></td>
                            <td class="actions-cell"><partial name="_{Entity}Actions" model="…" /></td>
                        </tr>
                    }
                </tbody>
            </table>
        </div>

        @if (results.TotalPages > 1)
        {
            <nav class="pager" aria-label="Pages">
                @if (results.HasPreviousPage)
                {
                    <a asp-action="Index" asp-all-route-data="Model.RouteValues(page: results.Page - 1)">Previous</a>
                }
                <span>Page @results.Page of @results.TotalPages (@results.TotalCount records)</span>
                @if (results.HasNextPage)
                {
                    <a asp-action="Index" asp-all-route-data="Model.RouteValues(page: results.Page + 1)">Next</a>
                }
            </nav>
        }
    }
</section>

@section Scripts {
    <script>
        // Apply a select filter as soon as it changes; the Apply button covers no-JS use.
        // Delegated, so it keeps working after the page content is swapped.
        document.addEventListener("change", (event) => {
            if (event.target.matches("[data-auto-submit]")) {
                event.target.form.requestSubmit();
            }
        });
    </script>
}
```

Three distinct empty states: nothing exists · nothing matches the filters · the table.

## Details page

`<section class="form-card" aria-labelledby="details-title" data-live-refresh="{Entity}" data-live-refresh-id="@Model.Id" data-live-refresh-gone="@Url.Action("Index")">`
→ heading with `DisplayName` + `_{Entity}Actions` → `_StatusMessages` → `_{Entity}Summary` →
audit panel (Created / Created by / Last updated / Last updated by / Record ID, using
`<time datetime="…">`) → `form-actions` with "Back to list".

## Behaviour hooks (data attributes, no inline handlers)

| Attribute | On | Effect |
| --- | --- | --- |
| `data-modal` | `<a>` to a form action | loads it into `#app-modal` (modal-forms.js); plain link without JS |
| `data-modal-form` | `<form>` inside the modal | AJAX submit; HTML → re-render, JSON `{redirectUrl}` → navigate |
| `data-auto-submit` | filter `<select>` | submits its form on change |
| `data-live-refresh="{Entity}"` | page section | refreshes when a change notification for that type arrives (pattern 9, optional) |
| `data-live-refresh-id` / `data-live-refresh-gone` | details section | only this id / where to go on 404 |

Modal requests send `X-Modal-Request: true` and `X-Modal-Return-Url`; `ModalReturnUrl`
accepts only local URLs and returns the user to the page the modal was opened from.

## Feedback and status codes

- Success: `Saved("… created.", url)` → `TempData["StatusMessage"]` → `notice notice--success` `role="status"`.
- Delete failure: `TempData["ErrorMessage"]` → `notice notice--danger` `role="alert"`.
- Re-rendered forms: 422 validation, 409 conflict / business rule; modal-forms.js renders any HTML response.

## Labels, CSS, accessibility

- All enum → label / CSS modifier / select options mapping in `{Entity}Labels`; views never call `ToString()` on an enum.
- CSS naming: block + `--modifier` (`button--primary`, `status-badge--success`, `notice--danger`, `form-field--full`). Modifier vocabulary: `success | warning | danger | info | active | neutral`.
- Every section `aria-labelledby` its heading; icon-only buttons have `aria-label` and `title`; tables use `<th scope>`; messages use `role="status"` / `role="alert"`; hidden text with `visually-hidden`.
- Machine-readable dates in `<time datetime>`; display formatting helpers in `Web/Models` (`AuditDisplay`, `NumberDisplay`, `MoneyDisplay`) use invariant culture.
- Navigation: one `sidebar-nav__link` per feature in `_Layout`, with `is-active` and `aria-current="page"` when the controller matches.
- Scripts: vanilla JS IIFEs with `"use strict"` in `wwwroot/js/`, loaded with `asp-append-version="true"`; third-party libraries vendored under `wwwroot/lib` via `libman.json`.
