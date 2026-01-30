namespace lab4.Models
{
    public class PurchaseOrder
    {
        public int Id { get; set; }
        public string Code { get; set; } = null!;
        public int SupplierId { get; set; }
        public DateTime OrderDate { get; set; } = DateTime.UtcNow;
        public string Status { get; set; } = "Draft";

        public Supplier Supplier { get; set; } = null!;
        public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
    }

}
