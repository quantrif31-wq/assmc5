using lab4.Models;
using lab4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

[Authorize]
public class ManageController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;

    public ManageController(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    // PROFILE
    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        return View(user);
    }

    [HttpPost]
    public async Task<IActionResult> Index(string fullName, string address)
    {
        var user = await _userManager.GetUserAsync(User);
        user.FullName = fullName;
        user.Address = address;
        await _userManager.UpdateAsync(user);
        ViewBag.Message = "Profile updated";
        return View(user);
    }

    // CHANGE PASSWORD
    public IActionResult ChangePassword() => View();

    [HttpPost]
    public async Task<IActionResult> ChangePassword(
        string oldPassword, string newPassword)
    {
        var user = await _userManager.GetUserAsync(User);
        var result = await _userManager.ChangePasswordAsync(
            user, oldPassword, newPassword);

        if (!result.Succeeded)
        {
            foreach (var e in result.Errors)
                ModelState.AddModelError("", e.Description);
            return View();
        }

        return RedirectToAction(nameof(Index));
    }

    // 2FA
    public async Task<IActionResult> TwoFactor()
    {
        var user = await _userManager.GetUserAsync(User);
        ViewBag.Enabled = user.TwoFactorEnabled;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> TwoFactor(bool enable)
    {
        var user = await _userManager.GetUserAsync(User);
        await _userManager.SetTwoFactorEnabledAsync(user, enable);
        return RedirectToAction(nameof(TwoFactor));
    }
}
