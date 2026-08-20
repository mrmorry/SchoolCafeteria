using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Infrastructure.Persistence;

namespace SchoolCafeteria.UnitTests.TestSupport;

/// <summary>
/// Creates an ApplicationDbContext backed by an in-memory SQLite connection. Unlike EF Core's
/// InMemory provider, SQLite supports real transactions (Database.BeginTransactionAsync), which
/// SaleService and InventoryAdminService.TransferAsync rely on — this keeps the tests exercising
/// the actual transactional code path instead of a relational-feature-free stand-in.
/// </summary>
public sealed class SqliteContextFactory : IDisposable
{
    private readonly SqliteConnection _connection;

    public SqliteContextFactory()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        using var context = CreateContext();
        context.Database.EnsureCreated();
    }

    public ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new ApplicationDbContext(options);
    }

    public void Dispose() => _connection.Dispose();
}
