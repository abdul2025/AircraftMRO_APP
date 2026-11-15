using System.Net;
using System.Text.RegularExpressions;
using AircraftMRO.Tests.Support;
using AircraftMRO.Web.Controllers;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AircraftMRO.Tests.Integration;

[Collection(SqlServerCollection.Name)]
public sealed class ReferenceDataWebTests(SqlServerFixture fixture) : IAsyncLifetime
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

    [Theory]
    [InlineData("/Countries", "Saudi Arabia")]
    [InlineData("/AircraftManufacturers", "Airbus")]
    [InlineData("/AircraftModels", "A320-214")]
    public async Task Index_pages_render_with_navigation(string url, string expected)
    {
        var html = await _client.GetStringAsync(url + "?search=" + Uri.EscapeDataString(expected));

        Assert.Contains(expected, html);
        Assert.Contains("href=\"/AircraftManufacturers\"", html);
        Assert.Contains("href=\"/AircraftModels\"", html);
        Assert.Contains("href=\"/Countries\"", html);
    }

    [Fact]
    public async Task Posts_without_antiforgery_token_are_rejected()
    {
        foreach (var url in new[] { "/Countries/Create", "/AircraftManufacturers/Create", "/AircraftModels/Create" })
        {
            var response = await _client.PostAsync(url, new FormUrlEncodedContent(new Dictionary<string, string> { ["Name"] = "X" }));
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
    }

    [Fact]
    public async Task Create_manufacturer_and_model_then_use_them_for_an_aircraft()
    {
        var country = Regex.Match(await _client.GetStringAsync("/AircraftManufacturers/Create"),
            "<option value=\"([0-9a-f-]{36})\"[^>]*>Japan \\(JP\\)</option>").Groups[1].Value;
        Assert.NotEmpty(country);

        var manufacturer = await PostFormAsync("/AircraftManufacturers/Create", "/AircraftManufacturers/Create", new()
        {
            ["Name"] = "Mitsubishi Aircraft",
            ["Code"] = "mitac",
            ["CountryId"] = country,
            ["IsActive"] = "true"
        });
        Assert.Equal(HttpStatusCode.Redirect, manufacturer.StatusCode);
        var manufacturerId = manufacturer.Headers.Location!.OriginalString.Split('/').Last();
        var manufacturerDetails = await _client.GetStringAsync($"/AircraftManufacturers/Details/{manufacturerId}");
        Assert.Contains("MITAC", manufacturerDetails);
        Assert.Contains("Japan (JP)", manufacturerDetails);

        var model = await PostFormAsync($"/AircraftModels/Create?manufacturerId={manufacturerId}", "/AircraftModels/Create", new()
        {
            ["ManufacturerId"] = manufacturerId,
            ["Name"] = "SpaceJet M90",
            ["ModelCode"] = "MRJ90",
            ["AircraftCategory"] = "RegionalJet",
            ["IsActive"] = "true"
        });
        Assert.Equal(HttpStatusCode.Redirect, model.StatusCode);
        var modelId = model.Headers.Location!.OriginalString.Split('/').Last();

        var aircraft = await PostFormAsync("/Aircraft/Create", "/Aircraft/Create", AircraftForm(manufacturerId, modelId));
        Assert.Equal(HttpStatusCode.Redirect, aircraft.StatusCode);
        var details = await _client.GetStringAsync(aircraft.Headers.Location!.OriginalString);
        Assert.Contains("Mitsubishi Aircraft", details);
        Assert.Contains("SpaceJet M90", details);

        // In use now, so the model cannot be deleted.
        var deletePage = await ModalGetAsync($"/AircraftModels/Delete/{modelId}");
        var deleted = await ModalPostAsync($"/AircraftModels/Delete/{modelId}", deletePage, new()
        {
            ["rowVersion"] = HiddenValue(deletePage, "rowVersion")
        });
        Assert.Equal(HttpStatusCode.Conflict, deleted.StatusCode);
        Assert.Contains("Aircraft are recorded against this model", WebUtility.HtmlDecode(await deleted.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Aircraft_with_a_model_of_another_manufacturer_is_rejected_with_the_options_still_listed()
    {
        var html = await ModalGetAsync("/Aircraft/Create");
        var form = AircraftForm(AircraftTestData.ManufacturerId.ToString(), AircraftTestData.OtherModelId.ToString());

        var response = await ModalPostAsync("/Aircraft/Create", html, form);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var fragment = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("The selected model is not made by the selected manufacturer.", fragment);
        Assert.Contains($"value=\"{AircraftTestData.ModelId}\"", fragment);
        Assert.Contains("data-depends-on=\"ManufacturerId\"", fragment);
    }

    [Fact]
    public async Task Deactivated_model_cannot_be_chosen_for_a_new_aircraft()
    {
        var editPage = await _client.GetStringAsync($"/AircraftModels/Edit/{AircraftTestData.OtherModelId}");
        var edit = await PostFormAsync(editPage, $"/AircraftModels/Edit/{AircraftTestData.OtherModelId}", new()
        {
            ["Name"] = "737-800",
            ["ModelCode"] = "737-800",
            ["AircraftCategory"] = "NarrowBody",
            ["IsActive"] = "false",
            ["RowVersion"] = HiddenValue(editPage, "RowVersion")
        });
        Assert.Equal(HttpStatusCode.Redirect, edit.StatusCode);

        var createPage = await _client.GetStringAsync("/Aircraft/Create");
        Assert.DoesNotContain($"value=\"{AircraftTestData.OtherModelId}\"", createPage);

        var response = await PostFormAsync(createPage, "/Aircraft/Create",
            AircraftForm(AircraftTestData.OtherManufacturerId.ToString(), AircraftTestData.OtherModelId.ToString()));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Contains("The selected model is inactive.", WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
    }

    [Fact]
    public async Task Duplicate_country_code_is_reported_on_the_code_field()
    {
        var response = await PostFormAsync("/Countries/Create", "/Countries/Create", new()
        {
            ["Name"] = "Fakeland",
            ["Code"] = "fr",
            ["IsActive"] = "true"
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Contains("A country with this code already exists.", await response.Content.ReadAsStringAsync());
    }

    private static Dictionary<string, string> AircraftForm(string manufacturerId, string modelId) => new()
    {
        ["RegistrationNumber"] = AircraftTestData.UniqueRegistration(),
        ["ManufacturerId"] = manufacturerId,
        ["ModelId"] = modelId,
        ["SerialNumber"] = Guid.NewGuid().ToString("N")[..8],
        ["YearOfManufacture"] = "2015",
        ["TotalFlightHours"] = "100",
        ["Status"] = "Active"
    };

    private async Task<string> ModalGetAsync(string url)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add(ModalFormController.ModalRequestHeader, "true");
        var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync();
    }

    private Task<HttpResponseMessage> ModalPostAsync(string url, string formHtml, Dictionary<string, string> form)
    {
        form["__RequestVerificationToken"] = HiddenValue(formHtml, "__RequestVerificationToken");
        var request = new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) };
        request.Headers.Add(ModalFormController.ModalRequestHeader, "true");
        return _client.SendAsync(request);
    }

    /// <summary>Posts <paramref name="form"/> with the antiforgery token from a form page (a URL or its HTML).</summary>
    private async Task<HttpResponseMessage> PostFormAsync(string formPage, string postUrl, Dictionary<string, string> form)
    {
        var html = formPage.StartsWith('/') ? await _client.GetStringAsync(formPage) : formPage;
        form["__RequestVerificationToken"] = HiddenValue(html, "__RequestVerificationToken");
        return await _client.PostAsync(postUrl, new FormUrlEncodedContent(form));
    }

    private static string HiddenValue(string html, string name)
    {
        var match = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"([^\"]*)\"|value=\"([^\"]*)\"[^>]*name=\"{Regex.Escape(name)}\"");
        Assert.True(match.Success, $"Hidden input '{name}' not found.");
        return WebUtility.HtmlDecode(match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value);
    }
}
