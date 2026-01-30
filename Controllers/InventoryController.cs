using Lab4.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Lab4.Controllers
{
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Inventory
        public async Task<IActionResult> Index()
        {
            var inventories = await _context.Inventories
                .Include(i => i.Product)
                .OrderBy(i => i.Product!.SortOrder)
                .ToListAsync();

            return View(inventories);
        }

        // GET: Inventory/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var inventory = await _context.Inventories
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (inventory == null) return NotFound();
            return View(inventory);
        }

        // POST: Inventory/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, int quantity)
        {
            var inventory = await _context.Inventories.FindAsync(id);
            if (inventory == null) return NotFound();

            inventory.Quantity = quantity;
            inventory.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        // GET: Inventory/AddStock/5
        public async Task<IActionResult> AddStock(int id)
        {
            var inventory = await _context.Inventories
                .Include(i => i.Product)
                .FirstOrDefaultAsync(i => i.Id == id);

            if (inventory == null) return NotFound();
            return View(inventory);
        }
        // POST: Inventory/AddStock
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddStock(int id, int addQuantity)
        {
            if (addQuantity <= 0)
            {
                ModelState.AddModelError("", "Số lượng nhập phải lớn hơn 0");
            }

            var inventory = await _context.Inventories.FindAsync(id);
            if (inventory == null) return NotFound();

            if (!ModelState.IsValid)
                return RedirectToAction(nameof(AddStock), new { id });

            inventory.Quantity += addQuantity;   // 👈 CỘNG THÊM
            inventory.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
        public IActionResult Logs()
        {
            var logs = _context.InventoryLogs
                .Include(l => l.Product)
                .OrderByDescending(l => l.CreatedAt)
                .ToList();

            return View(logs);
        }

    }
}
