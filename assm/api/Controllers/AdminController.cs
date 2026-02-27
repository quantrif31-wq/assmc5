using lab4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
namespace lab4.Controllers
{
   

    [Authorize(Policy = "AdminOnly")] // bắt buộc login & quyền StoreManager hoặc claim Admin.Access
    public class AdminController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public AdminController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // =========================
        // USER LIST
        // =========================
        public IActionResult Index()
        {
            var users = _userManager.Users.ToList();
            return View(users);
        }

        // =========================
        // USER DETAILS + PERMISSION
        // =========================
        public async Task<IActionResult> Manage(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var claims = await _userManager.GetClaimsAsync(user);

            ViewBag.AllPermissions = new List<string>
        {
            "Admin.Access",
            "Product.Create",
            "Order.Manage",
            "Inventory.Manage",
            "Report.View"
        };

            return View((user, claims));
        }

        // =========================
        // ADD PERMISSION
        // =========================
        [HttpPost]
        public async Task<IActionResult> AddPermission(string userId, string permission)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            await _userManager.AddClaimAsync(user, new Claim("Permission", permission));
            return RedirectToAction("Manage", new { id = userId });
        }

        // =========================
        // REMOVE PERMISSION
        // =========================
        [HttpPost]
        public async Task<IActionResult> RemovePermission(string userId, string permission)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            await _userManager.RemoveClaimAsync(user, new Claim("Permission", permission));
            return RedirectToAction("Manage", new { id = userId });
        }

        // =========================
        // LOCK / UNLOCK USER
        // =========================
        [HttpPost]
        public async Task<IActionResult> ToggleLock(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            if (user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.Now)
            {
                // unlock
                await _userManager.SetLockoutEndDateAsync(user, null);
            }
            else
            {
                // lock vĩnh viễn
                await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.MaxValue);
            }

            return RedirectToAction("Index");
        }

        // =========================
        // EDIT USER
        // =========================
        public async Task<IActionResult> Edit(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, string fullName, string email, string phoneNumber, string address)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.FullName = fullName;
            user.Email = email;
            user.UserName = email; // UserName = Email
            user.NormalizedEmail = email.ToUpper();
            user.NormalizedUserName = email.ToUpper();
            user.PhoneNumber = phoneNumber;
            user.Address = address;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Cập nhật tài khoản thành công!";
                return RedirectToAction("Index");
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError("", error.Description);

            return View(user);
        }

        // =========================
        // DELETE USER
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var result = await _userManager.DeleteAsync(user);
            if (result.Succeeded)
            {
                TempData["Success"] = "Đã xoá tài khoản thành công!";
            }
            else
            {
                TempData["Error"] = "Lỗi khi xoá tài khoản.";
            }

            return RedirectToAction("Index");
        }
    }
}
