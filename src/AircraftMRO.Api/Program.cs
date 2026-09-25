using System.Text.Json.Serialization;
using AircraftMRO.Api.OpenApi;
using AircraftMRO.Application;
using AircraftMRO.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddApiDocumentation();

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseExceptionHandler();
app.UseStatusCodePages();

// OpenAPI document at /openapi/v1.json and the Scalar UI at /scalar - Development only.
if (app.Environment.IsDevelopment())
{
    app.MapApiDocumentation();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
