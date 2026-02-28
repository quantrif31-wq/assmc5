using System.ComponentModel.DataAnnotations;

namespace Lab4.Models
{
    public class CheckoutVM
    {
        // ===== THÔNG TIN KHÁCH =====
        [Required(AllowEmptyStrings = false, ErrorMessage = "Vui lòng nhập họ tên.")]
        [StringLength(80)]
        public string FullName { get; set; } = "";

        [Required(ErrorMessage = "Vui lòng nhập số điện thoại.")]
        [RegularExpression(@"^(0|\+84)[0-9]{9}$", ErrorMessage = "Số điện thoại không hợp lệ.")]
        [StringLength(20)]
        public string Phone { get; set; } = "";

        [Required(AllowEmptyStrings = false, ErrorMessage = "Vui lòng nhập địa chỉ giao.")]
        [StringLength(150)]
        public string Address { get; set; } = "";

        [Required(AllowEmptyStrings = false, ErrorMessage = "Vui lòng nhập Quận/Huyện - Tỉnh/TP.")]
        [StringLength(80)]
        public string City { get; set; } = "";

        // ===== QUẬN/HUYỆN + PHÍ SHIP =====
        public int DistrictId { get; set; }

        [EmailAddress(ErrorMessage = "Email không hợp lệ.")]
        [StringLength(120)]
        public string? Email { get; set; }

        
        

        [StringLength(300)]
        public string? Note { get; set; }

        // COD / BANK
        [Required(ErrorMessage = "Vui lòng chọn phương thức thanh toán.")]
        [StringLength(10)]
        public string PaymentMethod { get; set; } = "COD";

        // ===== GIỎ HÀNG / TÓM TẮT =====
        public List<CheckoutItemVM> Items { get; set; } = new();

        public decimal Subtotal { get; set; }
        public decimal Tax { get; set; }
        public decimal ShippingFee { get; set; }
        public decimal Total { get; set; }

        public string Currency { get; set; } = "VND";
        public string SubtotalText { get; set; } = "0";
        public string TaxText { get; set; } = "0";
        public string ShippingFeeText { get; set; } = "0";
        public string TotalText { get; set; } = "0";
        public string? VoucherCode { get; set; }
        // ===== VOUCHER =====
        public decimal DiscountAmount { get; set; } = 0;

        public string? DiscountText { get; set; }


    }

    public class CheckoutItemVM
    {
        public string Name { get; set; } = "";
        public int Qty { get; set; } = 1;
        public string PriceText { get; set; } = "";
        public bool Bold { get; set; } = false;
    }
}
