using Lab4.Models;

namespace lab4.Models
{
    public class InventoryLog
    {
        public int Id { get; set; }
        public int ProductId { get; set; }

        public int QuantityChange { get; set; } // + / -
        public int QuantityAfter { get; set; }

        public string ActionType { get; set; } = null!;
        // PURCHASE_RECEIPT | SALE | ADJUST

        public int ReferenceId { get; set; }
        public string ReferenceTable { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public Product Product { get; set; } = null!;
    }
}
