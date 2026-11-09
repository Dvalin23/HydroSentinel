using HydroSentinel.Services;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Identity;
using HydroSentinel.Data;
using HydroSentinel.Backend;
using Microsoft.EntityFrameworkCore;

namespace HydroSentinel.Pages.Admin;

public class AdminPageModel(IAdminPortalProjection portal, IDemoMode demo, SignInManager<ApplicationUser> signIn,
    UserManager<ApplicationUser> users, SupportTicketService supportTickets) : PageModel
{
    public IAdminPortalProjection Portal { get; } = portal;
    public AdminPortalSnapshot Data => Portal.Snapshot;
    public bool IsDemo => Portal.IsDevelopmentDemo;
    [BindProperty(SupportsGet = true)] public string? Id { get; set; }
    [BindProperty] public AdminLoginInput Input { get; set; } = new();
    public IReadOnlyList<SupportTicket> SupportTickets { get; private set; } = [];
    public SupportTicket? SupportTicket { get; private set; }
    public string? SupportCustomerEmail { get; private set; }
    public static IReadOnlyList<string> SupportStatuses { get; } =
        [SupportTicketStatus.Open, SupportTicketStatus.InProgress, SupportTicketStatus.WaitingOnCustomer, SupportTicketStatus.Resolved];
    [BindProperty] public string? SupportReplyText { get; set; }
    [BindProperty] public string? SelectedSupportStatus { get; set; }
    public string? SupportActionStatus => TempData["SupportActionStatus"] as string;

    public async Task OnGetAsync(CancellationToken ct)
    {
        if (HttpContext.Request.Path.Equals("/Admin/Support", StringComparison.OrdinalIgnoreCase))
            SupportTickets = await supportTickets.AllTicketsAsync(ct);
        else if (HttpContext.Request.Path.Equals("/Admin/Support/Details", StringComparison.OrdinalIgnoreCase))
            await LoadSupportTicketAsync(ct);
    }

    public async Task<IActionResult> OnPostSupportReplyAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 32) return NotFound();
        if (string.IsNullOrWhiteSpace(SupportReplyText) || SupportReplyText.Trim().Length > 4000)
            ModelState.AddModelError(nameof(SupportReplyText), "Enter a reply of up to 4,000 characters.");
        if (!ModelState.IsValid) { await LoadSupportTicketAsync(ct); return Page(); }

        var staffId = users.GetUserId(User);
        if (string.IsNullOrEmpty(staffId)) return Challenge();
        if (!await supportTickets.StaffReplyAsync(staffId, Id, SupportReplyText!, ct)) return NotFound();
        TempData["SupportActionStatus"] = "Reply sent to the customer account.";
        return RedirectToPage("/Admin/Support/Details", new { id = Id });
    }

    public async Task<IActionResult> OnPostSupportStatusAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 32) return NotFound();
        if (SelectedSupportStatus is null || !SupportStatuses.Contains(SelectedSupportStatus))
            ModelState.AddModelError(nameof(SelectedSupportStatus), "Choose a valid ticket status.");
        if (!ModelState.IsValid) { await LoadSupportTicketAsync(ct); return Page(); }

        var staffId = users.GetUserId(User);
        if (string.IsNullOrEmpty(staffId)) return Challenge();
        if (!await supportTickets.SetStatusAsync(staffId, Id, SelectedSupportStatus!, ct)) return NotFound();
        TempData["SupportActionStatus"] = "Ticket status updated.";
        return RedirectToPage("/Admin/Support/Details", new { id = Id });
    }

    private async Task LoadSupportTicketAsync(CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(Id) || Id.Length > 32) return;
        SupportTicket = await supportTickets.TicketForStaffAsync(Id, ct);
        if (SupportTicket is not null)
        {
            SelectedSupportStatus ??= SupportTicket.Status;
            SupportCustomerEmail = await users.Users.AsNoTracking().Where(u => u.Id == SupportTicket.UserId)
                .Select(u => u.Email).FirstOrDefaultAsync(ct);
        }
    }

    public async Task<IActionResult> OnPostSignInAsync()
    {
        if (!ModelState.IsValid) return Page();

        // Real sign-in always runs. Only accounts in the Admin role are let through.
        var user = await users.FindByEmailAsync(Input.Email.Trim());
        const string generic = "Email or password is incorrect.";
        if (user is null || !await users.IsInRoleAsync(user, Roles.Admin))
        {
            ModelState.AddModelError(string.Empty, generic);
            return Page();
        }
        var result = await signIn.PasswordSignInAsync(user, Input.Password, Input.RememberMe, lockoutOnFailure: true);
        if (!result.Succeeded)
        {
            ModelState.AddModelError(string.Empty, result.IsLockedOut ? "Too many attempts. Try again in a few minutes." : generic);
            return Page();
        }
        return RedirectToPage("/Admin/Dashboard");
    }

    public IActionResult OnPostMarkNotificationsRead()
    {
        if (!IsDemo) return NotFound();
        demo.MarkNotificationsRead();
        return RedirectToPage("/Admin/Notifications");
    }
}

public sealed class AdminLoginInput
{
    [Required, EmailAddress, Display(Name = "Email address")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    public bool RememberMe { get; set; }
}
