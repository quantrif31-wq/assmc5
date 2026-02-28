using lab4.Models;
using Lab4.Data;
using Lab4.Models;
using Lab4.Models.QL_MGG;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

public class QL_MGGController : Controller
{
    private readonly ApplicationDbContext _context;
    private readonly UserManager<ApplicationUser> _userManager;

    public QL_MGGController(
        ApplicationDbContext context,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _userManager = userManager;
    }

    // =========================
    // DANH SÁCH VOUCHER
    // =========================
    public async Task<IActionResult> Index()
    {
        var vouchers = await _context.DiscountVouchers
            .Include(v => v.User)
            .ToListAsync();

        return View(vouchers);
    }

    // =========================
    // GET: CREATE
    // =========================
    public IActionResult Create()
    {
        LoadUserDropdown();
        return View();
    }

    // =========================
    // POST: CREATE
    // =========================
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(DiscountVoucher model)
    {
        if (!ModelState.IsValid)
        {
            LoadUserDropdown();
            return View(model);
        }

        try
        {
            _context.DiscountVouchers.Add(model);
            await _context.SaveChangesAsync();

            TempData["success"] = "Tạo mã giảm giá thành công!";
            return RedirectToAction(nameof(Index));
        }
        catch (DbUpdateException)
        {
            ModelState.AddModelError("Code", "Mã giảm giá đã tồn tại!");
            LoadUserDropdown();
            return View(model);
        }
    }

    // =========================
    // LOAD DROPDOWN USER
    // =========================
    private void LoadUserDropdown()
    {
        ViewBag.UserList = _userManager.Users
            .Select(u => new SelectListItem
            {
                Value = u.Id,
                Text = u.Email
            })
            .ToList();
    }
}