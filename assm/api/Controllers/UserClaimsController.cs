using lab4.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace Lab4.Controllers
{
    // Chỉ cho phép Admin truy cập vào trình quản lý quyền
    [Authorize(Policy = "AdminViewProductPolicy")] // Sử dụng chính sách Admin từ Bài 2 [3]
    public class UserClaimsController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;

        public UserClaimsController(UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        // Hiển thị danh sách người dùng
        public IActionResult Index()
        {
            var users = _userManager.Users.ToList();
            return View(users);
        }

        // Trang quản lý Claim cho từng người dùng cụ thể
        public async Task<IActionResult> Manage(string userId)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var claims = await _userManager.GetClaimsAsync(user);
            ViewBag.UserName = user.UserName;
            ViewBag.UserId = userId;
            
            return View(claims);
        }

        [HttpPost]
        public async Task<IActionResult> AddClaim(string userId, string claimType)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            // Thêm Claim với Type = "Permission" và Value = tên quyền (matching policy system)
            var result = await _userManager.AddClaimAsync(user, new Claim("Permission", claimType));
            
            if (result.Succeeded) return RedirectToAction(nameof(Manage), new { userId });
            return BadRequest("Lỗi khi thêm quyền.");
        }

        [HttpPost]
        public async Task<IActionResult> RemoveClaim(string userId, string claimType, string claimValue)
        {
            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var result = await _userManager.RemoveClaimAsync(user, new Claim(claimType, claimValue));
            
            if (result.Succeeded) return RedirectToAction(nameof(Manage), new { userId });
            return BadRequest("Lỗi khi xóa quyền.");
        }
    }
}