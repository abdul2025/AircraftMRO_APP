using System.Net.Http.Json;
using System.Text.Json;
using AircraftMRO.Tests.Support;
using AircraftMRO.Web.Features.Notifications;
using AircraftMRO.Web.Features.Notifications.Models;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;

namespace AircraftMRO.Tests.Integration;

/// <summary>
/// End to end: a change made through the API is recorded in the shared database and pushed
/// by the Web host's dispatcher to a connected SignalR client.
/// </summary>
[Collection(SqlServerCollection.Name)]
public sealed class RealtimeNotificationTests(SqlServerFixture fixture) : IAsyncLifetime
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);

    private WebHostFactory _web = null!;
    private ApiHostFactory _api = null!;

    public async Task InitializeAsync()
    {
        var connectionString = await fixture.CreateDatabaseAsync();
        _web = new WebHostFactory(connectionString);
        _api = new ApiHostFactory(connectionString);
    }

    public async Task DisposeAsync()
    {
        await _web.DisposeAsync();
        await _api.DisposeAsync();
    }

    [Fact]
    public async Task Api_change_reaches_web_clients_and_the_catch_up_endpoint()
    {
        var server = _web.Server;
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, NotificationsHub.Path.TrimStart('/')), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        var received = new TaskCompletionSource<NotificationMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.On<NotificationMessage>(nameof(INotificationsClient.NotificationReceived), message => received.TrySetResult(message));
        await connection.StartAsync();

        // Let the dispatcher take its starting position before the change happens.
        await Task.Delay(WebHostFactory.NotificationPollInterval * 5);

        var registration = AircraftTestData.UniqueRegistration();
        using var apiClient = _api.CreateClient();
        var created = await apiClient.PostAsJsonAsync("/api/aircraft", new
        {
            registrationNumber = registration,
            manufacturer = "Airbus",
            model = "A320-214",
            serialNumber = registration,
            yearOfManufacture = 2012,
            totalFlightHours = 100.5m,
            status = "Active"
        });
        created.EnsureSuccessStatusCode();

        var message = await received.Task.WaitAsync(ReceiveTimeout);
        Assert.Equal("Created", message.Action);
        Assert.Equal("Aircraft", message.EntityType);
        Assert.Equal($"Aircraft {registration} was created", message.Message);
        Assert.Equal($"/Aircraft/Details/{message.EntityId}", message.Url);

        // A browser that reconnects asks for everything after the last id it saw.
        using var webClient = _web.CreateClient();
        using var catchUp = JsonDocument.Parse(await webClient.GetStringAsync($"/Notifications/Since?afterId={message.Id - 1}"));
        var first = catchUp.RootElement.EnumerateArray().First();
        Assert.Equal(message.Id, first.GetProperty("id").GetInt64());
        Assert.Equal(message.Message, first.GetProperty("message").GetString());
        Assert.Empty(JsonDocument.Parse(await webClient.GetStringAsync($"/Notifications/Since?afterId={message.Id}")).RootElement.EnumerateArray());

        // The bell and the history page both show it.
        var history = await webClient.GetStringAsync("/Notifications");
        Assert.Contains($"Aircraft {registration} was created", history);
        Assert.Contains($"data-notification-id=\"{message.Id}\"", history);
    }
}
