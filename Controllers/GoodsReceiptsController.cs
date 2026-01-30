using lab4.Models;
using Lab4.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lab4.Controllers
{
    public class GoodsReceiptsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GoodsReceiptsController(ApplicationDbContext context)
        {
            _context = context;
        }
        public IActionResult Create()
        {
            ViewBag.Products = _context.Products.ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(GoodsReceiptViewModel model)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Products = _context.Products.ToList();
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var receipt = new GoodsReceipt
                {
                    Code = "GR" + DateTime.Now.Ticks,
                    ReceiptDate = DateTime.UtcNow
                };

                _context.GoodsReceipts.Add(receipt);
                await _context.SaveChangesAsync();

                foreach (var item in model.Items)
                {
                    var receiptItem = new GoodsReceiptItem
                    {
                        GoodsReceiptId = receipt.Id,
                        ProductId = item.ProductId,
                        QuantityReceived = item.Quantity
                    };

                    _context.GoodsReceiptItems.Add(receiptItem);

                    // 👉 CẬP NHẬT INVENTORY (snapshot)
                    var inventory = await _context.Inventories
                        .FirstAsync(i => i.ProductId == item.ProductId);

                    inventory.Quantity += item.Quantity;
                    inventory.UpdatedAt = DateTime.UtcNow;

                    // 👉 GHI LOG
                    _context.InventoryLogs.Add(new InventoryLog
                    {
                        ProductId = item.ProductId,
                        QuantityChange = item.Quantity,
                        QuantityAfter = inventory.Quantity,
                        ActionType = "PURCHASE",
                        ReferenceId = receipt.Id,
                        ReferenceTable = "GoodsReceipts"
                    });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
