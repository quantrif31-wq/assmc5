using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lab4.Data;
using Lab4.Models;

namespace Lab4.Controllers
{
    public class ComboController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ComboController(ApplicationDbContext context)
        {
            _context = context;
        }

        // USER VIEW
        public async Task<IActionResult> Index()
        {
            var combos = await _context.Combos
                .Include(c => c.ComboItems)
                .ThenInclude(ci => ci.Product)
                .Where(c => c.IsActive)
                .ToListAsync();

            return View(combos);
        }

        // ADMIN VIEW
        [Authorize(Policy = "ManageProduct")]
        public async Task<IActionResult> Manage()
        {
            var combos = await _context.Combos
                .Include(c => c.ComboItems)
                .ThenInclude(ci => ci.Product)
                .ToListAsync();

            return View(combos);
        }

        // CREATE
        [Authorize(Policy = "ManageProduct")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive)
                .ToListAsync();

            return View();
        }

        [HttpPost]
        [Authorize(Policy = "ManageProduct")]
        public async Task<IActionResult> Create(Combo combo, List<int> selectedProducts)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Products = await _context.Products.ToListAsync();
                return View(combo);
            }

            foreach (var productId in selectedProducts)
            {
                combo.ComboItems.Add(new ComboItem
                {
                    ProductId = productId
                });
            }

            _context.Combos.Add(combo);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Manage));
        }

        // SOFT DELETE
        [Authorize(Policy = "ManageProduct")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var combo = await _context.Combos.FindAsync(id);
            if (combo == null) return NotFound();

            combo.IsActive = !combo.IsActive;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Manage));
        }
    }
}