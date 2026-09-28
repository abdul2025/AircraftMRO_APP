using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AircraftMRO.Tests.Support;
using AircraftMRO.Web.Features.WorkOrders;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AircraftMRO.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed class WorkOrderWebTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private WebHostFactory _factory = null!;
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _factory = new WebHostFactory(await fixture.CreateDatabaseAsync());
        _client = _factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Index_renders_from_feature_folder_with_navigation_and_live_refresh()
    {
        var html = await _client.GetStringAsync("/WorkOrders");

        Assert.Contains("No work orders have been raised yet.", html);
        Assert.Contains("href=\"/WorkOrders\"", html);
        Assert.Contains("aria-current=\"page\"", html);
        Assert.Contains("data-live-refresh=\"WorkOrder\"", html);
        Assert.Contains("id=\"work-order-priority-chart-title\"", html);
        Assert.Contains("No open work orders yet.", html);
    }

    [Fact]
    public async Task Critical_work_order_grounds_the_aircraft_until_it_is_completed()
    {
        var (aircraftId, registration) = await CreateAircraftAsync();

        var created = await PostFormAsync($"/WorkOrders/Create?aircraftId={aircraftId}", "/WorkOrders/Create",
            WorkOrderForm(aircraftId, "Critical"));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var detailsUrl = created.Headers.Location!.OriginalString;
        var details = await _client.GetStringAsync(detailsUrl);
        Assert.Contains("Work order created.", details);
        Assert.Matches(">WO-\\d{6}</h1>", details);
        Assert.Contains($">{registration}</a>", details);

        Assert.Contains(">Grounded<", await _client.GetStringAsync($"/Aircraft/Details/{aircraftId}"));

        // While it is open, the aircraft's status cannot be changed from its edit form.
        var blocked = await PostFormAsync($"/Aircraft/Edit/{aircraftId}", $"/Aircraft/Edit/{aircraftId}",
            WithRowVersion(AircraftForm(registration, "Active"), await _client.GetStringAsync($"/Aircraft/Edit/{aircraftId}")));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Contains("must stay grounded", WebUtility.HtmlDecode(await blocked.Content.ReadAsStringAsync()));

        // An open work order cannot be deleted: the delete page explains why and offers no delete button.
        var id = detailsUrl.Split('/').Last();
        Assert.DoesNotContain($"/WorkOrders/Delete/{id}", details);
        var deletePage = await _client.GetStringAsync($"/WorkOrders/Delete/{id}");
        Assert.Contains("Open work orders can't be deleted", WebUtility.HtmlDecode(deletePage));
        Assert.DoesNotContain("Delete work order</button>", deletePage);
        var forcedDelete = await PostFormAsync($"/WorkOrders/Delete/{id}", $"/WorkOrders/Delete/{id}",
            new Dictionary<string, string> { ["rowVersion"] = HiddenValue(deletePage, "rowVersion") });
        Assert.Equal(HttpStatusCode.Redirect, forcedDelete.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync(detailsUrl)).StatusCode);

        // Lowering the priority moves the aircraft to maintenance; completing the last one returns it to Active.
        Assert.Equal(HttpStatusCode.Redirect, (await EditWorkOrderAsync(id, aircraftId, "High", "InProgress")).StatusCode);
        Assert.Contains(">In maintenance<", await _client.GetStringAsync($"/Aircraft/Details/{aircraftId}"));
        Assert.Equal(HttpStatusCode.Redirect, (await EditWorkOrderAsync(id, aircraftId, "High", "Completed")).StatusCode);
        Assert.Contains(">Active<", await _client.GetStringAsync($"/Aircraft/Details/{aircraftId}"));
        Assert.Contains($"/WorkOrders/Delete/{id}", await _client.GetStringAsync(detailsUrl));
    }

    private async Task<HttpResponseMessage> EditWorkOrderAsync(string id, Guid aircraftId, string priority, string status)
    {
        var form = WorkOrderForm(aircraftId, priority);
        form["Status"] = status;
        form["RowVersion"] = HiddenValue(await _client.GetStringAsync($"/WorkOrders/Edit/{id}"), "RowVersion");
        return await PostFormAsync($"/WorkOrders/Edit/{id}", $"/WorkOrders/Edit/{id}", form);
    }

    [Fact]
    public async Task Index_shows_statistics_tiles_and_applies_filters()
    {
        var (aircraftId, registration) = await CreateAircraftAsync();
        foreach (var (title, priority) in new[] { ("Wing inspection", "Critical"), ("Seat cover", "Low"), ("Galley oven", "High") })
        {
            var form = WorkOrderForm(aircraftId, priority);
            form["Title"] = title;
            Assert.Equal(HttpStatusCode.Redirect, (await PostFormAsync("/WorkOrders/Create", "/WorkOrders/Create", form)).StatusCode);
        }

        var all = await _client.GetStringAsync("/WorkOrders");
        Assert.Matches(@"Total work orders</span>\s*<span class=""stat-tile__value"">3<", all);
        Assert.Matches(@"Open critical\s*</span>\s*<span class=""stat-tile__value"">1<", all);
        Assert.Contains("href=\"/WorkOrders?priority=Critical&amp;open=true\"", all);
        Assert.Contains("Critical 1 (33%), High 1 (33%), Medium 0 (0%), Low 1 (33%)", all);
        Assert.Contains("data-tooltip-value=\"1 open work order · 33%\"", WebUtility.HtmlDecode(all));

        static string Row(string title) => $"<td>{title}</td>";

        var critical = await _client.GetStringAsync("/WorkOrders?priority=Critical&open=true");
        Assert.Contains(Row("Wing inspection"), critical);
        Assert.DoesNotContain(Row("Seat cover"), critical);
        Assert.Contains("donut--has-selection", critical);
        Assert.Contains("Showing 1 of 3 work orders · open only", WebUtility.HtmlDecode(critical));

        var searched = await _client.GetStringAsync($"/WorkOrders?search=galley");
        Assert.Contains(Row("Galley oven"), searched);
        Assert.DoesNotContain(Row("Wing inspection"), searched);

        var forAircraft = await _client.GetStringAsync($"/WorkOrders?aircraftId={aircraftId}");
        Assert.Contains($"Work orders for {registration}", forAircraft);

        Assert.Contains("No work orders match these filters.", await _client.GetStringAsync("/WorkOrders?search=no-such-work"));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/WorkOrders?status=Nope&priority=Nope&open=maybe")).StatusCode);
    }

    [Fact]
    public async Task Modal_create_validates_then_returns_json_redirect_and_aircraft_page_links_here()
    {
        var (aircraftId, _) = await CreateAircraftAsync();
        Assert.Contains($"/WorkOrders/Create?aircraftId={aircraftId}", await _client.GetStringAsync($"/Aircraft/Details/{aircraftId}"));

        var fragment = await ModalGetAsync($"/WorkOrders/Create?aircraftId={aircraftId}");
        var html = await fragment.Content.ReadAsStringAsync();
        Assert.Contains("data-modal-form", html);
        Assert.DoesNotContain("<html", html);
        Assert.Contains($"value=\"{aircraftId}\" selected", html);

        var missingAircraft = WorkOrderForm(aircraftId, "Low");
        missingAircraft.Remove("AircraftId");
        var invalid = await ModalPostAsync("/WorkOrders/Create", html, missingAircraft);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        Assert.Contains("Select an aircraft.", await invalid.Content.ReadAsStringAsync());

        var created = await ModalPostAsync("/WorkOrders/Create", html, WorkOrderForm(aircraftId, "Low"), returnUrl: $"/Aircraft/Details/{aircraftId}");
        Assert.Equal($"/Aircraft/Details/{aircraftId}", await ReadRedirectUrlAsync(created));
        Assert.Contains(">In maintenance<", await _client.GetStringAsync($"/Aircraft/Details/{aircraftId}"));
    }

    [Fact]
    public async Task Aircraft_with_open_work_cannot_be_deleted_and_retired_aircraft_cannot_get_new_work()
    {
        var (aircraftId, _) = await CreateAircraftAsync();
        await PostFormAsync("/WorkOrders/Create", "/WorkOrders/Create", WorkOrderForm(aircraftId, "Low"));

        var deletePage = await _client.GetStringAsync($"/Aircraft/Delete/{aircraftId}");
        var deleted = await PostFormAsync($"/Aircraft/Delete/{aircraftId}", $"/Aircraft/Delete/{aircraftId}",
            new Dictionary<string, string> { ["rowVersion"] = HiddenValue(deletePage, "rowVersion") });
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.Contains("open work orders", WebUtility.HtmlDecode(await _client.GetStringAsync(deleted.Headers.Location!.OriginalString)));
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync($"/Aircraft/Details/{aircraftId}")).StatusCode);

        var (retiredId, retiredRegistration) = await CreateAircraftAsync("Retired");
        Assert.DoesNotContain($"value=\"{retiredId}\"", await _client.GetStringAsync("/WorkOrders/Create"));
        var rejected = await PostFormAsync("/WorkOrders/Create", "/WorkOrders/Create", WorkOrderForm(retiredId, "Low"));
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("retired aircraft", await rejected.Content.ReadAsStringAsync());
        Assert.DoesNotContain($"/WorkOrders/Create?aircraftId={retiredId}", await _client.GetStringAsync($"/Aircraft/Details/{retiredId}"));
        Assert.NotNull(retiredRegistration);
    }

    [Fact]
    public async Task Closed_work_orders_hide_edit_and_reject_a_forced_edit()
    {
        var (aircraftId, _) = await CreateAircraftAsync();
        var created = await PostFormAsync("/WorkOrders/Create", "/WorkOrders/Create", WorkOrderForm(aircraftId, "Low"));
        var id = created.Headers.Location!.OriginalString.Split('/').Last();
        var editPage = await _client.GetStringAsync($"/WorkOrders/Edit/{id}");
        var cancel = WorkOrderForm(aircraftId, "Low");
        cancel["Status"] = "Cancelled";
        cancel["RowVersion"] = HiddenValue(editPage, "RowVersion");
        await PostFormAsync($"/WorkOrders/Edit/{id}", $"/WorkOrders/Edit/{id}", cancel);

        var details = await _client.GetStringAsync($"/WorkOrders/Details/{id}");
        Assert.DoesNotContain($"/WorkOrders/Edit/{id}", details);

        var reopen = WorkOrderForm(aircraftId, "Critical");
        reopen["RowVersion"] = HiddenValue(await _client.GetStringAsync($"/WorkOrders/Edit/{id}"), "RowVersion");
        var rejected = await PostFormAsync($"/WorkOrders/Edit/{id}", $"/WorkOrders/Edit/{id}", reopen);
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Contains("closed and can no longer be changed", await rejected.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Edit_failures_keep_display_values_and_report_stale_versions()
    {
        var (aircraftId, registration) = await CreateAircraftAsync();
        var created = await PostFormAsync("/WorkOrders/Create", "/WorkOrders/Create", WorkOrderForm(aircraftId, "Low"));
        var id = created.Headers.Location!.OriginalString.Split('/').Last();
        var editHtml = await (await ModalGetAsync($"/WorkOrders/Edit/{id}")).Content.ReadAsStringAsync();
        var number = Regex.Match(editHtml, "Edit (WO-\\d{6})").Groups[1].Value;
        Assert.NotEmpty(number);

        // Validation failure: the number and aircraft are not posted, yet the form still shows them.
        var blank = WorkOrderForm(aircraftId, "Low");
        blank["Title"] = "";
        blank["RowVersion"] = HiddenValue(editHtml, "RowVersion");
        var invalid = await ModalPostAsync($"/WorkOrders/Edit/{id}", editHtml, blank);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalid.StatusCode);
        var invalidHtml = await invalid.Content.ReadAsStringAsync();
        Assert.Contains($"Edit {number}", invalidHtml);
        Assert.Contains(registration, invalidHtml);
        Assert.Contains("input-validation-error", invalidHtml);

        // Someone else saves first; the original form is now stale.
        var first = WorkOrderForm(aircraftId, "High");
        first["Status"] = "InProgress";
        first["RowVersion"] = HiddenValue(editHtml, "RowVersion");
        Assert.Equal(HttpStatusCode.OK, (await ModalPostAsync($"/WorkOrders/Edit/{id}", editHtml, first)).StatusCode);

        var stale = WorkOrderForm(aircraftId, "Critical");
        stale["RowVersion"] = HiddenValue(editHtml, "RowVersion");
        var conflict = await ModalPostAsync($"/WorkOrders/Edit/{id}", editHtml, stale);
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        var conflictHtml = WebUtility.HtmlDecode(await conflict.Content.ReadAsStringAsync());
        Assert.Contains("This work order was changed by someone else", conflictHtml);
        Assert.Contains($"Edit {number}", conflictHtml);
        // The stale save changed nothing: the aircraft reflects the first save (High), not Critical.
        Assert.Contains(">In maintenance<", await _client.GetStringAsync($"/Aircraft/Details/{aircraftId}"));
    }

    [Fact]
    public async Task Work_order_notifications_link_to_the_work_orders_details_page()
    {
        var (aircraftId, _) = await CreateAircraftAsync();
        var created = await PostFormAsync("/WorkOrders/Create", "/WorkOrders/Create", WorkOrderForm(aircraftId, "Low"));
        var id = created.Headers.Location!.OriginalString.Split('/').Last();

        using var catchUp = JsonDocument.Parse(await _client.GetStringAsync("/Notifications/Since?afterId=0"));
        var workOrderMessage = catchUp.RootElement.EnumerateArray()
            .Single(message => message.GetProperty("entityType").GetString() == "WorkOrder");
        Assert.Equal($"/WorkOrders/Details/{id}", workOrderMessage.GetProperty("url").GetString());

        var aircraftMessage = catchUp.RootElement.EnumerateArray()
            .First(message => message.GetProperty("entityType").GetString() == "Aircraft");
        Assert.Equal($"/Aircraft/Details/{aircraftId}", aircraftMessage.GetProperty("url").GetString());

        var history = await _client.GetStringAsync("/Notifications");
        Assert.Contains($"href=\"/WorkOrders/Details/{id}\"", history);
        Assert.DoesNotContain("/WorkOrder/Details/", history);
    }

    private async Task<(Guid Id, string Registration)> CreateAircraftAsync(string status = "Active")
    {
        var registration = AircraftTestData.UniqueRegistration();
        var response = await PostFormAsync("/Aircraft/Create", "/Aircraft/Create", AircraftForm(registration, status));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        return (Guid.Parse(response.Headers.Location!.OriginalString.Split('/').Last()), registration);
    }

    private static Dictionary<string, string> AircraftForm(string registration, string status) => new()
    {
        ["RegistrationNumber"] = registration,
        ["Manufacturer"] = "Airbus",
        ["Model"] = "A320-214",
        ["SerialNumber"] = registration,
        ["YearOfManufacture"] = "2012",
        ["TotalFlightHours"] = "31250.5",
        ["Status"] = status
    };

    private static Dictionary<string, string> WorkOrderForm(Guid aircraftId, string priority) => new()
    {
        ["AircraftId"] = aircraftId.ToString(),
        ["Title"] = "Replace brake assembly",
        ["Description"] = "Left main gear.",
        ["Priority"] = priority,
        ["Status"] = "Open",
        ["DueDate"] = "2026-10-15"
    };

    private static Dictionary<string, string> WithRowVersion(Dictionary<string, string> form, string editPage)
    {
        form["RowVersion"] = HiddenValue(editPage, "RowVersion");
        return form;
    }

    private Task<HttpResponseMessage> ModalGetAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(WorkOrdersController.ModalRequestHeader, "true");
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> ModalPostAsync(
        string url,
        string formHtml,
        Dictionary<string, string> form,
        string? returnUrl = "/WorkOrders")
    {
        form["__RequestVerificationToken"] = HiddenValue(formHtml, "__RequestVerificationToken");
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Add(WorkOrdersController.ModalRequestHeader, "true");
        if (returnUrl is not null)
        {
            request.Headers.Add(WorkOrdersController.ModalReturnUrlHeader, returnUrl);
        }

        return _client.SendAsync(request);
    }

    private static async Task<string> ReadRedirectUrlAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("redirectUrl").GetString()!;
    }

    private async Task<HttpResponseMessage> PostFormAsync(string formPageUrl, string postUrl, Dictionary<string, string> form)
    {
        var page = await _client.GetStringAsync(formPageUrl);
        form["__RequestVerificationToken"] = HiddenValue(page, "__RequestVerificationToken");
        return await _client.PostAsync(postUrl, new FormUrlEncodedContent(form));
    }

    private static string HiddenValue(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"|value=\"([^\"]*)\"[^>]*name=\"{Regex.Escape(name)}\"");
        Assert.True(match.Success, $"Hidden input '{name}' not found.");
        return WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
    }
}
