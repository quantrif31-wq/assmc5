namespace lab4.Models
{
    public class GoodsReceiptViewModel
    {
        public List<GoodsReceiptItemVM> Items { get; set; } = new();
    }

    public class GoodsReceiptItemVM
    {
        public int ProductId { get; set; }
        public int Quantity { get; set; }
    }

}
