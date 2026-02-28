using Lab4.Data;
using Lab4.Models.QL_MGG;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

public class QL_MGGController : Controller
{
    private readonly ApplicationDbContext _context;

    public QL_MGGController(ApplicationDbContext context)
    {
        _context = context;
    }

    // Danh sách
    public async Task<IActionResult> Index()
    {
        var vouchers = await _context.DiscountVouchers
            .Include(v => v.User)
            .ToListAsync();

        return View(vouchers);
    }

    // GET Create
    public IActionResult Create()
    {
        return View();
    }

    // POST Create
    [HttpPost]
    public async Task<IActionResult> Create(DiscountVoucher model)
    {
        if (!ModelState.IsValid)
            return View(model);

        _context.DiscountVouchers.Add(model);
        await _context.SaveChangesAsync();

        return RedirectToAction(nameof(Index));
    }
}