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
        public async Task<IActionResult> Create(
    Combo combo,
    List<int> selectedProducts,
    IFormFile imageFile)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Products = await _context.Products.ToListAsync();
                return View(combo);
            }

            // 📷 Lưu ảnh nếu có
            if (imageFile != null && imageFile.Length > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(imageFile.FileName);
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var filePath = Path.Combine(uploadPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await imageFile.CopyToAsync(stream);
                }

                combo.ImageUrl = "/images/" + fileName;
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
        // GET: Edit Combo
        [Authorize(Policy = "ManageProduct")]
        public async Task<IActionResult> Edit(int id)
        {
            var combo = await _context.Combos
                .Include(c => c.ComboItems)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (combo == null) return NotFound();

            ViewBag.Products = await _context.Products
                .Where(p => p.IsActive)
                .ToListAsync();

            return View(combo);
        }
        // POST: Edit Combo
        [HttpPost]
        [Authorize(Policy = "ManageProduct")]
        public async Task<IActionResult> Edit(
            int id,
            Combo combo,
            List<int> selectedProducts,
            IFormFile? imageFile)
        {
            if (id != combo.Id) return NotFound();

            var existingCombo = await _context.Combos
                .Include(c => c.ComboItems)
                .FirstOrDefaultAsync(c => c.Id == id);

            if (existingCombo == null) return NotFound();

            if (!ModelState.IsValid)
            {
                ViewBag.Products = await _context.Products.ToListAsync();
                return View(combo);
            }

            // Update basic info
            existingCombo.Name = combo.Name;
            existingCombo.Description = combo.Description;
            existingCombo.PriceVnd = combo.PriceVnd;

            // 📷 Nếu upload ảnh mới
            if (imageFile != null && imageFile.Length > 0)
            {
                var fileName = Guid.NewGuid() + Path.GetExtension(imageFile.FileName);
                var uploadPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/images/combos");

                if (!Directory.Exists(uploadPath))
                    Directory.CreateDirectory(uploadPath);

                var filePath = Path.Combine(uploadPath, fileName);

                using var stream = new FileStream(filePath, FileMode.Create);
                await imageFile.CopyToAsync(stream);

                existingCombo.ImageUrl = "/images/combos/" + fileName;
            }

            // 🔄 Cập nhật lại danh sách sản phẩm
            existingCombo.ComboItems.Clear();

            foreach (var productId in selectedProducts)
            {
                existingCombo.ComboItems.Add(new ComboItem
                {
                    ProductId = productId
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Manage));
        }
    }
}