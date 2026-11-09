using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HydroSentinel.Pages.Account;

public sealed class RegisterConfirmationModel : PageModel
{
    public bool DeliveryFailed { get; private set; }

    public void OnGet(bool deliveryFailed = false)
    {
        DeliveryFailed = deliveryFailed;
    }
}
