namespace lab4.Models
{
    public class GoodsReceipt
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public int PurchaseOrderId { get; set; }
        public DateTime ReceiptDate { get; set; } = DateTime.UtcNow;

        public PurchaseOrder PurchaseOrder { get; set; } = null!;
        public ICollection<GoodsReceiptItem> Items { get; set; } = new List<GoodsReceiptItem>();
    }

}
