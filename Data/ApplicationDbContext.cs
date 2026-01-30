
using lab4.Models;
using Lab4.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System.Reflection.Emit;

namespace Lab4.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options) { }
    public DbSet<Product> Products { get; set; } = default!;
    public DbSet<Cart> Carts => Set<Cart>();
    public DbSet<CartItem> CartItems => Set<CartItem>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<Inventory> Inventories => Set<Inventory>();
    public DbSet<InventoryLog> InventoryLogs => Set<InventoryLog>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptItem> GoodsReceiptItems => Set<GoodsReceiptItem>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<Inventory>()
    .HasOne(i => i.Product)
    .WithOne(p => p.Inventory)
    .HasForeignKey<Inventory>(i => i.ProductId)
    .OnDelete(DeleteBehavior.Cascade);
        builder.Entity<Inventory>()
            .Property(x => x.UpdatedAt)
            .HasDefaultValueSql("GETUTCDATE()");


        builder.Entity<Inventory>().HasData(
    new Inventory { Id = 1, ProductId = 1, Quantity = 50 },
    new Inventory { Id = 2, ProductId = 2, Quantity = 40 },
    new Inventory { Id = 3, ProductId = 3, Quantity = 35 },
    new Inventory { Id = 4, ProductId = 4, Quantity = 20 },
    new Inventory { Id = 5, ProductId = 5, Quantity = 25 },
    new Inventory { Id = 6, ProductId = 6, Quantity = 10 }
);



        builder.Entity<Order>(e =>
        {
            e.Property(x => x.Subtotal).HasPrecision(18, 2);
            e.Property(x => x.Tax).HasPrecision(18, 2);
            e.Property(x => x.Total).HasPrecision(18, 2);

            e.HasMany(o => o.Items)
    .WithOne(i => i.Order)
    .HasForeignKey(i => i.OrderId)
    .OnDelete(DeleteBehavior.Cascade);
        })
    ;

        builder.Entity<OrderItem>(e =>
        {
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.Property(x => x.LineTotal).HasPrecision(18, 2);
        });

        builder.Entity<CartItem>()
        .HasOne(x => x.Cart)
        .WithMany(c => c.Items)
        .HasForeignKey(x => x.CartId)
        .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<CartItem>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Entity<Cart>()
        .HasIndex(c => c.UserId)
        .IsUnique()
        .HasFilter("[UserId] IS NOT NULL"); // SQL Server

        builder.Entity<Cart>()
            .HasIndex(c => c.SessionId)
            .IsUnique()
            .HasFilter("[SessionId] IS NOT NULL");
        // (Tuỳ chọn) index cho SortOrder
        builder.Entity<Product>()
            .HasIndex(p => p.SortOrder);

        // Seed 6 món
        builder.Entity<Product>().HasData(
            new Product
            {
                Id = 1,
                Name = "Bún Bò Huế",
                Description = "Spring tale, ibe pork shoulder, thai basil",
                PriceVnd = 50000,
                ImageUrl = "/images/foods/bun_bo_hue.png",
                IsActive = true,
                SortOrder = 1
            },
            new Product
            {
                Id = 2,
                Name = "Phở Bò",
                Description = "Shrimp, rice noodles, hcoi, bacon",
                PriceVnd = 50000,
                ImageUrl = "/images/foods/pho_bo.png",
                IsActive = true,
                SortOrder = 2
            },
            new Product
            {
                Id = 3,
                Name = "Phở Gà",
                Description = "Stemed pork, iba pork, hsiu, thai basil",
                PriceVnd = 50000,
                ImageUrl = "/images/foods/pho_ga.png",
                IsActive = true,
                SortOrder = 3
            },
            new Product
            {
                Id = 4,
                Name = "Mỳ Quảng",
                Description = "Spring pork, ibe nite shang, brown sugar, thai bean",
                PriceVnd = 50000,
                ImageUrl = "/images/foods/my_quang.png",
                IsActive = true,
                SortOrder = 4
            },
            new Product
            {
                Id = 5,
                Name = "Hủ Tiếu Nam Vang",
                Description = "Mtring pork, iba porl evolving, be teli, jorani, mint, bean",
                PriceVnd = 50000,
                ImageUrl = "/images/foods/hu_tieu_nam_vang.png",
                IsActive = true,
                SortOrder = 5
            },
            new Product
            {
                Id = 6,
                Name = "Cơm Tấm",
                Description = "Mivined per, ha fra rtwong, basil, mint, bean",
                PriceVnd = 90000,
                ImageUrl = "/images/foods/com_tam.png",
                IsActive = true,
                SortOrder = 6
            }
        );
    }
}
