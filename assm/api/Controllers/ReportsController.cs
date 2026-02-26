using Lab4.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lab4.Controllers
{
    [Authorize(Roles = "StoreManager,Accountant")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            // ===== KPI =====
            var completedOrders = _context.Orders
                .Where(o => o.Status == "Completed");

            var pendingOrders = _context.Orders
                .Where(o => o.Status != "Completed" && o.Status != "Cancelled");

            var totalRevenue = completedOrders.Sum(o => (decimal?)o.Total) ?? 0;
            var expectedRevenue = pendingOrders.Sum(o => (decimal?)o.Total) ?? 0;

            var totalOrders = _context.Orders.Count();
            var completedCount = completedOrders.Count();

            // ===== Inventory (safe — bảng có thể chưa tồn tại) =====
            int totalInventory = 0;
            object inventories = new List<object>();
            try
            {
                totalInventory = _context.Inventories.Sum(i => i.Quantity);
                inventories = _context.Inventories
                    .Select(i => new
                    {
                        product = i.Product.Name,
                        quantity = i.Quantity
                    })
                    .ToList();
            }
            catch { /* Inventories table chưa tồn tại */ }

            // ===== Revenue by day (CHỈ ĐƠN HOÀN TẤT) =====
            var revenueByDay = completedOrders
     .GroupBy(o => o.CreatedAt.Date)
     .Select(g => new
     {
         Date = g.Key,
         Total = g.Sum(x => x.Total)
     })
     .OrderBy(x => x.Date)
     .ToList()   // ⬅️ CHỐT QUERY TẠI ĐÂY
     .Select(x => new
     {
         date = x.Date.ToString("dd/MM"), // ⬅️ FORMAT SAU KHI RA RAM
         total = x.Total
     })
     .ToList();

            // ===== Top products (CHỈ ĐƠN HOÀN TẤT) =====
            var topProducts = _context.OrderItems
                .Where(i => i.Order.Status == "Completed")
                .GroupBy(i => i.ProductName)
                .Select(g => new
                {
                    product = g.Key,
                    quantity = g.Sum(x => x.Quantity)
                })
                .OrderByDescending(x => x.quantity)
                .Take(5)
                .ToList();

            // ===== Orders by status =====
            var ordersByStatus = _context.Orders
                .GroupBy(o => o.Status)
                .Select(g => new { status = g.Key, count = g.Count() })
                .ToList();

            var completionRate = totalOrders > 0
                ? Math.Round((decimal)completedCount / totalOrders * 100, 1)
                : 0m;

            // ===== ViewBag =====
            ViewBag.TotalRevenue = totalRevenue;
            ViewBag.ExpectedRevenue = expectedRevenue;
            ViewBag.TotalOrders = totalOrders;
            ViewBag.CompletedOrders = completedCount;
            ViewBag.TotalInventory = totalInventory;
            ViewBag.CompletionRate = completionRate;

            ViewBag.RevenueByDay = revenueByDay;
            ViewBag.TopProducts = topProducts;
            ViewBag.Inventories = inventories;
            ViewBag.OrdersByStatus = ordersByStatus;

            return View();
        }
    }
}