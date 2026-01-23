using System.ComponentModel.DataAnnotations;
using lab4.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace lab4.Areas.Identity.Pages.Account
{
    public class LoginWithEmailOtpModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;

        public LoginWithEmailOtpModel(SignInManager<ApplicationUser> signInManager)
        {
            _signInManager = signInManager;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        public string? ReturnUrl { get; set; }

        public class InputModel
        {
            [Required]
            [StringLength(6, MinimumLength = 6)]
            [Display(Name = "OTP Code")]
            public string Code { get; set; } = string.Empty;

            [Display(Name = "Remember this machine")]
            public bool RememberMachine { get; set; }
        }

        // ===== GET =====
        public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
        {
            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            ReturnUrl = returnUrl ?? Url.Content("~/");
            return Page();
        }

        // ===== POST =====
        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");

            var user = await _signInManager.GetTwoFactorAuthenticationUserAsync();
            if (user == null)
            {
                return RedirectToPage("/Account/Login");
            }

            var result = await _signInManager.TwoFactorSignInAsync(
    provider: TokenOptions.DefaultEmailProvider,
    code: Input.Code,
    isPersistent: false,
    rememberClient: Input.RememberMachine
);


            if (result.Succeeded)
            {
                return LocalRedirect(returnUrl);
            }

            if (result.IsLockedOut)
            {
                return RedirectToPage("/Account/Lockout");
            }

            ModelState.AddModelError(string.Empty, "Invalid verification code.");
            return Page();
        }
    }
}
