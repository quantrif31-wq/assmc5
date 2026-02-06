using Lab4.Data;
using lab4.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace lab4.Controllers.Admin
{
    public class PriceManagementController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PriceManagementController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ===== DANH SÁCH GIÁ HIỆN TẠI =====
        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Include(p => p.Inventory)
                .ToListAsync();

            return View(products);
        }

        // ===== LỊCH SỬ GIÁ =====
        public async Task<IActionResult> History(int productId)
        {
            var history = await _context.PriceHistories
                .Where(x => x.ProductId == productId)
                .OrderByDescending(x => x.EffectiveFrom)
                .ToListAsync();

            ViewBag.Product = await _context.Products.FindAsync(productId);
            return View(history);
        }

        // ===== ĐỔI GIÁ BÁN =====
        [HttpGet]
        public async Task<IActionResult> ChangeSalePrice(int productId)
        {
            var product = await _context.Products.FindAsync(productId);
            return View(product);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeSalePrice(int productId, int newPrice, string reason)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                var product = await _context.Products.FindAsync(productId);

                // 1. log lịch sử
                _context.PriceHistories.Add(new PriceHistory
                {
                    ProductId = productId,
                    Type = PriceType.SALE,
                    Price = newPrice,
                    EffectiveFrom = DateTime.UtcNow,
                    Reason = reason,
                    ReferenceCode = "MANUAL",
                    ChangedBy = User.FindFirstValue(ClaimTypes.Name)
                });

                // 2. cập nhật giá hiện hành
                product!.PriceVnd = newPrice;

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }
}