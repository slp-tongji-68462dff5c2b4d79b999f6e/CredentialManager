using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Tjslp.CredentialManager.Pages;

public abstract class BasePageModel : PageModel
{
    public string? Sub => User.FindFirstValue("sub") ?? User.FindFirstValue(ClaimTypes.NameIdentifier);

    protected IActionResult RequireLogin() => RedirectToPage("/Index");
}
