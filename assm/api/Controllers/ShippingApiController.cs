using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Lab4.ApiControllers
{
    [Route("api/shipping")]
    [ApiController]
    public class ShippingApiController : ControllerBase
    {
        // ===== BẢNG PHÍ VẬN CHUYỂN THEO QUẬN/HUYỆN =====
        private static readonly List<DistrictShipping> _districts = new()
        {
            // Trung tâm - 15.000₫
            new(1,  "Quận 1",      15000),
            new(2,  "Quận 3",      15000),
            new(3,  "Quận 5",      15000),
            new(4,  "Quận 10",     15000),

            // Vùng lân cận - 20.000₫
            new(5,  "Quận 4",      20000),
            new(6,  "Quận 6",      20000),
            new(7,  "Quận 7",      20000),
            new(8,  "Quận 8",      20000),
            new(9,  "Quận 11",     20000),
            new(10, "Tân Bình",    20000),
            new(11, "Phú Nhuận",   20000),
            new(12, "Bình Thạnh",  20000),

            // Ngoại thành gần - 30.000₫
            new(13, "Quận 9",      30000),
            new(14, "Quận 12",     30000),
            new(15, "Thủ Đức",     30000),
            new(16, "Bình Tân",    30000),
            new(17, "Gò Vấp",     30000),
            new(18, "Tân Phú",     30000),

            // Ngoại thành xa - 40.000₫
            new(19, "Hóc Môn",     40000),
            new(20, "Củ Chi",      40000),
            new(21, "Bình Chánh",  40000),
            new(22, "Nhà Bè",      40000),
            new(23, "Cần Giờ",     40000),
        };

        // =========================
        // LẤY DANH SÁCH QUẬN + PHÍ
        // =========================
        [HttpGet("districts")]
        public IActionResult GetDistricts()
        {
            var result = _districts.Select(d => new
            {
                d.Id,
                d.Name,
                d.Fee,
                FeeText = FormatVnd(d.Fee)
            });

            return Ok(result);
        }

        // =========================
        // LẤY PHÍ SHIP THEO ID QUẬN
        // =========================
        [HttpGet("fee")]
        public IActionResult GetFee([FromQuery] int districtId)
        {
            var district = _districts.FirstOrDefault(d => d.Id == districtId);

            if (district == null)
                return NotFound(new { error = "Không tìm thấy quận/huyện." });

            return Ok(new
            {
                districtId  = district.Id,
                districtName = district.Name,
                fee         = district.Fee,
                feeText     = FormatVnd(district.Fee)
            });
        }

        // ===== HELPER =====
        private static string FormatVnd(decimal amount)
        {
            var vi = CultureInfo.GetCultureInfo("vi-VN");
            return string.Format(vi, "{0:N0} ₫", amount);
        }

        // ===== STATIC HELPER: để CartController dùng lại =====
        public static decimal GetShippingFee(int districtId)
        {
            var d = _districts.FirstOrDefault(x => x.Id == districtId);
            return d?.Fee ?? 0;
        }

        public static string? GetDistrictName(int districtId)
        {
            return _districts.FirstOrDefault(x => x.Id == districtId)?.Name;
        }

        // ===== DTO nội bộ =====
        private class DistrictShipping
        {
            public int Id { get; }
            public string Name { get; }
            public decimal Fee { get; }

            public DistrictShipping(int id, string name, decimal fee)
            {
                Id = id;
                Name = name;
                Fee = fee;
            }
        }
    }
}
