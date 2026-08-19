using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Domain.Entities;

namespace SchoolCafeteria.Infrastructure.Persistence;

/// <summary>Centralizes EF Core fluent configuration: unique indexes, decimal precision, concurrency
/// tokens and relationship delete behavior, grouped by domain module for readability.</summary>
public static class ModelConfiguration
{
    public static void Configure(ModelBuilder b)
    {
        ConfigureIdentity(b);
        ConfigurePeople(b);
        ConfigureWallet(b);
        ConfigurePayments(b);
        ConfigureRfid(b);
        ConfigureCatalog(b);
        ConfigurePointOfSale(b);
        ConfigureInventory(b);
        ConfigureNotifications(b);
        ConfigureIntegration(b);
        ConfigureAuditAndSettings(b);
        ApplyGlobalConventions(b);
    }

    private static void ConfigureIdentity(ModelBuilder b)
    {
        b.Entity<User>().HasIndex(u => new { u.SchoolId, u.Email }).IsUnique();
        b.Entity<Role>().HasIndex(r => new { r.SchoolId, r.Name }).IsUnique();
        b.Entity<Permission>().HasIndex(p => p.Key).IsUnique();
        b.Entity<RolePermission>().HasIndex(rp => new { rp.RoleId, rp.PermissionId }).IsUnique();
        b.Entity<UserRole>().HasIndex(ur => new { ur.UserId, ur.RoleId, ur.PointOfSaleId }).IsUnique();
        b.Entity<RefreshToken>().HasIndex(t => t.TokenHash).IsUnique();

        b.Entity<User>().HasOne(u => u.Guardian).WithMany().HasForeignKey(u => u.GuardianId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<User>().HasOne(u => u.Buyer).WithMany().HasForeignKey(u => u.BuyerId).OnDelete(DeleteBehavior.SetNull);
    }

    private static void ConfigurePeople(ModelBuilder b)
    {
        b.Entity<Student>().HasIndex(s => new { s.SchoolId, s.StudentCode }).IsUnique();
        b.Entity<Employee>().HasIndex(e => new { e.SchoolId, e.EmployeeCode }).IsUnique();
        b.Entity<Buyer>().HasOne(buyer => buyer.Wallet).WithOne(w => w.Buyer!).HasForeignKey<Wallet>(w => w.BuyerId);

        b.Entity<GuardianStudent>().HasIndex(gs => new { gs.GuardianId, gs.StudentId }).IsUnique();
        b.Entity<GuardianStudent>().HasOne(gs => gs.Guardian).WithMany(g => g.StudentLinks).HasForeignKey(gs => gs.GuardianId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<GuardianStudent>().HasOne(gs => gs.Student).WithMany(s => s.GuardianLinks).HasForeignKey(gs => gs.StudentId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Student>().HasOne(s => s.SchoolLevelRef).WithMany().HasForeignKey(s => s.SchoolLevelId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<Student>().HasOne(s => s.SchoolSectionRef).WithMany().HasForeignKey(s => s.SchoolSectionId).OnDelete(DeleteBehavior.SetNull);
        b.Entity<SchoolSection>().HasOne(s => s.SchoolLevel).WithMany(l => l.Sections).HasForeignKey(s => s.SchoolLevelId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureWallet(ModelBuilder b)
    {
        b.Entity<Wallet>().HasIndex(w => w.BuyerId).IsUnique();
        b.Entity<Wallet>().Property(w => w.Balance).HasPrecision(18, 2);
        b.Entity<Wallet>().Property(w => w.HeldBalance).HasPrecision(18, 2);
        b.Entity<Wallet>().Property(w => w.MaxBalance).HasPrecision(18, 2);
        b.Entity<Wallet>().Property(w => w.LowBalanceThreshold).HasPrecision(18, 2);

        b.Entity<WalletTransaction>().HasIndex(t => t.TransactionNumber).IsUnique();
        b.Entity<WalletTransaction>().HasIndex(t => t.IdempotencyKey).IsUnique().HasFilter("[IdempotencyKey] IS NOT NULL");
        b.Entity<WalletTransaction>().HasIndex(t => new { t.WalletId, t.OccurredAtUtc });
        b.Entity<WalletTransaction>().Property(t => t.Amount).HasPrecision(18, 2);
        b.Entity<WalletTransaction>().Property(t => t.BalanceBefore).HasPrecision(18, 2);
        b.Entity<WalletTransaction>().Property(t => t.BalanceAfter).HasPrecision(18, 2);
        b.Entity<WalletTransaction>().HasOne(t => t.Wallet).WithMany(w => w.Transactions).HasForeignKey(t => t.WalletId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<WalletTransaction>().HasOne(t => t.RelatedTransaction).WithMany().HasForeignKey(t => t.RelatedTransactionId).OnDelete(DeleteBehavior.Restrict);
        // Ledger rows are append-only at the application layer; no delete behavior is ever exercised via cascading FKs.
    }

    private static void ConfigurePayments(ModelBuilder b)
    {
        b.Entity<Recharge>().HasIndex(r => r.IdempotencyKey).IsUnique();
        b.Entity<Recharge>().Property(r => r.Amount).HasPrecision(18, 2);
        b.Entity<PaymentOrder>().Property(p => p.Amount).HasPrecision(18, 2);
        b.Entity<PaymentTransaction>().Property(p => p.Amount).HasPrecision(18, 2);
        b.Entity<PaymentWebhook>().HasIndex(w => new { w.Provider, w.ExternalEventId }).IsUnique();
    }

    private static void ConfigureRfid(ModelBuilder b)
    {
        // A credential can only be Active for one buyer at a time — enforced by a unique filtered index.
        b.Entity<RfidCredential>().HasIndex(c => c.CredentialHash).IsUnique().HasFilter("[Status] = 0"); // 0 = Active
    }

    private static void ConfigureCatalog(ModelBuilder b)
    {
        b.Entity<Product>().HasIndex(p => new { p.SchoolId, p.Code }).IsUnique();
        b.Entity<Product>().HasIndex(p => p.BarCode);
        b.Entity<Product>().Property(p => p.Cost).HasPrecision(18, 4);
        b.Entity<Product>().Property(p => p.BasePrice).HasPrecision(18, 2);
        b.Entity<Product>().Property(p => p.TaxRate).HasPrecision(9, 4);
        b.Entity<Product>().Property(p => p.MinStockLevel).HasPrecision(18, 3);
        b.Entity<Product>().Property(p => p.ReorderLevel).HasPrecision(18, 3);
        b.Entity<Product>().HasOne(p => p.Category).WithMany().HasForeignKey(p => p.CategoryId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<ProductPrice>().Property(p => p.UnitPrice).HasPrecision(18, 2);
        b.Entity<ProductPrice>().HasOne(p => p.Product).WithMany(p => p.Prices).HasForeignKey(p => p.ProductId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ProductPrice>().HasOne(p => p.PriceList).WithMany(l => l.Prices).HasForeignKey(p => p.PriceListId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigurePointOfSale(ModelBuilder b)
    {
        b.Entity<Register>().HasOne(r => r.PointOfSale).WithMany(p => p.Registers).HasForeignKey(r => r.PointOfSaleId).OnDelete(DeleteBehavior.Cascade);

        b.Entity<Sale>().HasIndex(s => new { s.RegisterShiftId, s.IdempotencyKey }).IsUnique();
        b.Entity<Sale>().HasIndex(s => s.SaleNumber).IsUnique();
        b.Entity<Sale>().Property(s => s.Subtotal).HasPrecision(18, 2);
        b.Entity<Sale>().Property(s => s.TaxTotal).HasPrecision(18, 2);
        b.Entity<Sale>().Property(s => s.DiscountTotal).HasPrecision(18, 2);
        b.Entity<Sale>().Property(s => s.Total).HasPrecision(18, 2);
        b.Entity<Sale>().HasOne(s => s.RegisterShift).WithMany(sh => sh.Sales).HasForeignKey(s => s.RegisterShiftId).OnDelete(DeleteBehavior.Restrict);
        b.Entity<Sale>().HasOne(s => s.Buyer).WithMany().HasForeignKey(s => s.BuyerId).OnDelete(DeleteBehavior.Restrict);

        b.Entity<SaleLine>().Property(l => l.Quantity).HasPrecision(18, 3);
        b.Entity<SaleLine>().Property(l => l.UnitPrice).HasPrecision(18, 2);
        b.Entity<SaleLine>().Property(l => l.TaxRate).HasPrecision(9, 4);
        b.Entity<SaleLine>().Property(l => l.DiscountAmount).HasPrecision(18, 2);
        b.Entity<SaleLine>().Property(l => l.LineTotal).HasPrecision(18, 2);
        b.Entity<SaleLine>().HasOne(l => l.Sale).WithMany(s => s.Lines).HasForeignKey(l => l.SaleId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<SaleLine>().HasOne(l => l.Product).WithMany().HasForeignKey(l => l.ProductId).OnDelete(DeleteBehavior.Restrict);
    }

    private static void ConfigureInventory(ModelBuilder b)
    {
        b.Entity<InventoryBalance>().HasIndex(bal => new { bal.WarehouseId, bal.ProductId }).IsUnique();
        b.Entity<InventoryBalance>().Property(x => x.QuantityOnHand).HasPrecision(18, 3);
        b.Entity<InventoryMovement>().Property(m => m.Quantity).HasPrecision(18, 3);
        b.Entity<InventoryMovement>().Property(m => m.BalanceAfter).HasPrecision(18, 3);
        b.Entity<InventoryMovement>().HasIndex(m => new { m.WarehouseId, m.ProductId, m.OccurredAtUtc });

        b.Entity<StockCountLine>().Property(l => l.SystemQuantity).HasPrecision(18, 3);
        b.Entity<StockCountLine>().Property(l => l.CountedQuantity).HasPrecision(18, 3);
        b.Entity<StockCountLine>().HasOne(l => l.StockCount).WithMany(c => c.Lines).HasForeignKey(l => l.StockCountId).OnDelete(DeleteBehavior.Cascade);
    }

    private static void ConfigureNotifications(ModelBuilder b)
    {
        b.Entity<Notification>().HasIndex(n => n.DeduplicationKey).IsUnique();
        b.Entity<Notification>().HasIndex(n => n.Status);
    }

    private static void ConfigureIntegration(ModelBuilder b)
    {
        b.Entity<ImportJobRow>().HasOne(r => r.ImportJob).WithMany(j => j.Rows).HasForeignKey(r => r.ImportJobId).OnDelete(DeleteBehavior.Cascade);
        b.Entity<ImportJobRow>().HasIndex(r => new { r.ImportJobId, r.NaturalKey });
    }

    private static void ConfigureAuditAndSettings(ModelBuilder b)
    {
        b.Entity<AuditLog>().HasIndex(a => new { a.EntityName, a.EntityId, a.OccurredAtUtc });
        b.Entity<AuditLog>().HasIndex(a => new { a.UserId, a.OccurredAtUtc });

        b.Entity<SystemSetting>().HasIndex(s => new { s.SchoolId, s.Key }).IsUnique();
    }

    /// <summary>Applies RowVersion-as-concurrency-token and decimal defaults across every entity
    /// that declares them, so new entities pick these up without repeating boilerplate.</summary>
    private static void ApplyGlobalConventions(ModelBuilder b)
    {
        foreach (var entityType in b.Model.GetEntityTypes())
        {
            if (entityType.FindProperty(nameof(Domain.Common.BaseEntity.RowVersion)) is { } rowVersion)
            {
                rowVersion.SetColumnType("rowversion");
                rowVersion.IsConcurrencyToken = true;
                rowVersion.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.OnAddOrUpdate;
            }
        }
    }
}
