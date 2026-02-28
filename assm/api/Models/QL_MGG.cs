using lab4.Models;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lab4.Models.QL_MGG
{
    public class DiscountVoucher
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string Code { get; set; } = string.Empty;

        public decimal? DiscountAmount { get; set; }   // Giảm tiền cố định
        public double? DiscountPercent { get; set; }   // Giảm %

        public DateTime ExpiryDate { get; set; }

        public bool IsActive { get; set; } = true;

        // 🔥 Phần quan trọng: định danh user
        public string? UserId { get; set; }

        [ForeignKey("UserId")]
        public ApplicationUser? User { get; set; }

        public int UsageLimit { get; set; } = 1;
        public int UsedCount { get; set; } = 0;
    }
}