using System.ComponentModel.DataAnnotations;

namespace Lab4.Models
{
    public class Combo
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        public string? Description { get; set; }

        [Range(0, double.MaxValue)]
        public decimal PriceVnd { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsActive { get; set; } = true;

        public ICollection<ComboItem> ComboItems { get; set; } = new List<ComboItem>();
    }
}