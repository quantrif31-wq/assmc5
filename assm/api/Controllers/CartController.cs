using lab4.Models;
using Lab4.ApiControllers;
using Lab4.Data;
using Lab4.Models;
using Lab4.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using System.Text;

namespace Lab4.Controllers
{
    public class CartController : Controller
    {
        private readonly ApplicationDbContext _context;
        
        private const string SessionKey = "CART_SESSION_ID";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IVnPayService _vnPayService;

        public CartController(ApplicationDbContext context, UserManager<ApplicationUser> userManager, IVnPayService vnPayService)
        {
            _context = context;
            _userManager = userManager;
            _vnPayService = vnPayService;
        }
        [HttpGet]
        public async Task<IActionResult> Checkout()
        {
            var cart = await GetOrCreateCartAsync();

            await _context.Entry(cart)
                .Collection(c => c.Items)
                .Query()
                .Include(i => i.Product)
                .LoadAsync();

            if (cart.Items == null || !cart.Items.Any())
            {
                TempData["Error"] = "Giỏ hàng trống, vui lòng chọn món.";
                return RedirectToAction("Menu", "Product");
            }

            decimal subtotal = 0m;
            foreach (var i in cart.Items)
            {
                var unit = (decimal)ParseVndToLong(i.UnitPriceText);
                subtotal += unit * i.Quantity;
            }

            decimal taxRate = 0m;
            decimal tax = Math.Round(subtotal * taxRate, 0, MidpointRounding.AwayFromZero);
            decimal shippingFee = 0m; // mặc định, sẽ cập nhật real-time qua AJAX
            decimal total = subtotal + tax + shippingFee;

            var vm = new CheckoutVM
            {
                Email = User?.Identity?.IsAuthenticated == true ? User.Identity?.Name : null,

                Items = cart.Items.Select(i =>
                {
                    var unit = (decimal)ParseVndToLong(i.UnitPriceText);
                    return new CheckoutItemVM
                    {
                        Name = i.Product?.Name ?? "",
                        Qty = i.Quantity,
                        PriceText = FormatVnd(unit * i.Quantity)
                    };
                }).ToList(),

                Currency = "VND",
                Subtotal = subtotal,
                Tax = tax,
                ShippingFee = shippingFee,
                Total = total,
                SubtotalText = FormatVnd(subtotal),
                TaxText = FormatVnd(tax),
                ShippingFeeText = FormatVnd(shippingFee),
                TotalText = FormatVnd(total)
            };

            return View(vm);
        }
        // =========================
        // PLACE ORDER
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(CheckoutVM vm)
        {
            var cart = await GetOrCreateCartAsync();

            await _context.Entry(cart)
                .Collection(c => c.Items)
                .Query()
                .Include(i => i.Product)
                .LoadAsync();

            if (!cart.Items.Any())
                return RedirectToAction(nameof(Index));

            // =========================
            // CHECK INVENTORY (ANTI OVERSELL)
            // =========================
            foreach (var ci in cart.Items)
            {
                var inventory = await _context.Inventories
                    .FirstOrDefaultAsync(i => i.ProductId == ci.ProductId);

                if (inventory == null || inventory.Quantity < ci.Quantity)
                {
                    TempData["Error"] =
                        $"{ci.Product?.Name ?? "Sản phẩm"} chỉ còn {inventory?.Quantity ?? 0} phần";
                    return RedirectToAction(nameof(Checkout));
                }
            }

            // =========================
            // SERVER CALC
            // =========================
            decimal subtotal = 0;
            foreach (var i in cart.Items)
                subtotal += ParseVndToLong(i.UnitPriceText) * i.Quantity;

            decimal tax = 0;

            // ===== TÍNH PHÍ SHIP PHÍA SERVER (bảo mật) =====
            decimal shippingFee = ShippingApiController.GetShippingFee(vm.DistrictId);
            string? districtName = ShippingApiController.GetDistrictName(vm.DistrictId);

            decimal total = subtotal + tax + shippingFee;

            using var tx = await _context.Database.BeginTransactionAsync();

            try
            {
                // BANK = VNPay
                var isVnPayMethod = vm.PaymentMethod == "BANK";
                var initialStatus = isVnPayMethod ? "PENDING_PAYMENT" : "Pending";

                var order = new Order
                {
                    UserId = IsLoggedIn ? GetUserId() : null,
                    SessionId = IsLoggedIn ? null : HttpContext.Session.GetString(SessionKey),

                    FullName = vm.FullName,
                    Phone = vm.Phone,
                    Address = vm.Address,
                    City = districtName ?? vm.City,
                    District = districtName,
                    Email = vm.Email,
                    Note = vm.Note,
                    PaymentMethod = vm.PaymentMethod,

                    Subtotal = subtotal,
                    Tax = tax,
                    ShippingFee = shippingFee,
                    Total = total,
                    Status = initialStatus
                };

                _context.Orders.Add(order);
                await _context.SaveChangesAsync();

                // =========================
                // ORDER ITEMS
                // =========================
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

                // =========================
                // TRỪ TỒN KHO + CLEAR CART (COD)
                // =========================
                if (!isVnPayMethod)
                {
                    foreach (var ci in cart.Items)
                    {
                        var inventory = await _context.Inventories
                            .FirstAsync(i => i.ProductId == ci.ProductId);

                        if (inventory.Quantity < ci.Quantity)
                            throw new Exception("Inventory not enough");

                        inventory.Quantity -= ci.Quantity;
                        inventory.UpdatedAt = DateTime.Now;

                        _context.Inventories.Update(inventory);
                    }

                    _context.CartItems.RemoveRange(cart.Items);
                }

                // ✅ SAVE DUY NHẤT 1 LẦN
                await _context.SaveChangesAsync();
                await tx.CommitAsync();

                // =========================
                // VNPAY REDIRECT
                // =========================
                if (isVnPayMethod)
                {
                    var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";
                    var orderInfo = $"Thanh toán đơn hàng #{order.Id}";
                    var paymentUrl = _vnPayService.CreatePaymentUrl(
                        order.Id,
                        total,
                        orderInfo,
                        ipAddress
                    );

                    return Redirect(paymentUrl);
                }

                return RedirectToAction(nameof(Success), new { id = order.Id });
            }
            catch
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Có lỗi xảy ra khi đặt hàng. Vui lòng thử lại.";
                return RedirectToAction(nameof(Checkout));
            }
        }



