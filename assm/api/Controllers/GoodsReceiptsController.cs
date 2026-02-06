using lab4.Models;
using Lab4.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace lab4.Controllers
{
    public class GoodsReceiptsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public GoodsReceiptsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ====== FORM NHẬP KHO ======
        [HttpGet]
        public IActionResult Create(int purchaseOrderId)
        {
            var po = _context.PurchaseOrders
                .Include(x => x.Items)
                .ThenInclude(x => x.Product)
                .FirstOrDefault(x => x.Id == purchaseOrderId);

            if (po == null) return NotFound();

            return View(po);
        }

        // ====== XÁC NHẬN NHẬP KHO ======
        [HttpPost]
        public async Task<IActionResult> Create(int purchaseOrderId, List<GoodsReceiptItemVM> items)
        {
            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                // 1. tạo phiếu nhập
                var receipt = new GoodsReceipt
                {
                    Code = "GR" + DateTime.Now.Ticks,
                    PurchaseOrderId = purchaseOrderId,
                    ReceiptDate = DateTime.UtcNow
                };

                _context.GoodsReceipts.Add(receipt);
                await _context.SaveChangesAsync();

                // 2. duyệt từng dòng nhập
                foreach (var item in items)
                {
                    if (item.Quantity <= 0) continue;

                    _context.GoodsReceiptItems.Add(new GoodsReceiptItem
                    {
                        GoodsReceiptId = receipt.Id,
                        ProductId = item.ProductId,
                        QuantityReceived = item.Quantity,
                        UnitCost = item.UnitCost
                    });
                    // ===== LƯU LỊCH SỬ GIÁ NHẬP =====
                    _context.PriceHistories.Add(new PriceHistory
                    {
                        ProductId = item.ProductId,
                        Type = PriceType.PURCHASE,
                        Price = item.UnitCost,
                        EffectiveFrom = DateTime.UtcNow,
                        ReferenceCode = receipt.Code,          // GRxxxx
                        ChangedBy = User.FindFirstValue(ClaimTypes.Name)
                    });
                    // cập nhật tồn
                    var inventory = await _context.Inventories
                        .FirstAsync(i => i.ProductId == item.ProductId);

                    inventory.Quantity += item.Quantity;
                    inventory.UpdatedAt = DateTime.UtcNow;

                    // log kho
                    _context.InventoryLogs.Add(new InventoryLog
                    {
                        ProductId = item.ProductId,
                        QuantityChange = item.Quantity,
                        QuantityAfter = inventory.Quantity,
                        ActionType = "RECEIPT",
                        ReferenceId = receipt.Id,
                        ReferenceTable = "GoodsReceipts",
                        CreatedAt = DateTime.UtcNow
                    });

                    // cập nhật số lượng đã nhận trong PO
                    var poItem = await _context.PurchaseOrderItems
                        .FirstAsync(x =>
                            x.PurchaseOrderId == purchaseOrderId &&
                            x.ProductId == item.ProductId);

                    poItem.QuantityReceived += item.Quantity;
                }

                // 3. cập nhật trạng thái PO
                var po = await _context.PurchaseOrders.FindAsync(purchaseOrderId);
                po!.Status = "RECEIVED";

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                return RedirectToAction("Index", "PurchaseOrders");
            }
            catch
            {
                await tx.RollbackAsync();
                throw;
            }
        }
    }

}
