using lab4.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace lab4.Areas.Identity.Pages.Account.Manage
{
    public class TwoFactorAuthenticationModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;

        public TwoFactorAuthenticationModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        // ====== PROPERTIES ======
        public bool Is2faEnabled { get; set; }
        public bool HasAuthenticator { get; set; }
        public int RecoveryCodesLeft { get; set; }
        public bool IsMachineRemembered { get; set; }
        public bool IsEmailConfirmed { get; set; }

        [TempData]
        public string? StatusMessage { get; set; }

        // ====== GET ======
        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            Is2faEnabled = await _userManager.GetTwoFactorEnabledAsync(user);
            HasAuthenticator = await _userManager.GetAuthenticatorKeyAsync(user) != null;
            RecoveryCodesLeft = await _userManager.CountRecoveryCodesAsync(user);
            IsMachineRemembered = await _signInManager.IsTwoFactorClientRememberedAsync(user);

            // 👉 EMAIL CONFIRM CHECK
            IsEmailConfirmed = await _userManager.IsEmailConfirmedAsync(user);

            return Page();
        }

        // ====== POST: ENABLE EMAIL OTP ======
        public async Task<IActionResult> OnPostEnableEmail2faAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return NotFound($"Unable to load user with ID '{_userManager.GetUserId(User)}'.");
            }

            await _userManager.SetTwoFactorEnabledAsync(user, true);

            StatusMessage = "Email OTP has been enabled successfully.";
            return RedirectToPage();
        }
    }
}
