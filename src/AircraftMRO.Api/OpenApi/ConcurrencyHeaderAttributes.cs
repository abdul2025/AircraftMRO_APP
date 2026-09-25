namespace AircraftMRO.Api.OpenApi;

/// <summary>The action requires the resource's current ETag in the <c>If-Match</c> header.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class RequiresIfMatchAttribute : Attribute;

/// <summary>The action's success response carries the resource's <c>ETag</c> header.</summary>
[AttributeUsage(AttributeTargets.Method)]
public sealed class ReturnsETagAttribute(int statusCode) : Attribute
{
    public int StatusCode { get; } = statusCode;
}
