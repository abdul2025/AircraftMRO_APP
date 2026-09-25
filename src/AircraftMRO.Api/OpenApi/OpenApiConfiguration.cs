using System.Text.Json.Serialization;
using Microsoft.OpenApi;
using Scalar.AspNetCore;

namespace AircraftMRO.Api.OpenApi;

public static class OpenApiConfiguration
{
    private const string DocumentTitle = "Aircraft MRO API";

    public static IServiceCollection AddApiDocumentation(this IServiceCollection services)
    {
        // The OpenAPI generator reads the minimal-API JSON options, not the MVC ones,
        // so enums are registered here too to be documented as the strings the API sends.
        services.ConfigureHttpJsonOptions(options =>
            options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

        services.AddOpenApi(options =>
        {
            options.AddDocumentTransformer((document, _, _) =>
            {
                document.Info = new OpenApiInfo
                {
                    Title = DocumentTitle,
                    Version = "v1",
                    Description =
                        "Fleet records for the Aircraft MRO platform. Reads return an `ETag` header; " +
                        "send it back in `If-Match` to update or delete, so concurrent edits are rejected " +
                        "with 412 instead of being overwritten. Errors use RFC 9457 problem details with a `code` extension."
                };
                return Task.CompletedTask;
            });

            options.AddOperationTransformer((operation, context, _) =>
            {
                var metadata = context.Description.ActionDescriptor.EndpointMetadata;

                if (metadata.OfType<RequiresIfMatchAttribute>().Any())
                {
                    operation.Parameters ??= [];
                    operation.Parameters.Add(new OpenApiParameter
                    {
                        Name = "If-Match",
                        In = ParameterLocation.Header,
                        Required = true,
                        Description = "The quoted ETag from the latest GET or create response, e.g. \"AAAAAAAAB9E=\".",
                        Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                    });
                }

                foreach (var returnsETag in metadata.OfType<ReturnsETagAttribute>())
                {
                    if (operation.Responses?.TryGetValue(returnsETag.StatusCode.ToString(), out var response) == true
                        && response is OpenApiResponse concrete)
                    {
                        concrete.Headers ??= new Dictionary<string, IOpenApiHeader>();
                        concrete.Headers["ETag"] = new OpenApiHeader
                        {
                            Description = "Current version of the aircraft; send it in If-Match to update or delete.",
                            Schema = new OpenApiSchema { Type = JsonSchemaType.String }
                        };
                    }
                }

                return Task.CompletedTask;
            });
        });

        return services;
    }

    /// <summary>Serves the OpenAPI document and the Scalar reference UI (at <c>/scalar</c>).</summary>
    public static WebApplication MapApiDocumentation(this WebApplication app)
    {
        app.MapOpenApi();
        // Internal API: keep the reference local. No AI agent, MCP, telemetry, or share/deploy
        // toolbar, so the document and requests never go to Scalar's hosted services.
        app.MapScalarApiReference(options => options
            .WithTitle(DocumentTitle)
            .SortTagsAlphabetically()
            .WithDefaultHttpClient(ScalarTarget.Shell, ScalarClient.Curl)
            .DisableAgent()
            .DisableMcp()
            .DisableTelemetry()
            .HideDeveloperTools());

        return app;
    }
}
