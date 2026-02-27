using System.ComponentModel.DataAnnotations.Schema;

namespace Lab4.Models
{
    [NotMapped]
    public class CartViewModel
    {
        public string? StoreName { get; set; } // ví dụ: "Bun cha"
        public List<CartItemViewModel> Items { get; set; } = new();

        public string SubtotalText { get; set; } = "0 SEK";
        public string TaxText { get; set; } = "0 SEK";
        public string TotalText { get; set; } = "0 SEK";
    }
    [NotMapped]
    public class CartItemViewModel
    {
        public int? ProductId { get; set; }
        public string Name { get; set; } = "";
        public string ImageUrl { get; set; } = "";
        public int Quantity { get; set; }
        public string LineTotalText { get; set; } = "0 SEK";
        
        public int? ComboId { get; set; }

        public bool IsCombo => ComboId != null;
    }
}
