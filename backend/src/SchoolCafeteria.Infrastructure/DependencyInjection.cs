using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SchoolCafeteria.Application.Abstractions;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.Services;
using SchoolCafeteria.Infrastructure.Adapters;
using SchoolCafeteria.Infrastructure.BackgroundJobs;
using SchoolCafeteria.Infrastructure.Persistence;
using SchoolCafeteria.Infrastructure.Persistence.Interceptors;
using SchoolCafeteria.Infrastructure.Services;

namespace SchoolCafeteria.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();

        services.AddScoped<AuditSaveChangesInterceptor>();
        services.AddDbContext<ApplicationDbContext>((sp, options) =>
        {
            options.UseSqlServer(configuration.GetConnectionString("Default"), sql => sql.EnableRetryOnFailure());
            options.AddInterceptors(sp.GetRequiredService<AuditSaveChangesInterceptor>());
        });
        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<ApplicationDbContext>());

        services.AddSingleton<IDateTimeProvider, DateTimeProvider>();
        services.AddSingleton<IPasswordHasher, PasswordHasher>();
        services.AddSingleton<ITokenService, TokenService>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        // External integrations — every one of these is swappable purely by changing the
        // registration below (or by config-driven selection), never by touching Application code.
        services.AddSingleton<IPaymentGateway, SandboxPaymentGateway>();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<IFileStorage, LocalFileStorage>();
        services.AddScoped<IStudentSourceAdapter, CsvStudentSourceAdapter>();
        services.AddSingleton<IRfidReaderProvider, KeyboardWedgeRfidReaderProvider>();

        services.AddScoped<WalletLedgerService>();
        services.AddScoped<InventoryLedgerService>();
        services.AddScoped<NotificationOutboxService>();
        services.AddScoped<AuthService>();
        services.AddScoped<StudentService>();
        services.AddScoped<GuardianService>();
        services.AddScoped<EmployeeService>();
        services.AddScoped<WalletService>();
        services.AddScoped<RechargeService>();
        services.AddScoped<RfidService>();
        services.AddScoped<ProductService>();
        services.AddScoped<InventoryAdminService>();
        services.AddScoped<PosAdminService>();
        services.AddScoped<SaleService>();
        services.AddScoped<ImportService>();
        services.AddScoped<ReportService>();
        services.AddScoped<AuditService>();
        services.AddScoped<SettingsService>();

        services.AddHostedService<NotificationDispatcherService>();

        return services;
    }
}
