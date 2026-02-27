using Lab4.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
namespace lab4.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // Danh sách đơn hàng
        public async Task<IActionResult> Index()
        {
            var orders = await _context.Orders
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View(orders);
        }

        // Chi tiết đơn hàng
        public async Task<IActionResult> Details(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound();

            return View(order);
        }

        // Cập nhật trạng thái
        [HttpPost]
        [Authorize(Policy = "ManageOrder")] // StoreManager, KitchenStaff hoặc claim Order.Manage
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var order = await _context.Orders.FindAsync(id);

            if (order == null)
                return NotFound();

            // Admin (StoreManager hoặc Admin.Access) → được phép chuyển trạng thái TỰ DO (kể cả lùi)
            bool isAdmin = User.IsInRole("StoreManager") ||
                           User.HasClaim("Permission", "Admin.Access");

            if (isAdmin)
            {
                // Admin chỉ cần status hợp lệ trong danh sách
                var validStatuses = new[] { "Pending", "Preparing", "Delivering", "Done", "Cancelled" };
                if (!validStatuses.Contains(status))
                {
                    TempData["ErrorMessage"] = "Trạng thái không hợp lệ!";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }
            else
            {
                // Staff (KitchenStaff, Order.Manage): Chỉ cho phép trạng thái TIẾN LÊN
                bool isValidTransition = false;

                if (order.Status == "Pending" && (status == "Preparing" || status == "Cancelled"))
                    isValidTransition = true;
                else if (order.Status == "Preparing" && status == "Delivering")
                    isValidTransition = true;
                else if (order.Status == "Delivering" && status == "Done")
                    isValidTransition = true;

                if (!isValidTransition)
                {
                    TempData["ErrorMessage"] = "Chuyển trạng thái không hợp lệ! Nhân viên không thể lùi trạng thái đơn hàng.";
                    return RedirectToAction(nameof(Details), new { id });
                }
            }

            order.Status = status;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật trạng thái thành công.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
