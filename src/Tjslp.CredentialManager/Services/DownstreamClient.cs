using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using Tjslp.CredentialManager.Protocol;

namespace Tjslp.CredentialManager.Services;

public sealed class DownstreamClient(HttpClient http, Uri baseUri)
{
    public async Task<(string CredentialId, string Credential, DateTimeOffset? Expire)?> CreateAsync(
        DateTimeOffset? expire,
        CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<CreateRequest, CreateResponse>(
            "create", new CreateRequest(expire), cancellationToken);
        return response is null
            ? null
            : (response.CredentialId, response.Credential, response.Expire);
    }

    public async Task<bool> RevokeAsync(string id, CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<RevokeRequest, RevokeResponse>(
            "revoke", new RevokeRequest(id), cancellationToken);
        return response?.Succeeded ?? false;
    }

    public async IAsyncEnumerable<(string CredentialId, DateTimeOffset? Expire)> QueryAsync(
        IReadOnlyList<string> ids,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var response = await PostAsync<QueryRequest, QueryResponse>(
            "query", new QueryRequest(ids), cancellationToken);
        if (response is null)
        {
            yield break;
        }

        foreach (var item in response.Credentials)
        {
            yield return (item.CredentialId, item.Expire);
        }
    }

    private async Task<TResponse?> PostAsync<TRequest, TResponse>(string path, TRequest body, CancellationToken cancellationToken)
    {
        var response = await http.PostAsJsonAsync(new Uri(baseUri, path), body, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<TResponse>(cancellationToken);
    }
}
