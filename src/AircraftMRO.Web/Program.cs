using System.Globalization;
using AircraftMRO.Application;
using AircraftMRO.Infrastructure;
using AircraftMRO.Web.Configuration;
using AircraftMRO.Web.Features.Notifications;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Razor;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));
builder.Services.Configure<RazorViewEngineOptions>(options =>
    options.ViewLocationExpanders.Add(new FeatureViewLocationExpander()));
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Real-time notifications: rows recorded by the audit interceptor are pushed over SignalR.
builder.Services.AddSignalR();
builder.Services.AddOptions<NotificationOptions>()
    .Bind(builder.Configuration.GetSection(NotificationOptions.SectionName))
    .Validate(NotificationOptions.IsValid, "Notifications settings are out of range.")
    .ValidateOnStart();
builder.Services.AddHostedService<NotificationDispatcher>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// Browsers post numbers with a "." separator; pin the culture so binding and display
// don't depend on the host's regional settings.
var appCulture = new CultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(appCulture),
    SupportedCultures = [appCulture],
    SupportedUICultures = [appCulture]
});

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.MapHub<NotificationsHub>(NotificationsHub.Path);

app.Run();

public partial class Program;
