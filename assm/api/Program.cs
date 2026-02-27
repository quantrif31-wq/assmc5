using lab4.Models;
using Lab4.Data;
using Lab4.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;

    // ========== 2FA ==========
    // ❌ TẮT AUTHENTICATOR APP
    options.Tokens.AuthenticatorTokenProvider = null;

    // ✅ CHỈ DÙNG EMAIL OTP
    options.Tokens.EmailConfirmationTokenProvider = TokenOptions.DefaultEmailProvider;

    options.Lockout.AllowedForNewUsers = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders(); // ⚠️ BẮT BUỘC cho Email OTP


    // ================= GOOGLE LOGIN =================
builder.Services.AddAuthentication()
    .AddGoogle(options =>
    {
        options.ClientId = "960632346424-lanmhm44ij4ioce1bt51jcnokmd75ov4.apps.googleusercontent.com";
        options.ClientSecret = "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";
    });

// ================= EMAIL =================
builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddTransient<IEmailSender, EmailSender>();

// ================= VNPAY =================
builder.Services.Configure<Lab4.Models.VnPayConfig>(
    builder.Configuration.GetSection("VnPay"));
builder.Services.AddScoped<Lab4.Services.IVnPayService, Lab4.Services.VnPayService>();

// Cấu hình Authorization kết hợp Role + Claim
// Admin.Access = full quyền (bao gồm tất cả chức năng)
builder.Services.AddAuthorization(options =>
{
    // Admin: role StoreManager HOẶC claim Admin.Access
    options.AddPolicy("AdminOnly", p =>
        p.RequireAssertion(ctx =>
            ctx.User.IsInRole("StoreManager") ||
            ctx.User.HasClaim("Permission", "Admin.Access")));

    // Sản phẩm: role StoreManager HOẶC claim Product.Create HOẶC Admin.Access (full)
    options.AddPolicy("ManageProduct", p =>
        p.RequireAssertion(ctx =>
            ctx.User.IsInRole("StoreManager") ||
            ctx.User.HasClaim("Permission", "Admin.Access") ||
            ctx.User.HasClaim("Permission", "Product.Create")));

    // Kho: role StoreManager HOẶC claim Inventory.Manage HOẶC Admin.Access (full)
    options.AddPolicy("ManageInventory", p =>
        p.RequireAssertion(ctx =>
            ctx.User.IsInRole("StoreManager") ||
            ctx.User.HasClaim("Permission", "Admin.Access") ||
            ctx.User.HasClaim("Permission", "Inventory.Manage")));

    // Đơn hàng: role StoreManager/KitchenStaff HOẶC claim Order.Manage HOẶC Admin.Access (full)
    options.AddPolicy("ManageOrder", p =>
        p.RequireAssertion(ctx =>
            ctx.User.IsInRole("StoreManager") ||
            ctx.User.IsInRole("KitchenStaff") ||
            ctx.User.HasClaim("Permission", "Admin.Access") ||
            ctx.User.HasClaim("Permission", "Order.Manage")));

    // Báo cáo: role StoreManager/Accountant HOẶC claim Report.View HOẶC Admin.Access (full)
    options.AddPolicy("ViewReports", p =>
        p.RequireAssertion(ctx =>
            ctx.User.IsInRole("StoreManager") ||
            ctx.User.IsInRole("Accountant") ||
            ctx.User.HasClaim("Permission", "Admin.Access") ||
            ctx.User.HasClaim("Permission", "Report.View")));

    // Policy cho UserClaimsController (giống AdminOnly)
    options.AddPolicy("AdminViewProductPolicy", p =>
        p.RequireAssertion(ctx =>
            ctx.User.IsInRole("StoreManager") ||
            ctx.User.HasClaim("Permission", "Admin.Access")));
});
builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();
// Session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    // 🔥 BẮT BUỘC để Vue (5173) nói chuyện với MVC (7045)
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVue", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.None;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});
var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
// Khởi tạo Scope để lấy UserManager và chạy Seeding Data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    
    // Gọi hàm Initialize đã tạo ở Bước 1
    await DbInitializer.Initialize(userManager, roleManager);

    // ===== SEED SAMPLE ORDERS cho báo cáo doanh thu =====
    var db = services.GetRequiredService<ApplicationDbContext>();
    if (!db.Orders.Any(o => o.Status == "Completed"))
    {
        var orders = new List<Lab4.Models.Order>
        {
            new() { FullName="Nguyễn Văn An",   Phone="0901234567", Address="123 Lê Lợi",           City="TP.HCM",   Email="an@mail.com",    PaymentMethod="COD",  Subtotal=150000, Tax=15000, Total=165000, Status="Completed",  CreatedAt=new DateTime(2026,2,18,10,30,0,DateTimeKind.Utc) },
            new() { FullName="Trần Thị Bình",   Phone="0912345678", Address="45 Nguyễn Huệ",        City="TP.HCM",   Email="binh@mail.com",  PaymentMethod="BANK", Subtotal=100000, Tax=10000, Total=110000, Status="Completed",  CreatedAt=new DateTime(2026,2,19,14,0,0,DateTimeKind.Utc) },
            new() { FullName="Lê Hoàng Cường",  Phone="0987654321", Address="78 Trần Hưng Đạo",     City="Đà Nẵng",  Email="cuong@mail.com", PaymentMethod="COD",  Subtotal=200000, Tax=20000, Total=220000, Status="Completed",  CreatedAt=new DateTime(2026,2,20,9,15,0,DateTimeKind.Utc) },
            new() { FullName="Phạm Minh Đức",   Phone="0976543210", Address="12 Hai Bà Trưng",      City="Hà Nội",   Email="duc@mail.com",   PaymentMethod="BANK", Subtotal=140000, Tax=14000, Total=154000, Status="Completed",  CreatedAt=new DateTime(2026,2,21,11,45,0,DateTimeKind.Utc) },
            new() { FullName="Ngô Thị Em",      Phone="0909876543", Address="56 Pasteur",            City="TP.HCM",                           PaymentMethod="COD",  Subtotal=50000,  Tax=5000,  Total=55000,  Status="Cancelled",  CreatedAt=new DateTime(2026,2,21,16,0,0,DateTimeKind.Utc) },
            new() { FullName="Hoàng Văn Phú",   Phone="0918765432", Address="90 Bà Triệu",          City="Hà Nội",   Email="phu@mail.com",   PaymentMethod="COD",  Subtotal=250000, Tax=25000, Total=275000, Status="Completed",  CreatedAt=new DateTime(2026,2,22,8,30,0,DateTimeKind.Utc) },
            new() { FullName="Vũ Thị Giang",    Phone="0934567890", Address="34 Lý Tự Trọng",       City="TP.HCM",                           PaymentMethod="BANK", Subtotal=190000, Tax=19000, Total=209000, Status="Completed",  CreatedAt=new DateTime(2026,2,23,12,0,0,DateTimeKind.Utc) },
            new() { FullName="Đỗ Quang Huy",    Phone="0945678901", Address="67 Điện Biên Phủ",     City="TP.HCM",   Email="huy@mail.com",   PaymentMethod="COD",  Subtotal=100000, Tax=10000, Total=110000, Status="Completed",  CreatedAt=new DateTime(2026,2,24,10,0,0,DateTimeKind.Utc) },
            new() { FullName="Bùi Thị Khanh",   Phone="0956789012", Address="23 Nguyễn Trãi",       City="TP.HCM",                           PaymentMethod="COD",  Subtotal=180000, Tax=18000, Total=198000, Status="Pending",    CreatedAt=new DateTime(2026,2,25,9,0,0,DateTimeKind.Utc) },
            new() { FullName="Trịnh Văn Lâm",   Phone="0967890123", Address="45 Cách Mạng Tháng 8", City="TP.HCM",   Email="lam@mail.com",   PaymentMethod="BANK", Subtotal=140000, Tax=14000, Total=154000, Status="Delivering", CreatedAt=new DateTime(2026,2,25,15,30,0,DateTimeKind.Utc) },
            new() { FullName="Mai Thị Ngọc",    Phone="0978901234", Address="89 Võ Văn Tần",        City="TP.HCM",                           PaymentMethod="COD",  Subtotal=300000, Tax=30000, Total=330000, Status="Completed",  CreatedAt=new DateTime(2026,2,26,8,0,0,DateTimeKind.Utc) },
            new() { FullName="Lý Quốc Oai",     Phone="0989012345", Address="12 Lê Duẩn",           City="Đà Nẵng",  Email="oai@mail.com",   PaymentMethod="COD",  Subtotal=100000, Tax=10000, Total=110000, Status="Pending",    CreatedAt=new DateTime(2026,2,26,11,0,0,DateTimeKind.Utc) },
        };
        db.Orders.AddRange(orders);
        db.SaveChanges();

        // Map product names → seeded order items
        var productNames = new[] { "Bún Bò Huế","Phở Bò","Phở Gà","Mỳ Quảng","Hủ Tiếu Nam Vang","Cơm Tấm" };
        var productPrices = new[] { 50000m, 50000m, 50000m, 50000m, 50000m, 90000m };

        var items = new List<Lab4.Models.OrderItem>
        {
            // Order 0 (Completed): Bún Bò Huế x2 + Phở Bò x1
            new() { OrderId=orders[0].Id, ProductId=1, ProductName="Bún Bò Huế",       UnitPrice=50000, Quantity=2, LineTotal=100000 },
            new() { OrderId=orders[0].Id, ProductId=2, ProductName="Phở Bò",            UnitPrice=50000, Quantity=1, LineTotal=50000 },
            // Order 1 (Completed): Phở Gà x2
            new() { OrderId=orders[1].Id, ProductId=3, ProductName="Phở Gà",            UnitPrice=50000, Quantity=2, LineTotal=100000 },
            // Order 2 (Completed): Mỳ Quảng + Cơm Tấm + Bún Bò Huế
            new() { OrderId=orders[2].Id, ProductId=4, ProductName="Mỳ Quảng",          UnitPrice=50000, Quantity=1, LineTotal=50000 },
            new() { OrderId=orders[2].Id, ProductId=6, ProductName="Cơm Tấm",           UnitPrice=90000, Quantity=1, LineTotal=90000 },
            new() { OrderId=orders[2].Id, ProductId=1, ProductName="Bún Bò Huế",        UnitPrice=50000, Quantity=1, LineTotal=50000 },
            // Order 3 (Completed): Hủ Tiếu + Cơm Tấm
            new() { OrderId=orders[3].Id, ProductId=5, ProductName="Hủ Tiếu Nam Vang",  UnitPrice=50000, Quantity=1, LineTotal=50000 },
            new() { OrderId=orders[3].Id, ProductId=6, ProductName="Cơm Tấm",           UnitPrice=90000, Quantity=1, LineTotal=90000 },
            // Order 4 (Cancelled): Phở Bò x1
            new() { OrderId=orders[4].Id, ProductId=2, ProductName="Phở Bò",            UnitPrice=50000, Quantity=1, LineTotal=50000 },
            // Order 5 (Completed): Bún Bò Huế x3 + Phở Bò + Phở Gà
            new() { OrderId=orders[5].Id, ProductId=1, ProductName="Bún Bò Huế",        UnitPrice=50000, Quantity=3, LineTotal=150000 },
            new() { OrderId=orders[5].Id, ProductId=2, ProductName="Phở Bò",            UnitPrice=50000, Quantity=1, LineTotal=50000 },
            new() { OrderId=orders[5].Id, ProductId=3, ProductName="Phở Gà",            UnitPrice=50000, Quantity=1, LineTotal=50000 },
            // Order 6 (Completed): Cơm Tấm + Mỳ Quảng x2
            new() { OrderId=orders[6].Id, ProductId=6, ProductName="Cơm Tấm",           UnitPrice=90000, Quantity=1, LineTotal=90000 },
            new() { OrderId=orders[6].Id, ProductId=4, ProductName="Mỳ Quảng",          UnitPrice=50000, Quantity=2, LineTotal=100000 },
            // Order 7 (Completed): Phở Bò x2
            new() { OrderId=orders[7].Id, ProductId=2, ProductName="Phở Bò",            UnitPrice=50000, Quantity=2, LineTotal=100000 },
            // Order 8 (Pending): Bún Bò Huế x2 + Cơm Tấm
            new() { OrderId=orders[8].Id, ProductId=1, ProductName="Bún Bò Huế",        UnitPrice=50000, Quantity=2, LineTotal=100000 },
            new() { OrderId=orders[8].Id, ProductId=6, ProductName="Cơm Tấm",           UnitPrice=90000, Quantity=1, LineTotal=90000 },
            // Order 9 (Delivering): Hủ Tiếu + Cơm Tấm
            new() { OrderId=orders[9].Id, ProductId=5, ProductName="Hủ Tiếu Nam Vang",  UnitPrice=50000, Quantity=1, LineTotal=50000 },
            new() { OrderId=orders[9].Id, ProductId=6, ProductName="Cơm Tấm",           UnitPrice=90000, Quantity=1, LineTotal=90000 },
            // Order 10 (Completed): Phở Gà x3 + Bún Bò Huế x3
            new() { OrderId=orders[10].Id, ProductId=3, ProductName="Phở Gà",           UnitPrice=50000, Quantity=3, LineTotal=150000 },
            new() { OrderId=orders[10].Id, ProductId=1, ProductName="Bún Bò Huế",       UnitPrice=50000, Quantity=3, LineTotal=150000 },
            // Order 11 (Pending): Phở Bò + Mỳ Quảng
            new() { OrderId=orders[11].Id, ProductId=2, ProductName="Phở Bò",           UnitPrice=50000, Quantity=1, LineTotal=50000 },
            new() { OrderId=orders[11].Id, ProductId=4, ProductName="Mỳ Quảng",         UnitPrice=50000, Quantity=1, LineTotal=50000 },
        };
        db.OrderItems.AddRange(items);
        db.SaveChanges();
    }

    // ===== SEED SAMPLE SUPPLIERS =====
    if (!db.Set<lab4.Models.Supplier>().Any())
    {
        var suppliers = new List<lab4.Models.Supplier>
        {
            new() { Code = "NCC01", Name = "Công ty TNHH Thực phẩm Sài Gòn",     Phone = "028-3822-1100", Email = "contact@tpsaigon.vn",     Rating = 5, DefaultDiscount = 5,  IsActive = true },
            new() { Code = "NCC02", Name = "Nông trại Hữu cơ Đà Lạt",            Phone = "0263-382-5566", Email = "info@dalat-organic.vn",    Rating = 4, DefaultDiscount = 3,  IsActive = true },
            new() { Code = "NCC03", Name = "Công ty CP Hải sản Nha Trang",        Phone = "0258-352-1234", Email = "sales@nhatrang-seafood.vn",Rating = 4, DefaultDiscount = 4,  IsActive = true },
            new() { Code = "NCC04", Name = "Cơ sở Gia vị Phú Quốc",              Phone = "0297-384-6789", Email = "giavi@phuquoc-spice.vn",   Rating = 5, DefaultDiscount = 2,  IsActive = true },
            new() { Code = "NCC05", Name = "Công ty TNHH Thịt sạch Vissan",      Phone = "028-3855-2277", Email = "order@vissan.com.vn",      Rating = 4, DefaultDiscount = 3,  IsActive = true },
            new() { Code = "NCC06", Name = "HTX Rau sạch Củ Chi",                Phone = "028-3794-0088", Email = "rausach@cuchi-farm.vn",     Rating = 3, DefaultDiscount = 5,  IsActive = true },
        };
        db.Set<lab4.Models.Supplier>().AddRange(suppliers);
        db.SaveChanges();
    }

    // ===== SEED SAMPLE AUDIT LOGS =====
    if (!db.AuditLogs.Any())
    {
        var auditLogs = new List<Lab4.Models.AuditLog>
        {
            new() { Action="OrderStatusChanged", EntityType="Order", EntityId="1",  OldValue="Pending",   NewValue="Completed",  Description="Đơn hàng #1: Pending → Completed",                                          PerformedBy="admin@food.com",  PerformedAt=new DateTime(2026,2,18,11,0,0,DateTimeKind.Utc) },
            new() { Action="OrderStatusChanged", EntityType="Order", EntityId="5",  OldValue="Pending",   NewValue="Cancelled",  Description="Đơn hàng #5: Pending → Cancelled",                                          PerformedBy="staff01@food.com", PerformedAt=new DateTime(2026,2,21,16,30,0,DateTimeKind.Utc) },
            new() { Action="PriceChanged",       EntityType="Product", EntityId="1", OldValue="45,000đ",  NewValue="50,000đ",    Description="Đổi giá \"Bún Bò Huế\": 45,000đ → 50,000đ. Lý do: Tăng giá nguyên liệu",      PerformedBy="admin@food.com",  PerformedAt=new DateTime(2026,2,19,9,0,0,DateTimeKind.Utc) },
            new() { Action="OrderStatusChanged", EntityType="Order", EntityId="2",  OldValue="Pending",   NewValue="Completed",  Description="Đơn hàng #2: Pending → Completed",                                          PerformedBy="admin@food.com",  PerformedAt=new DateTime(2026,2,19,15,0,0,DateTimeKind.Utc) },
            new() { Action="PriceChanged",       EntityType="Product", EntityId="6", OldValue="80,000đ",  NewValue="90,000đ",    Description="Đổi giá \"Cơm Tấm\": 80,000đ → 90,000đ. Lý do: Điều chỉnh theo thị trường",   PerformedBy="admin@food.com",  PerformedAt=new DateTime(2026,2,20,8,0,0,DateTimeKind.Utc) },
            new() { Action="OrderStatusChanged", EntityType="Order", EntityId="3",  OldValue="Preparing", NewValue="Delivering", Description="Đơn hàng #3: Preparing → Delivering",                                       PerformedBy="staff01@food.com", PerformedAt=new DateTime(2026,2,20,10,0,0,DateTimeKind.Utc) },
            new() { Action="OrderStatusChanged", EntityType="Order", EntityId="3",  OldValue="Delivering",NewValue="Completed",  Description="Đơn hàng #3: Delivering → Completed",                                       PerformedBy="staff01@food.com", PerformedAt=new DateTime(2026,2,20,11,0,0,DateTimeKind.Utc) },
            new() { Action="OrderStatusChanged", EntityType="Order", EntityId="7",  OldValue="Pending",   NewValue="Completed",  Description="Đơn hàng #7: Pending → Completed",                                          PerformedBy="admin@food.com",  PerformedAt=new DateTime(2026,2,23,13,0,0,DateTimeKind.Utc) },
            new() { Action="PriceChanged",       EntityType="Product", EntityId="3", OldValue="48,000đ",  NewValue="50,000đ",    Description="Đổi giá \"Phở Gà\": 48,000đ → 50,000đ. Lý do: Cập nhật giá mới",              PerformedBy="admin@food.com",  PerformedAt=new DateTime(2026,2,24,8,30,0,DateTimeKind.Utc) },
            new() { Action="OrderStatusChanged", EntityType="Order", EntityId="8",  OldValue="Pending",   NewValue="Completed",  Description="Đơn hàng #8: Pending → Completed",                                          PerformedBy="staff01@food.com", PerformedAt=new DateTime(2026,2,24,11,0,0,DateTimeKind.Utc) },
        };
        db.AuditLogs.AddRange(auditLogs);
        db.SaveChanges();
    }
}
app.UseHttpsRedirection();
app.UseRouting();
app.UseCors("AllowVue");
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();
app.MapStaticAssets();


app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Welcome}/{id?}")
    .WithStaticAssets();
app.MapControllers();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
