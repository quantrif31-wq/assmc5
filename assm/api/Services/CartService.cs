using lab4.Models;
using Lab4.Data;
using Lab4.Models;
using Lab4.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

public class CartService : ICartService
{
    private readonly ApplicationDbContext _context;
    private readonly IHttpContextAccessor _http;
    private readonly UserManager<ApplicationUser> _userManager;

    private const string SessionKey = "CART_SESSION_ID";

    public CartService(
        ApplicationDbContext context,
        IHttpContextAccessor http,
        UserManager<ApplicationUser> userManager)
    {
        _context = context;
        _http = http;
        _userManager = userManager;
    }

    private HttpContext Http => _http.HttpContext!;

    private string? GetUserId()
        => _userManager.GetUserId(Http.User);

    private string GetOrCreateSessionId()
    {
        var sid = Http.Session.GetString(SessionKey);

        if (string.IsNullOrEmpty(sid))
        {
            sid = Guid.NewGuid().ToString("N");
            Http.Session.SetString(SessionKey, sid);
        }

        return sid;
    }

    // ========================= GET CART =========================

    public async Task<Cart> GetOrCreateCartAsync()
    {
        var userId = GetUserId();

        if (!string.IsNullOrEmpty(userId))
        {
            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (cart == null)
            {
                cart = new Cart
                {
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return cart;
        }
        else
        {
            var sid = GetOrCreateSessionId();

            var cart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sid);

            if (cart == null)
            {
                cart = new Cart
                {
                    SessionId = sid,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Carts.Add(cart);
                await _context.SaveChangesAsync();
            }

            return cart;
        }
    }

    // ========================= ADD ITEM =========================

    public async Task AddToCartAsync(int productId, int quantity)
    {
        var cart = await GetOrCreateCartAsync();

        var product = await _context.Products.FindAsync(productId);
        if (product == null)
            throw new Exception("Product not found");

        var existing = cart.Items
            .FirstOrDefault(x => x.ProductId == productId);

        if (existing != null)
        {
            existing.Quantity += quantity;
        }
        else
        {
            cart.Items.Add(new CartItem
            {
                ProductId = productId,
                Quantity = quantity,
                UnitPriceText = product.PriceText
            });
        }

        await _context.SaveChangesAsync();
    }

    // ========================= CLEAR CART =========================

    public async Task ClearCartAsync()
    {
        var cart = await GetOrCreateCartAsync();

        _context.CartItems.RemoveRange(cart.Items);

        await _context.SaveChangesAsync();
    }
}