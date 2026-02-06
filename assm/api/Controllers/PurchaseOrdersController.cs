using lab4.Models;
using Lab4.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lab4.Controllers
{
    public class PurchaseOrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PurchaseOrdersController(ApplicationDbContext context)
        {
            _context = context;
        }
        [HttpGet]
        public IActionResult Create()
        {
            ViewBag.Suppliers = _context.Suppliers.ToList();
            return View(new PurchaseOrder
            {
                Items = new List<PurchaseOrderItem>
        {
            new PurchaseOrderItem()
        }
            });
        }
        [HttpGet]
        public async Task<IActionResult> GetProductsBySupplier(int supplierId)
        {
            var products = await _context.SupplierProducts
                .Where(sp => sp.SupplierId == supplierId)
                .Select(sp => new
                {
                    id = sp.Product.Id,
                    name = sp.Product.Name
                })
                .ToListAsync();

            return Json(products);
        }
        [HttpPost]
        public async Task<IActionResult> Create(PurchaseOrder po)
        {
            // 1️⃣ Validate model
            if (!ModelState.IsValid)
            {
                ViewBag.Suppliers = _context.Suppliers.ToList();
                ViewBag.Products = _context.Products.ToList();
                return View(po);
            }

            try
            {
                po.OrderDate = DateTime.UtcNow;
                po.Status = "PENDING";

                // 2️⃣ Sinh code
                po.Code = await GeneratePurchaseOrderCode(po.SupplierId);

                _context.PurchaseOrders.Add(po);
                await _context.SaveChangesAsync();

                return RedirectToAction("Details", new { id = po.Id });
            }
            catch (Exception ex)
            {
                // 3️⃣ Bắt lỗi & hiển thị ra View
                ModelState.AddModelError("", "❌ Không thể tạo đơn mua. Lỗi: " + ex.Message);

                ViewBag.Suppliers = _context.Suppliers.ToList();
                ViewBag.Products = _context.Products.ToList();
                return View(po);
            }
        }

        private async Task<string> GeneratePurchaseOrderCode(int supplierId)
        {
            var supplier = await _context.Suppliers
                .Where(s => s.Id == supplierId)
                .Select(s => new { s.Code })
                .FirstAsync();

            var today = DateTime.UtcNow.Date;
            var datePart = today.ToString("yyyyMMdd");

            // Đếm số đơn cùng ngày + cùng supplier
            var countToday = await _context.PurchaseOrders
                .CountAsync(po =>
                    po.SupplierId == supplierId &&
                    po.OrderDate.Date == today
                );

            var sequence = (countToday + 1).ToString("D3");

            return $"PO-{datePart}-{supplier.Code}-{sequence}";
        }
        [HttpGet]
        public async Task<IActionResult> Details(int id)
        {
            var po = await _context.PurchaseOrders
                .Include(x => x.Supplier)
                .Include(x => x.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(x => x.Id == id);

            if (po == null)
                return NotFound();

            return View(po);
        }
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            var list = await _context.PurchaseOrders
                .Include(po => po.Supplier)
                .OrderByDescending(po => po.OrderDate)
                .ToListAsync();

            return View(list);
        }

        [HttpGet]
        public async Task<IActionResult> GetLastPurchasePrice(int supplierId, int productId)
        {
            decimal price = 0;

            // 1️⃣ Giá mua gần nhất theo NCC + SP
            price = await _context.PurchaseOrderItems
                .Where(x =>
                    x.ProductId == productId &&
                    x.PurchaseOrder.SupplierId == supplierId
                )
                .OrderByDescending(x => x.PurchaseOrder.OrderDate)
                .Select(x => x.UnitPrice)
                .FirstOrDefaultAsync();

            // 2️⃣ Nếu chưa có → lấy giá mua chung (PriceHistory)
            if (price <= 0)
            {
                price = await _context.PriceHistories
                    .Where(x =>
                        x.ProductId == productId &&
                        x.Type == PriceType.PURCHASE
                    )
                    .OrderByDescending(x => x.EffectiveFrom)
                    .Select(x => x.Price)
                    .FirstOrDefaultAsync();
            }

            // 3️⃣ Nếu vẫn chưa có → lấy giá hiện tại của Product
            if (price <= 0)
            {
                price = await _context.Products
                    .Where(p => p.Id == productId)
                    .Select(p => (decimal)p.PriceVnd)
                    .FirstAsync();
            }

            return Json(new { price });
        }
    }

}
