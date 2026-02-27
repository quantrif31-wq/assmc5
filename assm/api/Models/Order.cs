using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Lab4.Models
{
    public class Order
    {
        [Key]
        public int Id { get; set; }
        // ===== USER =====
        [StringLength(100)]
        public string? UserId { get; set; }   // 👈 QUAN TRỌNG
                                              // ✅ THÊM SESSION ID CHO KHÁCH VÃNG LAI
        [StringLength(100)]
        public string? SessionId { get; set; }


        // ===== THÔNG TIN KHÁCH =====
        [Required, StringLength(80)]
        public string FullName { get; set; } = "";

        [Required, StringLength(20)]
        public string Phone { get; set; } = "";

        [Required, StringLength(150)]
        public string Address { get; set; } = "";

        [Required, StringLength(80)]
        public string City { get; set; } = "";

        [StringLength(120)]
        public string? Email { get; set; }

        [StringLength(300)]
        public string? Note { get; set; }

        // ===== THANH TOÁN =====
        [Required, StringLength(10)]
        public string PaymentMethod { get; set; } = "COD"; // COD / BANK

        // ===== TỔNG TIỀN =====
        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }

        // ===== PHÍ VẬN CHUYỂN =====
        public decimal ShippingFee { get; set; }

        [StringLength(80)]
        public string? District { get; set; }

        public decimal Total { get; set; }

        // ===== TRẠNG THÁI =====
        [Required, StringLength(20)]
        public string Status { get; set; } = "Pending";
        // NEW / CONFIRMED / DELIVERING / DONE / CANCELLED

        // ===== META =====
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ===== VNPAY INFO =====
        [StringLength(50)]
        public string? VnpTxnRef { get; set; }

        [StringLength(50)]
        public string? VnpTransactionNo { get; set; }

        [StringLength(10)]
        public string? VnpResponseCode { get; set; }

        public DateTime? VnpPayDate { get; set; }

        // ===== QUAN HỆ =====
        public List<OrderItem> Items { get; set; } = new();
    }
    public class OrderItem
    {
        [Key]
        public int Id { get; set; }

        // ===== FK =====
        public int OrderId { get; set; }

        [ForeignKey(nameof(OrderId))]
        public Order Order { get; set; } = null!;

        // ===== SẢN PHẨM =====
        public int? ProductId { get; set; }     // để trace
        public int? ComboId { get; set; }
        [Required, StringLength(120)]
        public string ProductName { get; set; } = "";

        // ===== GIÁ SNAPSHOT =====
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }

        public decimal LineTotal { get; set; }
    }
}
