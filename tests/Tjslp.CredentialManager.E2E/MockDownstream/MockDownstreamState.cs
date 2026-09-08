using System.Collections.Concurrent;

namespace Tjslp.CredentialManager.E2E.MockDownstream;

public sealed class MockDownstreamState
{
    private readonly ConcurrentDictionary<string, Entry> _credentials = new();

    private sealed record Entry(string Credential, DateTimeOffset? Expire);

    public CreateResult New(DateTimeOffset? expire)
    {
        var id = Guid.NewGuid().ToString("N");
        var credential = "cm_" + Guid.NewGuid().ToString("N");
        _credentials[id] = new Entry(credential, expire);
        return new CreateResult(id, credential, expire);
    }

    public bool Revoke(string id)
    {
        return _credentials.TryRemove(id, out _);
    }

    public List<StoredCredential> List(IReadOnlyList<string> ids, DateTimeOffset now)
    {
        foreach (var kvp in _credentials)
        {
            if (kvp.Value.Expire is DateTimeOffset exp && exp < now)
            {
                _credentials.TryRemove(kvp.Key, out _);
            }
        }

        var allowed = new HashSet<string>(ids);
        return _credentials
            .Where(kvp => allowed.Contains(kvp.Key))
            .Select(kvp => new StoredCredential(kvp.Key, kvp.Value.Expire))
            .ToList();
    }
}

public sealed record CreateResult(string CredentialId, string Credential, DateTimeOffset? Expire);

public sealed record StoredCredential(string CredentialId, DateTimeOffset? Expire);
