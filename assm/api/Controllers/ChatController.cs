using lab4.Models;
using Lab4.Data;
using Lab4.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

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

    [HttpPost("Ask")]
    public async Task<IActionResult> Ask([FromBody] ChatRequest req)
    {
        if (req == null || string.IsNullOrWhiteSpace(req.message))
            return Json(new { reply = "Bạn cần hỗ trợ gì nè? 😊" });

        var userMessage = req.message.Trim();

        // ================= SHORT TERM MEMORY =================
        var history = GetHistory();
        history.Add(new ChatMessage
        {
            Role = "user",
            Content = userMessage
        });

        // giữ 16 message gần nhất (8 lượt hỏi đáp)
        if (history.Count > 16)
            history = history.Skip(history.Count - 16).ToList();

        // ================= STRUCTURED MEMORY =================
        var customerMemory = GetCustomerMemory();
        UpdateCustomerMemory(customerMemory, userMessage);
        SaveCustomerMemory(customerMemory);

        // ================= PRODUCT =================
        var products = await GetRelevantProducts(userMessage);

        // ================= BUILD PROMPT =================
        var prompt = BuildPrompt(history, products, customerMemory);

        var client = _httpFactory.CreateClient();
        client.Timeout = TimeSpan.FromSeconds(20);

        var payload = JsonSerializer.Serialize(new
        {
            message = prompt
        });

        var content = new StringContent(
            payload,
            Encoding.UTF8,
            "application/json"
        );

        HttpResponseMessage response;

        try
        {
            response = await client.PostAsync(
                "http://127.0.0.1:5678/webhook/chatbot",
                content
            );
        }
        catch (Exception ex)
        {
            return Json(new { reply = "Exception: " + ex.Message });
        }

        var raw = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(raw))
            return Json(new { reply = "Bò hơi mất kết nối 😅" });

        ChatResponse? result;

        try
        {
            result = JsonSerializer.Deserialize<ChatResponse>(raw);
        }
        catch
        {
            return Json(new { reply = "AI trả dữ liệu không hợp lệ." });
        }

        if (result == null || string.IsNullOrWhiteSpace(result.reply))
            return Json(new { reply = "Bò chưa hiểu lắm 😅" });

        // ================= SAVE ASSISTANT MESSAGE =================
        history.Add(new ChatMessage
        {
            Role = "assistant",
            Content = result.reply
        });

        SaveHistory(history);

        return Json(result);
    }

    // =========================================================
    // ================= SHORT TERM MEMORY =====================
    // =========================================================

    private List<ChatMessage> GetHistory()
    {
        var data = HttpContext.Session.GetString(CHAT_HISTORY_KEY);

        if (string.IsNullOrEmpty(data))
            return new List<ChatMessage>();

        return JsonSerializer.Deserialize<List<ChatMessage>>(data)
               ?? new List<ChatMessage>();
    }

    private void SaveHistory(List<ChatMessage> history)
    {
        HttpContext.Session.SetString(
            CHAT_HISTORY_KEY,
            JsonSerializer.Serialize(history)
        );
    }

    // =========================================================
    // ================= STRUCTURED MEMORY =====================
    // =========================================================

    private CustomerMemory GetCustomerMemory()
    {
        var data = HttpContext.Session.GetString(CUSTOMER_MEMORY_KEY);

        if (string.IsNullOrEmpty(data))
            return new CustomerMemory();

        return JsonSerializer.Deserialize<CustomerMemory>(data)
               ?? new CustomerMemory();
    }

    private void SaveCustomerMemory(CustomerMemory memory)
    {
        HttpContext.Session.SetString(
            CUSTOMER_MEMORY_KEY,
            JsonSerializer.Serialize(memory)
        );
    }

    private void UpdateCustomerMemory(CustomerMemory memory, string message)
    {
        var lower = message.ToLower();

        // Size
        if (lower.Contains("size m")) memory.SelectedSize = "M";
        if (lower.Contains("size l")) memory.SelectedSize = "L";
        if (lower.Contains("size s")) memory.SelectedSize = "S";

        // Màu
        if (lower.Contains("màu đen")) memory.SelectedColor = "Đen";
        if (lower.Contains("màu trắng")) memory.SelectedColor = "Trắng";
        if (lower.Contains("màu đỏ")) memory.SelectedColor = "Đỏ";

        // Quan tâm sản phẩm
        var product = _context.Products
            .FirstOrDefault(p =>
                lower.Contains(p.Name.ToLower()));

        if (product != null)
            memory.InterestedProduct = product.Name;

        // Ngân sách
        if (lower.Contains("dưới 500"))
            memory.BudgetRange = "Dưới 500k";

        // Chốt đơn
        if (lower.Contains("chốt") ||
            lower.Contains("lấy luôn") ||
            lower.Contains("mua luôn"))
        {
            memory.ReadyToBuy = true;
        }
    }

    // =========================================================
    // ================= PRODUCT FILTER ========================
    // =========================================================

    private async Task<List<Product>> GetRelevantProducts(string message)
    {
        var keyword = message.ToLower();

        var query = _context.Products
            .Where(p => p.IsActive);

        query = query.Where(p =>
            p.Name.ToLower().Contains(keyword));

        var products = await query
            .OrderBy(p => p.SortOrder)
            .Take(3)
            .ToListAsync();

        if (!products.Any())
        {
            products = await _context.Products
                .Where(p => p.IsActive)
                .OrderBy(p => p.SortOrder)
                .Take(3)
                .ToListAsync();
        }

        return products;
    }

    // =========================================================
    // ================= PROMPT BUILDER ========================
    // =========================================================

    private string BuildPrompt(
        List<ChatMessage> history,
        List<Product> products,
        CustomerMemory memory)
    {
        var conversation = string.Join(" | ",
            history.Select(m => $"{m.Role}: {m.Content}")
        );

        var productList = string.Join(" | ",
            products.Select(p => $"{p.Name} ({p.PriceVnd:N0} VNĐ)")
        );

        var memoryInfo =
            $"Khách quan tâm: {memory.InterestedProduct ?? "chưa rõ"}. " +
            $"Màu đã chọn: {memory.SelectedColor ?? "chưa chọn"}. " +
            $"Size đã chọn: {memory.SelectedSize ?? "chưa chọn"}. " +
            $"Ngân sách: {memory.BudgetRange ?? "chưa rõ"}. " +
            $"Sẵn sàng mua: {(memory.ReadyToBuy ? "Có" : "Chưa")}.";

        var isFirstMessage = history.Count <= 1;

        var greetingRule = isFirstMessage
            ? "Nếu là tin đầu có thể chào nhẹ một câu."
            : "Không được chào lại.";

        var prompt =
            "Bạn là nhân viên bán hàng thân thiện. " +
            "Nói chuyện tự nhiên như chat Facebook. " +
            "Trả lời ngắn gọn đúng trọng tâm. " +
            "Không bịa thông tin. " +
            greetingRule + " " +
            "Thông tin khách: " + memoryInfo + " " +
            "Lịch sử gần đây: " + conversation + ". " +
            "Sản phẩm hiện có: " + productList + ". " +
            "Không hỏi lại thông tin khách đã cung cấp.";

        return prompt;
    }
}