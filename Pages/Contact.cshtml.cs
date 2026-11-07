using System.ComponentModel.DataAnnotations;
using HydroSentinel.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HydroSentinel.Pages;

[ValidateAntiForgeryToken]
public class ContactModel(IFormTestingMode formTestingMode) : PageModel
{
    public bool TestModeEnabled => formTestingMode.Enabled;
    [BindProperty]
    public ContactForm Form { get; set; } = new();

    public string? StatusMessage { get; private set; }

    public void OnGet()
    {
    }

    public void OnPost()
    {
        if (!ModelState.IsValid)
        {
            return;
        }

        StatusMessage = TestModeEnabled
            ? "Test submission successful. No message was sent or saved."
            : "Your enquiry is ready for review, but message delivery has not been configured yet. No message was sent.";
    }

    public class ContactForm
    {
        [Required(ErrorMessage = "Please enter your full name.")]
        [Display(Name = "Full name")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter your email address.")]
        [EmailAddress(ErrorMessage = "Please enter a valid email address.")]
        [Display(Name = "Email address")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please choose an enquiry type.")]
        [Display(Name = "Enquiry type")]
        public string EnquiryType { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a subject.")]
        [StringLength(120, ErrorMessage = "The subject cannot exceed 120 characters.")]
        public string Subject { get; set; } = string.Empty;

        [Required(ErrorMessage = "Please enter a message.")]
        [StringLength(2000, ErrorMessage = "The message cannot exceed 2,000 characters.")]
        public string Message { get; set; } = string.Empty;
    }
}
