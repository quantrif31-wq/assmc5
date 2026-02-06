using Lab4.Models;

namespace lab4.Models
{
    public class PriceHistory
    {
        public int Id { get; set; }

        public int ProductId { get; set; }
        public Product Product { get; set; } = null!;

        public PriceType Type { get; set; } // PURCHASE / SALE

        public decimal Price { get; set; }

        public DateTime EffectiveFrom { get; set; }

        public string? Reason { get; set; }   // Lý do đổi giá
        public string? ReferenceCode { get; set; } // PO, GR, MANUAL

        public string? ChangedBy { get; set; } // UserId / Email
    }

    public enum PriceType
    {
        PURCHASE = 1,
        SALE = 2
    }
}