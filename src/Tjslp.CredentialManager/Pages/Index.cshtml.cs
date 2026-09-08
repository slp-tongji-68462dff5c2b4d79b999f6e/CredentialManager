using Tjslp.CredentialManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tjslp.CredentialManager.Pages;

[Authorize]
public sealed class IndexModel : BasePageModel
{
    private readonly CredentialService credentialService;

    public IReadOnlyList<(string CredentialId, string Alias, string Owner, DateTimeOffset? Expire)> Credentials { get; private set; }
        = [];
    public (string CredentialId, string Credential, DateTimeOffset? Expire)? NewCredential { get; private set; }
    public string? Error { get; private set; }

    public IndexModel(CredentialService credentialService)
    {
        this.credentialService = credentialService;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        if (Sub is null) return RequireLogin();
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostCreateAsync(string alias, string? expire, CancellationToken cancellationToken)
    {
        if (Sub is null) return RequireLogin();

        if (string.IsNullOrWhiteSpace(alias))
        {
            await LoadAsync(cancellationToken);
            Error = "别名不能为空。";
            return Page();
        }

        DateTimeOffset? parsedExpire = null;
        if (!string.IsNullOrWhiteSpace(expire))
        {
            if (!DateTimeOffset.TryParse(expire, null, System.Globalization.DateTimeStyles.AssumeUniversal, out var parsed))
            {
                await LoadAsync(cancellationToken);
                Error = "过期时间格式无法解析，请使用 ISO 格式（如 2026-10-01T00:00:00Z）。";
                return Page();
            }
            parsedExpire = parsed;
        }

        NewCredential = await credentialService.CreateAsync(Sub, alias.Trim(), parsedExpire, cancellationToken);

        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAsync(string id, CancellationToken cancellationToken)
    {
        if (Sub is null) return RequireLogin();

        var revoked = await credentialService.RevokeOwnAsync(id, Sub, cancellationToken);
        if (!revoked)
        {
            Error = "吊销失败：没有权限，或下游已拒绝该操作。";
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostSyncAsync(CancellationToken cancellationToken)
    {
        if (Sub is null) return RequireLogin();
        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var result = new List<(string CredentialId, string Alias, string Owner, DateTimeOffset? Expire)>();
        await foreach (var credential in credentialService.ListOwnAsync(Sub!, cancellationToken))
        {
            result.Add(credential);
        }
        Credentials = result;
    }
}
