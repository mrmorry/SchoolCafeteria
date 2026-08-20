using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SchoolCafeteria.Infrastructure.Persistence;

namespace SchoolCafeteria.IntegrationTests;

/// <summary>
/// Boots the real API pipeline (auth, middleware, routing, DI) but swaps the SQL Server-backed
/// ApplicationDbContext for an in-memory SQLite one, and runs under the "Testing" environment so
/// Program.cs skips its normal EnsureCreated+seed block (see Program.cs) — this factory does that
/// itself against the SQLite connection instead.
/// </summary>
public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(_connection));
        });

        // Build the schema directly against the same SQLite connection this factory will hand out,
        // without spinning up a second full DI container.
        using var bootstrapContext = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlite(_connection).Options);
        bootstrapContext.Database.EnsureCreated();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) _connection.Dispose();
    }
}
