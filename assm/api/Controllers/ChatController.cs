using lab4.Models;
using Lab4.Data;
using Lab4.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

[Route("Chat")]
public class ChatController : Controller
{
    private readonly IHttpClientFactory _httpFactory;
    private readonly ApplicationDbContext _context;

    private const string CHAT_HISTORY_KEY = "CHAT_MEMORY";
    private const string CUSTOMER_MEMORY_KEY = "CUSTOMER_MEMORY";

    public ChatController(
        IHttpClientFactory httpFactory,
        ApplicationDbContext context)
    {
        _httpFactory = httpFactory;
        _context = context;
    }

    // ========================== ASK ==========================

    [HttpPost("Ask")]
    public async Task<IActionResult> Ask([FromBody] ChatRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.message))
            return Json(new { reply = "Anh/chị muốn dùng gì hôm nay ạ? 😊" });

        var userMessage = req.message.Trim();

        var history = GetHistory();
        var memory = GetCustomerMemory();

        history.Add(new ChatMessage { Role = "user", Content = userMessage });

        await UpdateMemory(memory, userMessage);

        // ====== CHỐT ĐƠN → HỎI THANH TOÁN ======
        if (memory.Stage == ConversationStage.ConfirmingOrder && memory.Cart.Any())
        {
            memory.Stage = ConversationStage.AskingPaymentMethod;
            SaveCustomerMemory(memory);

            return Json(new
            {
                reply = "<b>Đơn của anh/chị:</b><br>" +
                        string.Join("<br>", memory.Cart.Select(x =>
                            $"{x.ProductName} x{x.Quantity}")) +
                        "<br><br>Anh/chị muốn thanh toán bằng tiền mặt hay chuyển khoản ạ?"
            });
        }

        // ====== ĐÃ CHỌN THANH TOÁN → TẠO ORDER ======
        if (memory.Stage == ConversationStage.Payment && memory.Cart.Any())
        {
            var order = await CreateOrderFromMemory(memory);

            var reply = $"<b>Đặt hàng thành công 🎉</b><br>" +
                        $"Mã đơn: {order.Id}<br>" +
                        $"Tổng tiền: {order.Total:N0}đ<br><br>" +
                        $"Cảm ơn anh/chị đã ủng hộ quán ❤️";

            memory.Cart.Clear();
            memory.Stage = ConversationStage.None;
            memory.PaymentMethod = null;

            SaveCustomerMemory(memory);

            return Json(new { reply });
        }

        SaveCustomerMemory(memory);

        // ====== AI CHAT ======
        var products = await _context.Products
            .Where(p => p.IsActive)
            .OrderBy(p => p.SortOrder)
            .ToListAsync();

        var prompt = BuildPrompt(history, products, memory);

        var client = _httpFactory.CreateClient();
        var payload = JsonSerializer.Serialize(new { message = prompt });

        var response = await client.PostAsync(
            "http://127.0.0.1:5678/webhook/chatbot",
            new StringContent(payload, Encoding.UTF8, "application/json"));

        var raw = await response.Content.ReadAsStringAsync();
        var result = JsonSerializer.Deserialize<ChatResponse>(raw);

        history.Add(new ChatMessage
        {
            Role = "assistant",
            Content = result?.reply ?? ""
        });

        SaveHistory(history);

        return Json(result);
    }

    // ========================== MEMORY ==========================

    private async Task UpdateMemory(CustomerMemory memory, string message)
    {
        var lower = message.ToLower();
        var products = await _context.Products.ToListAsync();

        var quantityMatch = Regex.Match(lower, @"\b\d+\b");
        int quantity = quantityMatch.Success ? int.Parse(quantityMatch.Value) : 0;

        var matchedProduct = products
            .FirstOrDefault(p => lower.Contains(p.Name.ToLower()));

        // Chọn món nhưng chưa có số lượng
        if (matchedProduct != null && quantity == 0)
        {
            memory.TempProductId = matchedProduct.Id;
            memory.TempProductName = matchedProduct.Name;
            memory.Stage = ConversationStage.AskingQuantity;
            return;
        }

        // Đang hỏi số lượng
        if (memory.Stage == ConversationStage.AskingQuantity && quantity > 0)
        {
            var existing = memory.Cart
                .FirstOrDefault(x => x.ProductId == memory.TempProductId);

            if (existing != null)
                existing.Quantity += quantity;
            else
            {
                memory.Cart.Add(new CartItemMemory
                {
                    ProductId = memory.TempProductId!.Value,
                    ProductName = memory.TempProductName!,
                    Quantity = quantity
                });
            }

            memory.TempProductId = null;
            memory.TempProductName = null;
            memory.Stage = ConversationStage.AskingForMore;
            return;
        }

        // Chốt đơn
        if (lower.Contains("chốt") ||
            lower.Contains("thanh toán") ||
            lower.Contains("đủ rồi"))
        {
            memory.Stage = ConversationStage.ConfirmingOrder;
            return;
        }

        // Chọn phương thức thanh toán
        if (lower.Contains("tiền mặt") ||
            lower.Contains("chuyển khoản") ||
            lower.Contains("momo"))
        {
            memory.PaymentMethod = message;
            memory.Stage = ConversationStage.Payment;
            return;
        }
    }

    // ========================== CREATE ORDER ==========================

    private async Task<Order> CreateOrderFromMemory(CustomerMemory memory)
    {
        var order = new Order
        {
            CreatedAt = DateTime.UtcNow,
            Subtotal = 0,
            Items = new List<OrderItem>()
        };

        foreach (var item in memory.Cart)
        {
            var product = await _context.Products.FindAsync(item.ProductId);
            if (product == null) continue;

            var lineTotal = product.PriceVnd * item.Quantity;

            order.Subtotal += lineTotal;

            order.Items.Add(new OrderItem
            {
                ProductId = product.Id,
                Quantity = item.Quantity,
                UnitPrice = product.PriceVnd,
                LineTotal = lineTotal
            });

            var inventory = await _context.Inventories
                .FirstOrDefaultAsync(i => i.ProductId == product.Id);

            if (inventory != null)
                inventory.Quantity -= item.Quantity;
        }

        order.Tax = 0;
        order.Total = order.Subtotal;

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        return order;
    }

    // ========================== PROMPT ==========================

    private string BuildPrompt(
        List<ChatMessage> history,
        List<Product> products,
        CustomerMemory memory)
    {
        var conversation = string.Join(" | ",
            history.Select(m => $"{m.Role}: {m.Content}"));

        var productJson = JsonSerializer.Serialize(
            products.Select(p => new
            {
                p.Id,
                p.Name,
                p.PriceVnd,
                p.Description
            }));

        var cartInfo = memory.Cart.Any()
            ? JsonSerializer.Serialize(memory.Cart)
            : "Chưa có món nào";

        return $""" Bạn là nhân viên quán ăn dễ mến, nói chuyện tự nhiên, lịch sự. QUY TẮC ỨNG XỬ: 1. Nếu Stage = AskingQuantity → Hỏi: "Anh/chị muốn mấy phần {memory.TempProductName} ạ?" 2. Nếu Stage = AskingForMore → Nói đã thêm vào giỏ, hỏi muốn dùng thêm món gì không. 3. Nếu Stage = ConfirmingOrder → Tóm tắt đơn hàng gọn gàng bằng HTML. → Sau đó hỏi khách muốn thanh toán bằng tiền mặt hay chuyển khoản. 4. Nếu Stage = AskingPaymentMethod → Nhắc khách chọn phương thức thanh toán. 5. Nếu Stage = Payment → Cảm ơn khách và xác nhận đơn. Luôn trả về HTML gọn: - <b> cho tên món - <br> để xuống dòng - Không dùng markdown - Không dùng ký tự | Giỏ hàng: {cartInfo} Stage: {memory.Stage} Lịch sử: {conversation} Menu: {productJson} """;
    }

    // ========================== SESSION ==========================

    private List<ChatMessage> GetHistory()
    {
        var data = HttpContext.Session.GetString(CHAT_HISTORY_KEY);
        return string.IsNullOrEmpty(data)
            ? new List<ChatMessage>()
            : JsonSerializer.Deserialize<List<ChatMessage>>(data) ?? new();
    }

    private void SaveHistory(List<ChatMessage> history)
    {
        HttpContext.Session.SetString(
            CHAT_HISTORY_KEY,
            JsonSerializer.Serialize(history));
    }

    private CustomerMemory GetCustomerMemory()
    {
        var data = HttpContext.Session.GetString(CUSTOMER_MEMORY_KEY);
        return string.IsNullOrEmpty(data)
            ? new CustomerMemory()
            : JsonSerializer.Deserialize<CustomerMemory>(data) ?? new();
    }

    private void SaveCustomerMemory(CustomerMemory memory)
    {
        HttpContext.Session.SetString(
            CUSTOMER_MEMORY_KEY,
            JsonSerializer.Serialize(memory));
    }
}