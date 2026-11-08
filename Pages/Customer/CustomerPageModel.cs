using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HydroSentinel.Pages.Customer;

/// <summary>Shared plumbing for customer pages: they all show data for the signed-in owner only.</summary>
public abstract class CustomerPageModel : PageModel
{
    protected string? UserId => User.FindFirstValue(ClaimTypes.NameIdentifier);

    protected IActionResult SignInRequired()
        => RedirectToPage("/Login", new { ReturnUrl = Request.Path + Request.QueryString });

    /// <summary>Parses the route/query id; returns null when it is missing or not a guid.</summary>
    protected Guid? RequestedDeviceId(string? value)
        => Guid.TryParse(value, out var id) ? id : null;
}
