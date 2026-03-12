using System.Net.Http.Json;
using System.Text.Json;
using Aegis.Shared.Serialization;

namespace Aegis.Universe.IntegrationTests;

internal static class JsonTestExtensions
{
    private static readonly JsonSerializerOptions JsonOptions = AegisJson.CreateSerializerOptions();

    public static Task<T?> ReadAegisJsonAsync<T>(this HttpContent content, CancellationToken cancellationToken = default) =>
        content.ReadFromJsonAsync<T>(JsonOptions, cancellationToken);

    public static Task<T?> GetAegisJsonAsync<T>(this HttpClient client, string? requestUri, CancellationToken cancellationToken = default) =>
        client.GetFromJsonAsync<T>(requestUri, JsonOptions, cancellationToken);
}
