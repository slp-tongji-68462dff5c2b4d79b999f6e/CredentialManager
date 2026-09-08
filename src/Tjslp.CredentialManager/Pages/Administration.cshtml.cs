using Tjslp.CredentialManager.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tjslp.CredentialManager.Pages;

[Authorize(Policy = "Administrator")]
public sealed class AdministrationModel : PageModel
{
    private readonly CredentialService credentialService;

    public IReadOnlyList<(string CredentialId, string Alias, string Owner, DateTimeOffset? Expire)> Credentials { get; private set; }
        = [];
    public string? Error { get; private set; }

    public AdministrationModel(CredentialService credentialService)
    {
        this.credentialService = credentialService;
    }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        await LoadAsync(cancellationToken);
        return Page();
    }

    public async Task<IActionResult> OnPostRevokeAsync(string id, CancellationToken cancellationToken)
    {
        var revoked = await credentialService.RevokeAnyAsync(id, cancellationToken);
        if (!revoked)
        {
            Error = "吊销失败：下游已拒绝该操作，或该凭证已不存在。";
        }

        await LoadAsync(cancellationToken);
        return Page();
    }

    private async Task LoadAsync(CancellationToken cancellationToken)
    {
        var result = new List<(string CredentialId, string Alias, string Owner, DateTimeOffset? Expire)>();
        await foreach (var credential in credentialService.ListAllAsync(cancellationToken))
        {
            result.Add(credential);
        }
        Credentials = result;
    }
}
