using Lab4.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace lab4.Controllers
{
    [Authorize(Policy = "AdminOnly")]
    public class AuditLogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AuditLogController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? actionFilter, string? search)
        {
            var query = _context.AuditLogs.AsQueryable();

            // Lọc theo loại hành động
            if (!string.IsNullOrEmpty(actionFilter))
            {
                query = query.Where(x => x.Action == actionFilter);
            }

            // Tìm kiếm theo mô tả hoặc người thực hiện
            if (!string.IsNullOrEmpty(search))
            {
                query = query.Where(x =>
                    x.Description.Contains(search) ||
                    x.PerformedBy.Contains(search) ||
                    x.EntityId.Contains(search));
            }

            var logs = await query
                .OrderByDescending(x => x.PerformedAt)
                .Take(200)
                .ToListAsync();

            ViewBag.ActionFilter = actionFilter;
            ViewBag.Search = search;

            return View(logs);
        }
    }
}
