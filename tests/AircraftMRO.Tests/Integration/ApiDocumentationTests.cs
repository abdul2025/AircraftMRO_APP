using System.Net;
using System.Text.Json;
using AircraftMRO.Tests.Support;

namespace AircraftMRO.Tests.Integration;

// Documentation endpoints only; no database access, so no container is needed.
public sealed class ApiDocumentationTests
{
    private const string UnusedConnectionString = "Server=unused;Database=unused";

    [Fact]
    public async Task Development_serves_scalar_and_an_accurate_openapi_document()
    {
        await using var factory = new ApiHostFactory(UnusedConnectionString, "Development");
        using var client = factory.CreateClient();

        var scalar = await client.GetAsync("/scalar/v1");
        Assert.Equal(HttpStatusCode.OK, scalar.StatusCode);
        var scalarHtml = await scalar.Content.ReadAsStringAsync();
        Assert.Contains("<title>Aircraft MRO API</title>", scalarHtml);
        Assert.Contains("\"telemetry\":false", scalarHtml);
        Assert.Contains("\"showDeveloperTools\":\"never\"", scalarHtml);

        using var document = JsonDocument.Parse(await client.GetStringAsync("/openapi/v1.json"));
        var root = document.RootElement;
        Assert.Equal("Aircraft MRO API", root.GetProperty("info").GetProperty("title").GetString());

        var statusValues = root.GetProperty("components").GetProperty("schemas").GetProperty("AircraftStatus")
            .GetProperty("enum").EnumerateArray().Select(value => value.GetString());
        Assert.Equal(["Active", "InMaintenance", "Grounded", "Retired"], statusValues);

        var itemPath = root.GetProperty("paths").GetProperty("/api/aircraft/{id}");
        foreach (var method in new[] { "put", "delete" })
        {
            var ifMatch = itemPath.GetProperty(method).GetProperty("parameters").EnumerateArray()
                .Single(parameter => parameter.GetProperty("name").GetString() == "If-Match");
            Assert.Equal("header", ifMatch.GetProperty("in").GetString());
            Assert.True(ifMatch.GetProperty("required").GetBoolean());
        }

        Assert.True(itemPath.GetProperty("get").GetProperty("responses").GetProperty("200")
            .GetProperty("headers").TryGetProperty("ETag", out _));
        Assert.True(root.GetProperty("paths").GetProperty("/api/aircraft").GetProperty("post")
            .GetProperty("responses").GetProperty("201").GetProperty("headers").TryGetProperty("ETag", out _));
    }

    [Theory]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.json")]
    public async Task Documentation_is_not_served_outside_development(string path)
    {
        await using var factory = new ApiHostFactory(UnusedConnectionString, "Production");
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(path)).StatusCode);
    }
}
