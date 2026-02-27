using lab4.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace Lab4.Data
{
    public class DbInitializer
    {
        public static async Task Initialize(UserManager<ApplicationUser> userManager, RoleManager<IdentityRole> roleManager)
        {
            async Task EnsureUser(string email, string password, params string[] permissions)
            {
                var user = await userManager.FindByEmailAsync(email);
                bool isNew = false;
                if (user == null)
                {
                    isNew = true;
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true
                    };
                    await userManager.CreateAsync(user, password);
                }

                // Chỉ gán quyền mặc định khi tạo user MỚI
                // Nếu user đã tồn tại → giữ nguyên quyền đã được admin chỉnh sửa
                if (isNew)
                {
                    foreach (var p in permissions)
                    {
                        await userManager.AddClaimAsync(user, new Claim("Permission", p));
                    }
                }
            }

            async Task EnsureRole(string roleName)
            {
                if (!await roleManager.RoleExistsAsync(roleName))
                {
                    await roleManager.CreateAsync(new IdentityRole(roleName));
                }
            }

            async Task EnsureUserWithRole(string email, string password, string roleName)
            {
                var user = await userManager.FindByEmailAsync(email);
                if (user == null)
                {
                    user = new ApplicationUser
                    {
                        UserName = email,
                        Email = email,
                        EmailConfirmed = true
                    };
                    await userManager.CreateAsync(user, password);
                }

                if (!await userManager.IsInRoleAsync(user, roleName))
                {
                    await userManager.AddToRoleAsync(user, roleName);
                }
            }

            // Tạo các Roles
            await EnsureRole("StoreManager");
            await EnsureRole("Accountant");
            await EnsureRole("KitchenStaff");

            await EnsureUser(
                "admin@poly.edu.vn",
                "Admin@123",
                "Admin.Access",
                "Product.Create",
                "Inventory.Manage",
                "Order.Manage"
            );

            await EnsureUser(
                "sales@poly.edu.vn",
                "Sales@123",
                "Product.Create",
                "Order.Manage"
            );

            // Tạo các User mẫu cho từng Role + gán Permission claims tương ứng
            await EnsureUserWithRole("manager@poly.edu.vn", "Manager@123", "StoreManager");
            await EnsureUserWithRole("accountant@poly.edu.vn", "Accountant@123", "Accountant");
            await EnsureUserWithRole("kitchen@poly.edu.vn", "Kitchen@123", "KitchenStaff");

            // Gán Permission claims cho các user có role
            // Manager: tất cả quyền
            await EnsureUser(
                "manager@poly.edu.vn",
                "Manager@123",
                "Admin.Access",
                "Product.Create",
                "Inventory.Manage",
                "Order.Manage",
                "Report.View"
            );

            // Accountant: xem báo cáo
            await EnsureUser(
                "accountant@poly.edu.vn",
                "Accountant@123",
                "Report.View"
            );

            // Kitchen: quản lý đơn hàng
            await EnsureUser(
                "kitchen@poly.edu.vn",
                "Kitchen@123",
                "Order.Manage"
            );
        }
    }
}