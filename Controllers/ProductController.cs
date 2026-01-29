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
    }
    
    }