        [HttpGet]
        public async Task<IActionResult> Success(int id)
        {
            // ===== CASE 1: ĐÃ ĐĂNG NHẬP =====
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = GetUserId();

                var order = await _context.Orders
                    .Include(o => o.Items)
                    .FirstOrDefaultAsync(o => o.Id == id && o.UserId == userId);

                if (order == null)
                    return NotFound();

                return View(order);
            }

            // ===== CASE 2: KHÁCH VÃNG LAI =====
            var sessionId = HttpContext.Session.GetString(SessionKey);
            if (string.IsNullOrEmpty(sessionId))
            {
                return View("NoAccess"); // view thông báo mất session
            }

            var guestOrder = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id && o.SessionId == sessionId);

            if (guestOrder == null)
                return NotFound();

            return View(guestOrder);
        }

        // =========================
        // VNPAY CALLBACK - Người dùng redirect về
        // =========================
        [HttpGet]
        public async Task<IActionResult> VnPayReturn()
        {
            var result = _vnPayService.ValidateCallback(Request.Query);

            if (!result.IsValid)
            {
                TempData["Error"] = result.Message;
                return RedirectToAction(nameof(Checkout));
            }

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == result.OrderId);
            if (order == null)
            {
                TempData["Error"] = "Không tìm thấy đơn hàng.";
                return RedirectToAction(nameof(Index));
            }

            // Cập nhật thông tin VNPay
            order.VnpTxnRef = result.OrderId.ToString();
            order.VnpTransactionNo = result.TransactionNo;
            order.VnpResponseCode = result.ResponseCode;
            order.VnpPayDate = result.PayDate;

            if (result.IsSuccess)
            {
                order.Status = "PAID";

                // Xóa cart sau khi thanh toán thành công
                var cart = await GetOrCreateCartAsync();
                if (cart.Items.Any())
                {
                    _context.CartItems.RemoveRange(cart.Items);
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Success), new { id = order.Id });
            }
            else
            {
                order.Status = "PAYMENT_FAILED";
                await _context.SaveChangesAsync();

                TempData["Error"] = $"Thanh toán thất bại: {result.Message}";
                return View("VnPayResult", result);
            }
        }

        // =========================
        // VNPAY IPN - VNPay gọi server
        // =========================
        [HttpGet]
        public async Task<IActionResult> VnPayIPN()
        {
            var result = _vnPayService.ValidateCallback(Request.Query);

            if (!result.IsValid)
            {
                return Json(new { RspCode = "97", Message = "Invalid signature" });
            }

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.Id == result.OrderId);
            if (order == null)
            {
                return Json(new { RspCode = "01", Message = "Order not found" });
            }

            // Kiểm tra số tiền khớp
            if (order.Total != result.Amount)
            {
                return Json(new { RspCode = "04", Message = "Invalid amount" });
            }

            // Kiểm tra trạng thái đơn hàng (tránh xử lý lại)
            if (order.Status == "PAID")
            {
                return Json(new { RspCode = "02", Message = "Order already confirmed" });
            }

            // Cập nhật thông tin
            order.VnpTxnRef = result.OrderId.ToString();
            order.VnpTransactionNo = result.TransactionNo;
            order.VnpResponseCode = result.ResponseCode;
            order.VnpPayDate = result.PayDate;

            if (result.IsSuccess)
            {
                order.Status = "PAID";
            }
            else
            {
                order.Status = "PAYMENT_FAILED";
            }

            await _context.SaveChangesAsync();

            return Json(new { RspCode = "00", Message = "Confirm Success" });
        }




        // Format VND đơn giản (45,000 ₫)
        private static string FormatVnd(decimal amount)
        {
            // format theo nhóm 3 số, không có lẻ
            return string.Format(System.Globalization.CultureInfo.GetCultureInfo("vi-VN"), "{0:N0} ₫", amount);
        }


        [HttpGet]
        public async Task<IActionResult> Inc(int productId)
        {
            var cart = await GetOrCreateCartAsync();

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                item.Quantity += 1;
                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Dec(int productId)
        {
            var cart = await GetOrCreateCartAsync();

            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            if (item != null)
            {
                item.Quantity -= 1;

                if (item.Quantity <= 0)
                    _context.CartItems.Remove(item);

                await _context.SaveChangesAsync();
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Count()
        {
            var cart = await GetOrCreateCartAsync();

            // đảm bảo Items đã load (GetOrCreateCartAsync có Include Items rồi)
            var totalQty = cart.Items.Sum(i => i.Quantity);

            return Json(new { totalQty });
        }

        private bool IsLoggedIn => User?.Identity?.IsAuthenticated == true;

        private string GetOrCreateSessionId()
        {
            var sid = HttpContext.Session.GetString(SessionKey);
            if (string.IsNullOrWhiteSpace(sid))
            {
                sid = Guid.NewGuid().ToString("N");
                HttpContext.Session.SetString(SessionKey, sid);
            }
            return sid;
        }

        private string? GetUserId()
            => _userManager.GetUserId(User); // hoặc User.FindFirstValue(ClaimTypes.NameIdentifier)

        // ✅ Nếu login: merge cart session -> cart user (gọi trước khi lấy cart user)
        private async Task EnsureMergedIfLoggedInAsync()
        {
            if (!IsLoggedIn) return;

            var userId = GetUserId();
            if (string.IsNullOrWhiteSpace(userId)) return;

            // Có thể user đã có session cart từ trước khi login
            var sid = HttpContext.Session.GetString(SessionKey);
            if (string.IsNullOrWhiteSpace(sid)) return;

            var sessionCart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.SessionId == sid && c.UserId == null);

            if (sessionCart == null) return;

            var userCart = await _context.Carts
                .Include(c => c.Items)
                .FirstOrDefaultAsync(c => c.UserId == userId);

            if (userCart == null)
            {
                // Gán thẳng cart session cho user
                sessionCart.UserId = userId;
                sessionCart.SessionId = null; // từ giờ dùng theo user
            }
            else
            {
                // Merge items: cộng dồn số lượng nếu trùng product
                foreach (var sItem in sessionCart.Items)
                {
                    var uItem = userCart.Items.FirstOrDefault(i => i.ProductId == sItem.ProductId);
                    if (uItem == null)
                    {
                        userCart.Items.Add(new CartItem
                        {
                            ProductId = sItem.ProductId,
                            Quantity = sItem.Quantity,
                            UnitPriceText = sItem.UnitPriceText
                        });
                    }
                    else
                    {
                        uItem.Quantity += sItem.Quantity;
                    }
                }

                _context.Carts.Remove(sessionCart);
            }

            await _context.SaveChangesAsync();

            // Option: xóa session key cho sạch
            HttpContext.Session.Remove(SessionKey);
        }

        private async Task<Cart> GetOrCreateCartAsync()
        {
            if (IsLoggedIn)
            {
                await EnsureMergedIfLoggedInAsync();

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
                var sid = GetOrCreateSessionId();

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

        // ✅ HIỂN THỊ CART
        public async Task<IActionResult> Index()
        {
            var cart = await GetOrCreateCartAsync();

            // load Product để lấy Name/Image
            await _context.Entry(cart)
                .Collection(c => c.Items)
                .Query()
                .Include(i => i.Product)
                .LoadAsync();

            // ===== TÍNH TIỀN =====
            long subtotal = 0;

            foreach (var i in cart.Items)
            {
                var unit = ParseVndToLong(i.UnitPriceText);   // "50.000 VND" -> 50000
                subtotal += unit * i.Quantity;
            }

            // Bạn muốn VAT thì đổi taxRate (ví dụ 0.1 = 10%). Không muốn thuế thì để 0.
            decimal taxRate = 0m;
            long tax = (long)Math.Round(subtotal * (decimal)taxRate, MidpointRounding.AwayFromZero);

            long total = subtotal + tax;

            var vm = new Lab4.Models.CartViewModel
            {
                StoreName = "Your Store",
                Items = cart.Items.Select(i =>
                {
                    var unit = ParseVndToLong(i.UnitPriceText);
                    var line = unit * i.Quantity;

                    return new Lab4.Models.CartItemViewModel
                    {
                        ProductId = i.ProductId,
                        Name = i.Product?.Name ?? "",
                        ImageUrl = i.Product?.ImageUrl ?? "",
                        Quantity = i.Quantity,

                        // ✅ Hiển thị đúng "đơn giá x số lượng"
                        LineTotalText = FormatVnd(line)
                    };
                }).ToList(),

                // ✅ Hiển thị VND
                SubtotalText = FormatVnd(subtotal),
                TaxText = FormatVnd(tax),
                TotalText = FormatVnd(total)
            };

            return View(vm);
        }
        // ===== HELPER: parse "50.000 VND" -> 50000 =====
        private static long ParseVndToLong(string? priceText)
        {
            if (string.IsNullOrWhiteSpace(priceText)) return 0;

            // Lấy tất cả ký tự số (0-9), bỏ dấu . , khoảng trắng, chữ VND...
            var digits = new StringBuilder();
            foreach (var ch in priceText)
                if (char.IsDigit(ch)) digits.Append(ch);

            return long.TryParse(digits.ToString(), out var v) ? v : 0;
        }

        // ===== HELPER: format 50000 -> "50.000 VND" =====
        private static string FormatVnd(long amount)
        {
            var vi = CultureInfo.GetCultureInfo("vi-VN");
            return string.Format(vi, "{0:N0} VND", amount);
        }

        // ✅ Endpoint AJAX: thêm vào giỏ, KHÔNG redirect, trả JSON
        [HttpPost]
        [IgnoreAntiforgeryToken]
        public async Task<IActionResult> AddAjax(int productId, int quantity = 1)
        {
            try
            {
                if (quantity < 1) quantity = 1;

                var product = await _context.Products
                    .Include(p => p.Inventory)
                    .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);

                if (product == null)
                    return Json(new { ok = false, message = "Sản phẩm không tồn tại" });

                if (product.Inventory == null || product.Inventory.Quantity <= 0)
                    return Json(new { ok = false, message = "Sản phẩm đã hết hàng" });

                var cart = await GetOrCreateCartAsync();

                var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
                var currentQty = item?.Quantity ?? 0;

                // ❌ vượt tồn kho
                if (currentQty + quantity > product.Inventory.Quantity)
                {
                    return Json(new
                    {
                        ok = false,
                        message = $"Chỉ còn {product.Inventory.Quantity} phần"
                    });
                }

                if (item == null)
                {
                    item = new CartItem
                    {
                        CartId = cart.Id,
                        ProductId = productId,
                        Quantity = quantity,
                        UnitPriceText = product.PriceText
                    };
                    _context.CartItems.Add(item);
                }
                else
                {
                    item.Quantity += quantity;
                }

                await _context.SaveChangesAsync();

                var totalQty = await _context.CartItems
                    .Where(i => i.CartId == cart.Id)
                    .SumAsync(i => i.Quantity);

                return Json(new { ok = true, totalQty });
            }
            catch (Exception ex)
            {
                return Json(new { ok = false, message = "Lỗi: " + ex.Message });
            }
        }


        [HttpGet]
        public async Task<IActionResult> OrderHistory()
        {
            // ✅ ĐÃ ĐĂNG NHẬP
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = GetUserId();

                var orders = await _context.Orders
                    .Where(o => o.UserId == userId)
                    .OrderByDescending(o => o.CreatedAt)
                    .ToListAsync();

                return View(orders);
            }

            // ❌ CHƯA ĐĂNG NHẬP → DÙNG SESSION
            var sid = HttpContext.Session.GetString(SessionKey);
            if (string.IsNullOrEmpty(sid))
            {
                return View("NoAccess"); // view thông báo mất session
            }

            var guestOrders = await _context.Orders
                .Where(o => o.SessionId == sid)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();

            return View("OrderHistory", guestOrders);
        }

    }
}
