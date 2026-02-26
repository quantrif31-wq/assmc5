using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Lab4.Data;
using Lab4.Models;
using System.Security.Claims;

namespace Lab4.Controllers
{
    public class ProductController : Controller
    {
        private readonly ApplicationDbContext _context;

        // Tiêm DbContext vào để làm việc với database thật [1]
        public ProductController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.SortOrder)
                .ToListAsync();

            return View(products);
        }
        public async Task<IActionResult> Menu()
        {
            var products = await _context.Products
                .Include(p => p.Inventory)
                .Where(p => p.IsActive)
                .OrderBy(p => p.SortOrder)
                .ToListAsync();

            return View(products);
        }


        public async Task<IActionResult> Search(string? query, int? minPrice, int? maxPrice, string? sortBy)
        {
            ViewData["SearchQuery"] = query ?? "";
            ViewData["MinPrice"] = minPrice;
            ViewData["MaxPrice"] = maxPrice;
            ViewData["SortBy"] = sortBy ?? "default";

            var productsQuery = _context.Products.Where(p => p.IsActive);

            // Lọc theo từ khóa
            if (!string.IsNullOrWhiteSpace(query))
            {
                var lowerQuery = query.ToLower();
                productsQuery = productsQuery.Where(p => 
                    p.Name.ToLower().Contains(lowerQuery) || 
                    (p.Description != null && p.Description.ToLower().Contains(lowerQuery)));
            }

            // Lọc theo giá tối thiểu
            if (minPrice.HasValue && minPrice.Value > 0)
            {
                productsQuery = productsQuery.Where(p => p.PriceVnd >= minPrice.Value);
            }

            // Lọc theo giá tối đa
            if (maxPrice.HasValue && maxPrice.Value > 0)
            {
                productsQuery = productsQuery.Where(p => p.PriceVnd <= maxPrice.Value);
            }

            // Sắp xếp
            productsQuery = sortBy switch
            {
                "price_asc" => productsQuery.OrderBy(p => p.PriceVnd),
                "price_desc" => productsQuery.OrderByDescending(p => p.PriceVnd),
                "name_asc" => productsQuery.OrderBy(p => p.Name),
                "name_desc" => productsQuery.OrderByDescending(p => p.Name),
                _ => productsQuery.OrderBy(p => p.SortOrder)
            };

            var products = await productsQuery.ToListAsync();

            return View(products);
        }

        // GET: Admin/Product
        [Authorize(Roles = "StoreManager")]
        public async Task<IActionResult> QL_SP()
        {
            var products = await _context.Products
                .OrderBy(p => p.SortOrder)
                .ToListAsync();

            return View(products);
        }

        // GET: Admin/Product/Create
        [Authorize(Roles = "StoreManager")]
        public IActionResult Create()
        {
            return View();
        }

        // POST: Admin/Product/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "StoreManager")]
        public async Task<IActionResult> Create(Product product)
        {
            if (!ModelState.IsValid)
                return View(product);

            // 👇 TẠO INVENTORY NGAY KHI TẠO PRODUCT
            product.Inventory = new Inventory
            {
                Quantity = 0,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Product/Edit/5
        [Authorize(Roles = "StoreManager")]
        public async Task<IActionResult> Edit(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            return View(product);
        }

        // POST: Admin/Product/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "StoreManager")]
        public async Task<IActionResult> Edit(int id, Product product)
        {
            if (id != product.Id) return NotFound();

            if (!ModelState.IsValid)
                return View(product);

            _context.Products.Update(product);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // GET: Admin/Product/Delete/5
        [Authorize(Roles = "StoreManager")]
        public async Task<IActionResult> Delete(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();
            return View(product);
        }

        // POST: Admin/Product/Delete (SOFT DELETE)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "StoreManager")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.IsActive = false; // 👈 soft delete
            _context.Products.Update(product);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(QL_SP));
        }

        // POST: Admin/Product/ToggleActive
        [HttpPost]
        [Authorize(Roles = "StoreManager")]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return NotFound();

            product.IsActive = !product.IsActive;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(QL_SP));
        }
    }
    
    }