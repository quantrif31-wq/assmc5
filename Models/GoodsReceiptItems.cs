using Lab4.Models;

namespace lab4.Models
{
    public class GoodsReceiptItem
    {
        public int Id { get; set; }
        public int GoodsReceiptId { get; set; }
        public int ProductId { get; set; }

        public int QuantityReceived { get; set; }
        public decimal UnitCost { get; set; }

        public GoodsReceipt GoodsReceipt { get; set; } = null!;
        public Product Product { get; set; } = null!;
    }

}
