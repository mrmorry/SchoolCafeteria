using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SchoolCafeteria.Domain.Entities;

namespace SchoolCafeteria.Application.Common;

/// <summary>
/// Application-facing view of the persistence context (Dependency Inversion: Application defines
/// the contract, Infrastructure's EF Core DbContext implements it). Keeps Application free of any
/// direct EF Core provider concerns.
/// </summary>
public interface IAppDbContext
{
    DbSet<School> Schools { get; }
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<Role> Roles { get; }
    DbSet<Permission> Permissions { get; }
    DbSet<RolePermission> RolePermissions { get; }
    DbSet<UserRole> UserRoles { get; }

    DbSet<SchoolLevel> SchoolLevels { get; }
    DbSet<SchoolSection> SchoolSections { get; }
    DbSet<Buyer> Buyers { get; }
    DbSet<Student> Students { get; }
    DbSet<Employee> Employees { get; }
    DbSet<Guardian> Guardians { get; }
    DbSet<GuardianStudent> GuardianStudents { get; }

    DbSet<Wallet> Wallets { get; }
    DbSet<WalletTransaction> WalletTransactions { get; }

    DbSet<Recharge> Recharges { get; }
    DbSet<PaymentOrder> PaymentOrders { get; }
    DbSet<PaymentTransaction> PaymentTransactions { get; }
    DbSet<PaymentWebhook> PaymentWebhooks { get; }

    DbSet<RfidCredential> RfidCredentials { get; }
    DbSet<RfidAssignmentHistory> RfidAssignmentHistories { get; }
    DbSet<RfidUsageLog> RfidUsageLogs { get; }

    DbSet<ProductCategory> ProductCategories { get; }
    DbSet<Product> Products { get; }
    DbSet<PriceList> PriceLists { get; }
    DbSet<ProductPrice> ProductPrices { get; }

    DbSet<PointOfSale> PointsOfSale { get; }
    DbSet<Register> Registers { get; }
    DbSet<RegisterShift> RegisterShifts { get; }
    DbSet<Sale> Sales { get; }
    DbSet<SaleLine> SaleLines { get; }

    DbSet<Warehouse> Warehouses { get; }
    DbSet<InventoryBalance> InventoryBalances { get; }
    DbSet<InventoryMovement> InventoryMovements { get; }
    DbSet<StockCount> StockCounts { get; }
    DbSet<StockCountLine> StockCountLines { get; }

    DbSet<NotificationTemplate> NotificationTemplates { get; }
    DbSet<Notification> Notifications { get; }

    DbSet<ImportJob> ImportJobs { get; }
    DbSet<ImportJobRow> ImportJobRows { get; }
    DbSet<ExternalSystem> ExternalSystems { get; }
    DbSet<IntegrationLog> IntegrationLogs { get; }

    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SystemSetting> SystemSettings { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
