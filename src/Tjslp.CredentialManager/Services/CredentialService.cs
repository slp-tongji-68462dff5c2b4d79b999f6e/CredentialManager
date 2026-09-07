using System.Runtime.CompilerServices;
using Tjslp.CredentialManager.Data;
using Tjslp.CredentialManager.Downstream;

namespace Tjslp.CredentialManager.Services;

public sealed class CredentialService(DownstreamClient downstream, CredentialRepository repository)
{
    public async Task<(string CredentialId, string Credential, DateTimeOffset? Expire)?> CreateAsync(string owner, string alias, DateTimeOffset? expire, CancellationToken cancellationToken = default)
    {
        var created = await downstream.CreateAsync(expire, cancellationToken);
        if (created is null)
        {
            return null;
        }

        var ownership = new CredentialOwnership(created.Value.CredentialId, owner, alias);

        try
        {
            repository.Insert(ownership);
        }
        catch
        {
            try
            {
                await downstream.RevokeAsync(created.Value.CredentialId, CancellationToken.None);
            }
            catch
            {
            }
            throw;
        }

        return (created.Value.CredentialId, created.Value.Credential, created.Value.Expire);
    }

    public async Task<bool> RevokeOwnAsync(string id, string owner, CancellationToken cancellationToken = default)
    {
        var ownership = repository.FindById(id);
        if (ownership is null || ownership.Owner != owner)
        {
            return false;
        }

        return await RevokeAnyAsync(id, cancellationToken);
    }

    public async Task<bool> RevokeAnyAsync(string id, CancellationToken cancellationToken = default)
    {
        repository.Delete(id);

        return await downstream.RevokeAsync(id, cancellationToken);
    }

    public IAsyncEnumerable<(string CredentialId, string Alias, string Owner, DateTimeOffset? Expire)> ListOwnAsync(
        string owner, CancellationToken cancellationToken)
    {
        var local = repository.FindByOwner(owner);

        return FilterByDownstreamAsync(local, cancellationToken);
    }

    public IAsyncEnumerable<(string CredentialId, string Alias, string Owner, DateTimeOffset? Expire)> ListAllAsync(
        CancellationToken cancellationToken)
    {
        var local = repository.FindAll();
        return FilterByDownstreamAsync(local, cancellationToken);
    }

    private async IAsyncEnumerable<(string CredentialId, string Alias, string Owner, DateTimeOffset? Expire)> FilterByDownstreamAsync(
        IEnumerable<CredentialOwnership> local,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        local = [.. local];
        var downstreamExpire = await downstream.QueryAsync(
            [.. local.Select(ownership => ownership.Id)],
            cancellationToken
        ).ToDictionaryAsync();

        foreach (var ownership in local)
        {
            if (!downstreamExpire.TryGetValue(ownership.Id, out var expire))
            {
                repository.Delete(ownership.Id);
                continue;
            }

            yield return (
                ownership.Id,
                ownership.Alias,
                ownership.Owner,
                expire
            );
        }
    }
}
