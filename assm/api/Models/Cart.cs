using Lab4.Models;
using System.ComponentModel.DataAnnotations;

namespace Lab4.Models
{
    public class Cart
    {
        public int Id { get; set; }                       // PK

        // Nếu có đăng nhập: lưu theo UserId (tuỳ bạn)
        public string? UserId { get; set; }

        // Nếu chưa có đăng nhập: lưu theo SessionId (khuyên dùng cho lab)
        [MaxLength(100)]
        public string? SessionId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public List<CartItem> Items { get; set; } = new();
    }
    public class CartItem
    {
        public int Id { get; set; }

        public int CartId { get; set; }
        public Cart Cart { get; set; } = null!;

        // ===== PRODUCT =====
        public int? ProductId { get; set; }
        public Product? Product { get; set; }

        // ===== COMBO =====
        public int? ComboId { get; set; }
        public Combo? Combo { get; set; }

        public int Quantity { get; set; } = 1;

        [MaxLength(50)]
        public string UnitPriceText { get; set; } = "";
    }
}
