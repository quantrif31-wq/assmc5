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
        [Authorize(Roles = "StoreManager,KitchenStaff")] // Kế toán không được phép đổi trạng thái
        public async Task<IActionResult> UpdateStatus(int id, string status)
        {
            var order = await _context.Orders.FindAsync(id);

            if (order == null)
                return NotFound();

            // Logic State Machine: Chỉ cho phép trạng thái tiến lên
            bool isValidTransition = false;

            if (order.Status == "Pending" && (status == "Preparing" || status == "Cancelled"))
            {
                isValidTransition = true;
            }
            else if (order.Status == "Preparing" && status == "Delivering")
            {
                isValidTransition = true;
            }
            else if (order.Status == "Delivering" && status == "Done")
            {
                isValidTransition = true;
            }

            if (!isValidTransition)
            {
                TempData["ErrorMessage"] = "Chuyển trạng thái không hợp lệ! Không thể lùi trạng thái đơn hàng.";
                return RedirectToAction(nameof(Details), new { id });
            }

            order.Status = status;
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "Cập nhật trạng thái thành công.";
            return RedirectToAction(nameof(Details), new { id });
        }
    }
}
