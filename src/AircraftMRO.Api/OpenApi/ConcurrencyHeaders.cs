using Microsoft.Net.Http.Headers;

namespace AircraftMRO.Api.OpenApi;

/// <summary>
/// Row versions travel as strong ETags: reads return one, updates and deletes send it back in
/// <c>If-Match</c>. Documented on operations by <see cref="ReturnsETagAttribute"/> and
/// <see cref="RequiresIfMatchAttribute"/>.
/// </summary>
public static class ConcurrencyHeaders
{
    public static void SetETag(HttpResponse response, byte[] rowVersion) =>
        response.Headers.ETag = new EntityTagHeaderValue($"\"{Convert.ToBase64String(rowVersion)}\"").ToString();

    /// <summary>False when <c>If-Match</c> is missing, weak, a list, or not a base64 row version.</summary>
    public static bool TryReadIfMatch(HttpRequest request, out byte[] rowVersion)
    {
        rowVersion = [];
        var header = request.GetTypedHeaders().IfMatch;
        if (header is not [{ IsWeak: false } tag] || tag.Tag.Length < 3)
        {
            return false;
        }

        var value = tag.Tag.Value![1..^1];
        var buffer = new byte[value.Length];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written == 0)
        {
            return false;
        }

        rowVersion = buffer[..written];
        return true;
    }
}
