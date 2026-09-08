using System.Text.Json;
using System.Text.Json.Serialization;

namespace Tjslp.CredentialManager.E2E.MockDownstream;

public static class MockDownstreamEndpoints
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static void MapMockDownstream(this WebApplication app, MockDownstreamState state)
    {
        app.MapPost("/create", async (HttpContext context) =>
        {
            DateTimeOffset? expire = null;
            using (var document = await JsonDocument.ParseAsync(context.Request.Body))
            {
                if (document.RootElement.TryGetProperty("expire", out var expireElement) && expireElement.ValueKind == JsonValueKind.String)
                {
                    expire = expireElement.GetDateTimeOffset();
                }
            }

            var result = state.New(expire);
            return Results.Json(new { result.CredentialId, result.Credential, Expire = result.Expire }, JsonOpts);
        });

        app.MapPost("/revoke", async (HttpContext context) =>
        {
            string id;
            using (var document = await JsonDocument.ParseAsync(context.Request.Body))
            {
                id = document.RootElement.GetProperty("credentialId").GetString() ?? "";
            }

            var succeeded = state.Revoke(id);
            return Results.Json(new { succeeded }, JsonOpts);
        });

        app.MapPost("/query", async (HttpContext context) =>
        {
            IReadOnlyList<string> ids;
            using (var document = await JsonDocument.ParseAsync(context.Request.Body))
            {
                if (document.RootElement.TryGetProperty("credentialIds", out var array) && array.ValueKind == JsonValueKind.Array)
                {
                    ids = array.EnumerateArray().Select(element => element.GetString() ?? "").ToList();
                }
                else
                {
                    ids = Array.Empty<string>();
                }
            }

            var credentials = state.List(ids, DateTimeOffset.UtcNow)
                .Select(item => new { item.CredentialId, Expire = item.Expire })
                .ToList();
            return Results.Json(new { credentials }, JsonOpts);
        });
    }
}
