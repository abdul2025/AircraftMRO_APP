using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AircraftMRO.Tests.Support;
using AircraftMRO.Web.Features.Aircraft;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace AircraftMRO.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed partial class AircraftWebTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private const string ModalRequestHeader = AircraftController.ModalRequestHeader;
    private const string ModalReturnUrlHeader = AircraftController.ModalReturnUrlHeader;
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
    public async Task Index_renders_from_feature_folder()
    {
        var response = await _client.GetAsync("/Aircraft");

        response.EnsureSuccessStatusCode();
        Assert.Contains("No aircraft have been added yet.", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Details_for_unknown_id_returns_404()
    {
        var response = await _client.GetAsync($"/Aircraft/Details/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_without_antiforgery_token_is_rejected()
    {
        var response = await _client.PostAsync("/Aircraft/Create", new FormUrlEncodedContent(ValidForm("HZ-NOT")));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Create_edit_and_delete_through_the_ui()
    {
        var registration = AircraftTestData.UniqueRegistration();
        var created = await PostFormAsync("/Aircraft/Create", "/Aircraft/Create", ValidForm(registration));
        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var detailsUrl = created.Headers.Location!.OriginalString;

        var details = await _client.GetStringAsync(detailsUrl);
        Assert.Contains(registration, details);
        Assert.Contains("Aircraft created.", details);
        Assert.Contains("Audit trail", details);
        Assert.Contains("Never updated", details);
        Assert.Contains("Not recorded", details);
        Assert.Matches("<time datetime=\"\\d{4}-\\d{2}-\\d{2}T\\d{2}:\\d{2}:\\d{2}Z\">", details);

        var id = detailsUrl.Split('/').Last();
        var editPage = await _client.GetStringAsync($"/Aircraft/Edit/{id}");
        var form = ValidForm(registration);
        form["Status"] = "Grounded";
        form["RowVersion"] = HiddenValue(editPage, "RowVersion");
        var edited = await PostFormAsync($"/Aircraft/Edit/{id}", $"/Aircraft/Edit/{id}", form);
        Assert.Equal(HttpStatusCode.Redirect, edited.StatusCode);
        var editedDetails = await _client.GetStringAsync(detailsUrl);
        Assert.Contains("Grounded", editedDetails);
        Assert.DoesNotContain("Never updated", editedDetails);
        Assert.Equal(2, Regex.Matches(editedDetails, "<time datetime=").Count);

        var stale = await PostFormAsync($"/Aircraft/Edit/{id}", $"/Aircraft/Edit/{id}", form);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Contains("changed by someone else", WebUtility.HtmlDecode(await stale.Content.ReadAsStringAsync()));

        var deletePage = await _client.GetStringAsync($"/Aircraft/Delete/{id}");
        var deleted = await PostFormAsync(
            $"/Aircraft/Delete/{id}",
            $"/Aircraft/Delete/{id}",
            new Dictionary<string, string> { ["rowVersion"] = HiddenValue(deletePage, "rowVersion") });
        Assert.Equal(HttpStatusCode.Redirect, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(detailsUrl)).StatusCode);
    }

    [Fact]
    public async Task Duplicate_registration_shows_field_error()
    {
        var registration = AircraftTestData.UniqueRegistration();
        await PostFormAsync("/Aircraft/Create", "/Aircraft/Create", ValidForm(registration, "S-1"));

        var duplicate = await PostFormAsync("/Aircraft/Create", "/Aircraft/Create", ValidForm(registration, "S-2"));

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Contains("already exists", await duplicate.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Decimal_flight_hours_bind_when_server_culture_uses_comma_separator()
    {
        // Flow the test's culture into the request, as a host running under de-DE would have.
        using var factory = _factory.WithWebHostBuilder(builder => builder.ConfigureServices(services =>
            services.Configure<TestServerOptions>(options => options.PreserveExecutionContext = true)));
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var originalCulture = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var created = await PostFormAsync(client, "/Aircraft/Create", "/Aircraft/Create", ValidForm(AircraftTestData.UniqueRegistration()));

            Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
            Assert.Contains("31,250.5", await client.GetStringAsync(created.Headers.Location!.OriginalString));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public async Task Modal_create_returns_form_fragment_then_json_redirect_to_the_opening_page()
    {
        var fragment = await ModalGetAsync("/Aircraft/Create");
        Assert.Equal(HttpStatusCode.OK, fragment.StatusCode);
        var html = await fragment.Content.ReadAsStringAsync();
        Assert.Contains("data-modal-form", html);
        Assert.DoesNotContain("<html", html);

        var created = await ModalPostAsync("/Aircraft/Create", html, ValidForm(AircraftTestData.UniqueRegistration()), returnUrl: "/Aircraft?page=2");

        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        Assert.Equal("/Aircraft?page=2", await ReadRedirectUrlAsync(created));
        Assert.Contains("Aircraft created.", await _client.GetStringAsync("/Aircraft?page=2"));
    }

    [Fact]
    public async Task Modal_redirect_ignores_non_local_return_url()
    {
        var html = await (await ModalGetAsync("/Aircraft/Create")).Content.ReadAsStringAsync();

        var created = await ModalPostAsync("/Aircraft/Create", html, ValidForm(AircraftTestData.UniqueRegistration()), returnUrl: "https://evil.example/phish");

        Assert.StartsWith("/Aircraft/Details/", await ReadRedirectUrlAsync(created));
    }

    [Fact]
    public async Task Modal_validation_errors_return_form_fragment_with_422()
    {
        var html = await (await ModalGetAsync("/Aircraft/Create")).Content.ReadAsStringAsync();
        var form = ValidForm(AircraftTestData.UniqueRegistration());
        form["Manufacturer"] = "";

        var response = await ModalPostAsync("/Aircraft/Create", html, form);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var fragment = await response.Content.ReadAsStringAsync();
        Assert.Contains("data-modal-form", fragment);
        Assert.Contains("input-validation-error", fragment);
        Assert.DoesNotContain("<html", fragment);
    }

    [Fact]
    public async Task Modal_edit_and_stale_delete_stay_in_the_modal()
    {
        var registration = AircraftTestData.UniqueRegistration();
        var createHtml = await (await ModalGetAsync("/Aircraft/Create")).Content.ReadAsStringAsync();
        var detailsUrl = await ReadRedirectUrlAsync(await ModalPostAsync("/Aircraft/Create", createHtml, ValidForm(registration), returnUrl: null));
        var id = detailsUrl.Split('/').Last();

        var deleteHtml = await (await ModalGetAsync($"/Aircraft/Delete/{id}")).Content.ReadAsStringAsync();
        var staleRowVersion = HiddenValue(deleteHtml, "rowVersion");

        var editHtml = await (await ModalGetAsync($"/Aircraft/Edit/{id}")).Content.ReadAsStringAsync();
        var form = ValidForm(registration);
        form["Status"] = "Retired";
        form["RowVersion"] = HiddenValue(editHtml, "RowVersion");
        var edited = await ModalPostAsync($"/Aircraft/Edit/{id}", editHtml, form, returnUrl: "/Aircraft");
        Assert.Equal("/Aircraft", await ReadRedirectUrlAsync(edited));

        var staleDelete = await ModalPostAsync(
            $"/Aircraft/Delete/{id}", deleteHtml, new Dictionary<string, string> { ["rowVersion"] = staleRowVersion });
        Assert.Equal(HttpStatusCode.Conflict, staleDelete.StatusCode);
        var conflictHtml = await staleDelete.Content.ReadAsStringAsync();
        Assert.Contains("changed by someone else", WebUtility.HtmlDecode(conflictHtml));
        Assert.Contains("Retired", conflictHtml);

        var deleted = await ModalPostAsync(
            $"/Aircraft/Delete/{id}", conflictHtml, new Dictionary<string, string> { ["rowVersion"] = HiddenValue(conflictHtml, "rowVersion") },
            returnUrl: detailsUrl);
        Assert.Equal("/Aircraft", await ReadRedirectUrlAsync(deleted));
        Assert.Equal(HttpStatusCode.NotFound, (await _client.GetAsync(detailsUrl)).StatusCode);
    }

    [Fact]
    public async Task Pages_link_create_edit_and_delete_to_the_modal()
    {
        var html = await (await ModalGetAsync("/Aircraft/Create")).Content.ReadAsStringAsync();
        var detailsUrl = await ReadRedirectUrlAsync(await ModalPostAsync("/Aircraft/Create", html, ValidForm(AircraftTestData.UniqueRegistration()), returnUrl: null));

        Assert.Equal(3, Regex.Matches(await _client.GetStringAsync("/Aircraft"), "data-modal[ >]").Count);
        Assert.Equal(2, Regex.Matches(await _client.GetStringAsync(detailsUrl), "data-modal[ >]").Count);
        Assert.Contains("id=\"app-modal\"", await _client.GetStringAsync("/Aircraft"));
    }

    [Fact]
    public async Task Index_shows_statistics_and_applies_filters()
    {
        foreach (var (registration, status) in new[] { ("WF-A1", "Active"), ("WF-A2", "Active"), ("WF-G1", "Grounded") })
        {
            var form = ValidForm(registration, registration);
            form["Status"] = status;
            Assert.Equal(HttpStatusCode.Redirect, (await PostFormAsync("/Aircraft/Create", "/Aircraft/Create", form)).StatusCode);
        }

        var all = await _client.GetStringAsync("/Aircraft");
        Assert.Matches(@"Total aircraft</span>\s*<span class=""stat-tile__value"">3<", all);
        Assert.Matches(@"Grounded\s*</span>\s*<span class=""stat-tile__value"">1<", all);
        Assert.Contains("href=\"/Aircraft?status=Grounded\"", all);

        var grounded = await _client.GetStringAsync("/Aircraft?status=Grounded");
        Assert.Contains("WF-G1", grounded);
        Assert.DoesNotContain("WF-A1", grounded);
        Assert.Contains("Showing 1 of 3 aircraft", grounded);

        var searched = await _client.GetStringAsync("/Aircraft?search=wf-a2");
        Assert.Contains("WF-A2", searched);
        Assert.DoesNotContain("WF-A1", searched);

        var none = await _client.GetStringAsync("/Aircraft?search=no-such-aircraft");
        Assert.Contains("No aircraft match these filters.", none);

        Assert.Contains("id=\"fleet-status-chart-title\"", all);
        Assert.Contains("Active 2 (67%), In maintenance 0 (0%), Grounded 1 (33%), Retired 0 (0%)", all);
        Assert.Equal(2, Regex.Matches(all, "class=\"donut__segment ").Count);
        Assert.Contains("data-tooltip-value=\"2 aircraft · 67%\"", WebUtility.HtmlDecode(all));
        Assert.Contains("donut--has-selection", grounded);

        var badStatus = await _client.GetAsync("/Aircraft?status=NotAStatus");
        Assert.Equal(HttpStatusCode.OK, badStatus.StatusCode);
    }

    [Fact]
    public async Task Layout_renders_clock_with_utc_fallback()
    {
        var html = await _client.GetStringAsync("/");

        Assert.Contains("data-clock", html);
        Assert.Matches(@"data-clock-utc datetime=""\d{4}-\d{2}-\d{2}T\d{2}:\d{2}Z"">\d{2}:\d{2} UTC<", html);
        Assert.Contains("js/clock.js", html);
    }

    [Fact]
    public async Task Huge_page_number_does_not_fail()
    {
        var response = await _client.GetAsync($"/Aircraft?page={int.MaxValue}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private Task<HttpResponseMessage> ModalGetAsync(string url)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(ModalRequestHeader, "true");
        return _client.SendAsync(request);
    }

    private Task<HttpResponseMessage> ModalPostAsync(
        string url,
        string formHtml,
        Dictionary<string, string> form,
        string? returnUrl = "/Aircraft")
    {
        form["__RequestVerificationToken"] = HiddenValue(formHtml, "__RequestVerificationToken");
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Add(ModalRequestHeader, "true");
        if (returnUrl is not null)
        {
            request.Headers.Add(ModalReturnUrlHeader, returnUrl);
        }

        return _client.SendAsync(request);
    }

    private static async Task<string> ReadRedirectUrlAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("redirectUrl").GetString()!;
    }

    private Task<HttpResponseMessage> PostFormAsync(string formPageUrl, string postUrl, Dictionary<string, string> form) =>
        PostFormAsync(_client, formPageUrl, postUrl, form);

    private static async Task<HttpResponseMessage> PostFormAsync(
        HttpClient client,
        string formPageUrl,
        string postUrl,
        Dictionary<string, string> form)
    {
        var page = await client.GetStringAsync(formPageUrl);
        form["__RequestVerificationToken"] = HiddenValue(page, "__RequestVerificationToken");
        return await client.PostAsync(postUrl, new FormUrlEncodedContent(form));
    }

    private static Dictionary<string, string> ValidForm(string registration, string serial = "5123") => new()
    {
        ["RegistrationNumber"] = registration,
        ["Manufacturer"] = "Airbus",
        ["Model"] = "A320-214",
        ["SerialNumber"] = serial,
        ["YearOfManufacture"] = "2012",
        ["TotalFlightHours"] = "31250.5",
        ["Status"] = "Active"
    };

    private static string HiddenValue(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"|value=\"([^\"]*)\"[^>]*name=\"{Regex.Escape(name)}\"");
        Assert.True(match.Success, $"Hidden input '{name}' not found.");
        return WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
    }
}
