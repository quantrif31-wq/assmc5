using lab4.Models;
using Lab4.Data;
using Lab4.Models;
using Lab4.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text;

namespace Lab4.ApiControllers
{
    [ApiController]
    [Route("api/cart")]
    public class CartApiController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IVnPayService _vnPayService;

        private const string SessionKey = "CART_SESSION_ID";

        public CartApiController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IVnPayService vnPayService)
        {
            _context = context;
            _userManager = userManager;
            _vnPayService = vnPayService;
        }

        // =========================
        // GET CART
        // =========================
        [HttpGet]
        public async Task<IActionResult> GetCart()
        {
            var cart = await GetOrCreateCartAsync();

            await _context.Entry(cart)
                .Collection(c => c.Items)
                .Query()
                .Include(i => i.Product)
                .LoadAsync();

            var items = cart.Items.Select(i =>
            {
                var unit = ParseVndToLong(i.UnitPriceText);
                return new
                {
                    i.ProductId,
                    Name = i.Product?.Name,
                    Image = i.Product?.ImageUrl,
                    Quantity = i.Quantity,
                    LineTotal = unit * i.Quantity,
                    LineTotalText = FormatVnd(unit * i.Quantity)
                };
            });

            var subtotal = items.Sum(i => i.LineTotal);
            var total = subtotal;

            return Ok(new
            {
                items,
                subtotal,
                subtotalText = FormatVnd(subtotal),
                total,
                totalText = FormatVnd(total)
            });
        }

        // =========================
        // ADD TO CART
        // =========================
        [HttpPost("add")]
        public async Task<IActionResult> Add(int productId, int quantity = 1)
        {
            try
            {
                if (quantity < 1) quantity = 1;

                var product = await _context.Products
                    .Include(p => p.Inventory)
                    .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);

                if (product == null)
                    return NotFound(new { ok = false, message = "Sản phẩm không tồn tại" });

                if (product.Inventory == null || product.Inventory.Quantity <= 0)
                    return BadRequest(new { ok = false, message = "Sản phẩm đã hết hàng" });

                var cart = await GetOrCreateCartAsync();

                var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                var currentQty = item?.Quantity ?? 0;

                if (currentQty + quantity > product.Inventory.Quantity)
                {
                    return BadRequest(new
                    {
                        ok = false,
                        message = $"Chỉ còn {product.Inventory.Quantity} phần"
                    });
                }

                if (item == null)
                {
                    _context.CartItems.Add(new CartItem
                    {
                        CartId = cart.Id,
                        ProductId = productId,
                        Quantity = quantity,
                        UnitPriceText = product.PriceText
                    });
                }
                else
                {
                    item.Quantity += quantity;
                }

                await _context.SaveChangesAsync();

                var totalQty = await _context.CartItems
                    .Where(i => i.CartId == cart.Id)
                    .SumAsync(i => i.Quantity);

                return Ok(new
                {
                    ok = true,
                    totalQty
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { ok = false, message = "Lỗi server: " + ex.Message });
            }
        }

        // =========================
        // INC / DEC
        // =========================
        [HttpPost("inc")]
        public async Task<IActionResult> Inc(int productId)
        {
            var cart = await GetOrCreateCartAsync();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item == null) return NotFound();

            item.Quantity++;
            await _context.SaveChangesAsync();

            return Ok();
        }

        [HttpPost("dec")]
        public async Task<IActionResult> Dec(int productId)
        {
            var cart = await GetOrCreateCartAsync();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);

            if (item == null) return NotFound();

            item.Quantity--;

            if (item.Quantity <= 0)
                _context.CartItems.Remove(item);

            await _context.SaveChangesAsync();
            return Ok();
        }

        // =========================
        // CHECKOUT DATA
        // =========================
        [HttpGet("checkout")]
        public async Task<IActionResult> Checkout()
        {
            var cart = await GetOrCreateCartAsync();

            if (!cart.Items.Any())
                return BadRequest(new { message = "Giỏ hàng trống" });

            decimal subtotal = 0;
            foreach (var i in cart.Items)
                subtotal += ParseVndToLong(i.UnitPriceText) * i.Quantity;

            var total = subtotal;

            return Ok(new
            {
                subtotal,
                subtotalText = FormatVnd(subtotal),
                total,
                totalText = FormatVnd(total)
            });
        }

        // =========================
        // PLACE ORDER
        // =========================
        [HttpPost("place-order")]
        public async Task<IActionResult> PlaceOrder([FromBody] CheckoutVM vm)
        {
            var cart = await GetOrCreateCartAsync();
            if (!cart.Items.Any())
                return BadRequest("Cart empty");

            foreach (var ci in cart.Items)
            {
                var inventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == ci.ProductId);

                if (inventory == null || inventory.Quantity < ci.Quantity)
                    return BadRequest($"{ci.ProductId} không đủ tồn kho");
            }

            decimal subtotal = cart.Items.Sum(i =>
                ParseVndToLong(i.UnitPriceText) * i.Quantity);

            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                var isVnPay = vm.PaymentMethod == "BANK";

                var order = new Order
                {
                    UserId = IsLoggedIn ? GetUserId() : null,
                    SessionId = IsLoggedIn ? null : HttpContext.Session.GetString(SessionKey),
                    FullName = vm.FullName,
                    Phone = vm.Phone,
                    Address = vm.Address,
                    City = vm.City,
                    Email = vm.Email,
                    PaymentMethod = vm.PaymentMethod,
                    Subtotal = subtotal,
                    Total = subtotal,
                    Status = isVnPay ? "PENDING_PAYMENT" : "NEW"
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                foreach (var ci in cart.Items)
                {
                    var unit = ParseVndToLong(ci.UnitPriceText);
                    _context.OrderItems.Add(new OrderItem
                    {
                        OrderId = order.Id,
                        ProductId = ci.ProductId,
                        ProductName = ci.Product!.Name,
                        UnitPrice = unit,
                        Quantity = ci.Quantity,
                        LineTotal = unit * ci.Quantity
                    });
                }

                await _context.SaveChangesAsync();

                if (!isVnPay)
                {
                    foreach (var ci in cart.Items)
                    {
                        var inv = await _context.Inventories
                            .FirstAsync(i => i.ProductId == ci.ProductId);
                        inv.Quantity -= ci.Quantity;
                    }
                    _context.CartItems.RemoveRange(cart.Items);
                }

                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                if (isVnPay)
                {
                    var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
                    var url = _vnPayService.CreatePaymentUrl(order.Id, order.Total,
                        $"Thanh toán #{order.Id}", ip);

                    return Ok(new { paymentUrl = url });
                }

                return Ok(new { orderId = order.Id });
            }
            catch
            {
                await tx.RollbackAsync();
                return StatusCode(500, "Order failed");
            }
        }

        // =========================
        // HELPERS
        // =========================
        private bool IsLoggedIn => User?.Identity?.IsAuthenticated == true;

        private string? GetUserId()
            => _userManager.GetUserId(User);

        private static long ParseVndToLong(string? priceText)
        {
            if (string.IsNullOrWhiteSpace(priceText)) return 0;
            var sb = new StringBuilder();
            foreach (var c in priceText)
                if (char.IsDigit(c)) sb.Append(c);
            return long.TryParse(sb.ToString(), out var v) ? v : 0;
        }

        private static string FormatVnd(decimal amount)
        {
            return string.Format(
                CultureInfo.GetCultureInfo("vi-VN"),
                "{0:N0} ₫", amount);
        }

        private async Task<Cart> GetOrCreateCartAsync()
        {
            if (IsLoggedIn)
            {
                var userId = GetUserId();
                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.UserId == userId);

                if (cart == null)
                {
                    cart = new Cart { UserId = userId };
                    _context.Carts.Add(cart);
                    await _context.SaveChangesAsync();
                }
                return cart;
            }
            else
            {
                var sid = HttpContext.Session.GetString(SessionKey)
                    ?? Guid.NewGuid().ToString("N");

                HttpContext.Session.SetString(SessionKey, sid);

                var cart = await _context.Carts
                    .Include(c => c.Items)
                    .FirstOrDefaultAsync(c => c.SessionId == sid);

                if (cart == null)
                {
                    cart = new Cart { SessionId = sid };
                    _context.Carts.Add(cart);
                    await _context.SaveChangesAsync();
                }
                return cart;
            }
        }
        [HttpGet("count")]
        public async Task<IActionResult> Count()
        {
            var cart = await GetOrCreateCartAsync();
            var totalQty = cart.Items.Sum(i => i.Quantity);
            return Ok(new { totalQty });
        }
    }
}