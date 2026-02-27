using System.ComponentModel.DataAnnotations;

namespace Lab4.Models
{
    public class AuditLog
    {
        [Key]
        public int Id { get; set; }

        /// <summary>Loại hành động: OrderStatusChanged, PriceChanged, …</summary>
        [Required, StringLength(50)]
        public string Action { get; set; } = "";

        /// <summary>Loại đối tượng: Order, Product, …</summary>
        [Required, StringLength(50)]
        public string EntityType { get; set; } = "";

        /// <summary>ID của đối tượng bị thay đổi</summary>
        [StringLength(50)]
        public string EntityId { get; set; } = "";

        /// <summary>Giá trị cũ</summary>
        [StringLength(500)]
        public string? OldValue { get; set; }

        /// <summary>Giá trị mới</summary>
        [StringLength(500)]
        public string? NewValue { get; set; }

        /// <summary>Mô tả chi tiết</summary>
        [StringLength(500)]
        public string Description { get; set; } = "";

        /// <summary>Email / tên tài khoản thực hiện</summary>
        [StringLength(100)]
        public string PerformedBy { get; set; } = "";

        /// <summary>Thời gian thực hiện</summary>
        public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    }
}
