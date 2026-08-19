using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Domain.Entities;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Infrastructure.Persistence.Seed;

/// <summary>
/// Seeds SYNTHETIC demonstration data only — no real people, no real payment data. Runs once
/// (guarded by "already seeded" checks) so it is safe on every container start. See
/// docs/06-runbook.md "Usuarios de demostración" for the full credential list.
/// </summary>
public static class DemoDataSeeder
{
    public const string DemoPassword = "Demo#2026!"; // synthetic demo credential, not a real secret

    public static async Task SeedAsync(ApplicationDbContext db, IServiceProvider services, CancellationToken ct = default)
    {
        if (await db.Schools.AnyAsync(ct)) return; // already seeded

        var hasher = services.GetRequiredService<IPasswordHasher>();

        var school = new School
        {
            Name = "Colegio Demostración (datos sintéticos)",
            LegalId = "DEMO-0001",
            DefaultCurrency = "USD",
            DefaultLocale = "es",
            TimeZoneId = "America/Panama"
        };
        db.Schools.Add(school);
        await db.SaveChangesAsync(ct);

        var permissions = SeedPermissions(db);
        await db.SaveChangesAsync(ct);

        var roles = await SeedRolesAsync(db, school.Id, permissions, ct);
        await SeedSettingsAsync(db, school.Id, ct);
        var (levels, sections) = SeedAcademicStructure(db, school.Id);
        await db.SaveChangesAsync(ct);

        var warehouse = new Warehouse { SchoolId = school.Id, Name = "Almacén Cafetería Principal" };
        db.Warehouses.Add(warehouse);
        await db.SaveChangesAsync(ct);

        var pos = new PointOfSale { SchoolId = school.Id, Name = "Cafetería Principal", Location = "Edificio A", DefaultWarehouseId = warehouse.Id };
        db.PointsOfSale.Add(pos);
        await db.SaveChangesAsync(ct);
        db.Registers.Add(new Register { SchoolId = school.Id, PointOfSaleId = pos.Id, Name = "Caja 1" });
        await db.SaveChangesAsync(ct);

        var categories = SeedProductCategories(db, school.Id);
        await db.SaveChangesAsync(ct);
        var products = SeedProducts(db, school.Id, categories);
        await db.SaveChangesAsync(ct);
        foreach (var product in products)
            db.InventoryBalances.Add(new InventoryBalance { SchoolId = school.Id, WarehouseId = warehouse.Id, ProductId = product.Id, QuantityOnHand = 100 });
        await db.SaveChangesAsync(ct);

        await SeedUsersAsync(db, school.Id, roles, hasher, ct);
        var guardian = await SeedStudentsAndGuardiansAsync(db, school.Id, levels, sections, ct);
        await SeedGuardianLoginAsync(db, school.Id, guardian, roles["Tutor"], hasher, ct);
    }

    private static List<Permission> SeedPermissions(ApplicationDbContext db)
    {
        var defs = new (string Key, string Module, string Description)[]
        {
            ("students.read", "Students", "Consultar estudiantes"),
            ("students.write", "Students", "Crear/editar estudiantes"),
            ("students.import", "Students", "Importar estudiantes masivamente"),
            ("guardians.read", "Guardians", "Consultar tutores"),
            ("guardians.write", "Guardians", "Crear/editar tutores"),
            ("employees.read", "Employees", "Consultar empleados"),
            ("employees.write", "Employees", "Crear/editar empleados"),
            ("wallets.read", "Wallets", "Consultar carteras y movimientos"),
            ("wallets.adjust", "Wallets", "Realizar ajustes manuales de cartera"),
            ("recharges.create.presential", "Recharges", "Registrar recargas presenciales"),
            ("recharges.create.digital", "Recharges", "Solicitar recargas digitales"),
            ("recharges.read", "Recharges", "Consultar recargas"),
            ("rfid.manage", "Rfid", "Emitir, reemplazar y bloquear credenciales RFID"),
            ("rfid.manual_lookup", "Rfid", "Búsqueda manual de comprador"),
            ("products.read", "Catalog", "Consultar productos"),
            ("products.write", "Catalog", "Administrar productos y categorías"),
            ("prices.write", "Catalog", "Administrar precios"),
            ("inventory.read", "Inventory", "Consultar inventario"),
            ("inventory.write", "Inventory", "Registrar entradas y transferencias"),
            ("inventory.adjust", "Inventory", "Ajustar existencias"),
            ("pos.sell", "Pos", "Procesar ventas en el punto de venta"),
            ("pos.refund", "Pos", "Autorizar anulaciones y devoluciones"),
            ("pos.shift.manage", "Pos", "Abrir y cerrar turnos de caja"),
            ("reports.read", "Reports", "Consultar reportes"),
            ("reports.export", "Reports", "Exportar reportes"),
            ("audit.read", "Audit", "Consultar bitácora de auditoría"),
            ("settings.write", "Settings", "Modificar configuración del sistema"),
            ("users.manage", "Security", "Administrar usuarios, roles y permisos")
        };

        var permissions = defs.Select(d => new Permission { Key = d.Key, Module = d.Module, Description = d.Description }).ToList();
        db.Permissions.AddRange(permissions);
        return permissions;
    }

    private static async Task<Dictionary<string, Role>> SeedRolesAsync(ApplicationDbContext db, Guid schoolId, List<Permission> permissions, CancellationToken ct)
    {
        var byKey = permissions.ToDictionary(p => p.Key);
        var definitions = new Dictionary<string, string[]>
        {
            ["Administrador"] = byKey.Keys.ToArray(), // full access
            ["Finanzas"] = new[] { "wallets.read", "wallets.adjust", "recharges.create.presential", "recharges.read", "products.read", "prices.write", "reports.read", "reports.export" },
            ["Supervisor"] = new[] { "pos.refund", "pos.shift.manage", "inventory.read", "wallets.read", "reports.read", "rfid.manual_lookup" },
            ["Operador"] = new[] { "pos.sell", "pos.shift.manage", "recharges.create.presential", "rfid.manual_lookup", "products.read" },
            ["Auditor"] = new[] { "audit.read", "reports.read", "wallets.read", "recharges.read" },
            ["Tutor"] = new[] { "wallets.read", "recharges.create.digital", "rfid.manual_lookup" }
        };

        var roles = new Dictionary<string, Role>();
        foreach (var (name, permKeys) in definitions)
        {
            var role = new Role { SchoolId = schoolId, Name = name, IsSystemRole = true, Description = $"Rol {name} (datos sintéticos)" };
            db.Roles.Add(role);
            await db.SaveChangesAsync(ct);
            foreach (var key in permKeys)
                db.RolePermissions.Add(new RolePermission { RoleId = role.Id, PermissionId = byKey[key].Id });
            roles[name] = role;
        }
        await db.SaveChangesAsync(ct);
        return roles;
    }

    private static async Task SeedSettingsAsync(ApplicationDbContext db, Guid schoolId, CancellationToken ct)
    {
        db.SystemSettings.AddRange(
            new SystemSetting { SchoolId = schoolId, Key = "currency.default", Value = "USD", ValueType = "string", Description = "Moneda por defecto del colegio" },
            new SystemSetting { SchoolId = schoolId, Key = "wallet.default_low_balance_threshold", Value = "5.00", ValueType = "number" },
            new SystemSetting { SchoolId = schoolId, Key = "wallet.allow_negative_balance", Value = "false", ValueType = "bool" },
            new SystemSetting { SchoolId = schoolId, Key = "pos.allow_sales_without_stock", Value = "false", ValueType = "bool" });
        await db.SaveChangesAsync(ct);
    }

    private static (List<SchoolLevel>, List<SchoolSection>) SeedAcademicStructure(ApplicationDbContext db, Guid schoolId)
    {
        var primaria = new SchoolLevel { SchoolId = schoolId, Name = "Primaria", SortOrder = 1 };
        var secundaria = new SchoolLevel { SchoolId = schoolId, Name = "Secundaria", SortOrder = 2 };
        db.SchoolLevels.AddRange(primaria, secundaria);

        var sectionA = new SchoolSection { SchoolId = schoolId, SchoolLevel = primaria, Name = "3A" };
        var sectionB = new SchoolSection { SchoolId = schoolId, SchoolLevel = secundaria, Name = "9B" };
        db.SchoolSections.AddRange(sectionA, sectionB);

        return (new List<SchoolLevel> { primaria, secundaria }, new List<SchoolSection> { sectionA, sectionB });
    }

    private static List<ProductCategory> SeedProductCategories(ApplicationDbContext db, Guid schoolId)
    {
        var categories = new[] { "Almuerzos", "Meriendas", "Bebidas", "Snacks" }
            .Select(name => new ProductCategory { SchoolId = schoolId, Name = name }).ToList();
        db.ProductCategories.AddRange(categories);
        return categories;
    }

    private static List<Product> SeedProducts(ApplicationDbContext db, Guid schoolId, List<ProductCategory> categories)
    {
        Guid CategoryId(string name) => categories.First(c => c.Name == name).Id;

        var products = new List<Product>
        {
            new() { SchoolId = schoolId, Code = "ALM-001", Name = "Almuerzo del día", CategoryId = CategoryId("Almuerzos"), Cost = 2.50m, BasePrice = 4.50m, TaxRate = 0.07m, MinStockLevel = 10, ReorderLevel = 20 },
            new() { SchoolId = schoolId, Code = "MER-001", Name = "Sándwich de jamón y queso", CategoryId = CategoryId("Meriendas"), Cost = 0.90m, BasePrice = 2.00m, TaxRate = 0.07m, MinStockLevel = 15, ReorderLevel = 30 },
            new() { SchoolId = schoolId, Code = "BEB-001", Name = "Agua embotellada 500ml", CategoryId = CategoryId("Bebidas"), Cost = 0.30m, BasePrice = 0.75m, TaxRate = 0.07m, MinStockLevel = 20, ReorderLevel = 40 },
            new() { SchoolId = schoolId, Code = "BEB-002", Name = "Jugo natural 300ml", CategoryId = CategoryId("Bebidas"), Cost = 0.50m, BasePrice = 1.25m, TaxRate = 0.07m, MinStockLevel = 20, ReorderLevel = 40 },
            new() { SchoolId = schoolId, Code = "SNK-001", Name = "Galletas integrales", CategoryId = CategoryId("Snacks"), Cost = 0.40m, BasePrice = 1.00m, TaxRate = 0.07m, MinStockLevel = 15, ReorderLevel = 30 }
        };
        db.Products.AddRange(products);
        return products;
    }

    private static async Task SeedUsersAsync(ApplicationDbContext db, Guid schoolId, Dictionary<string, Role> roles, IPasswordHasher hasher, CancellationToken ct)
    {
        var demoUsers = new (string Email, string FullName, string Role)[]
        {
            ("admin@demo.schoolcafeteria.local", "Administradora Demo", "Administrador"),
            ("finanzas@demo.schoolcafeteria.local", "Analista de Finanzas Demo", "Finanzas"),
            ("supervisor@demo.schoolcafeteria.local", "Supervisor de Cafetería Demo", "Supervisor"),
            ("operador@demo.schoolcafeteria.local", "Operador de Caja Demo", "Operador"),
            ("auditor@demo.schoolcafeteria.local", "Auditor Interno Demo", "Auditor")
        };

        foreach (var (email, fullName, roleName) in demoUsers)
        {
            var user = new User { SchoolId = schoolId, Email = email, FullName = fullName, PasswordHash = hasher.Hash(DemoPassword), IsActive = true };
            db.Users.Add(user);
            await db.SaveChangesAsync(ct);
            db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roles[roleName].Id });
        }
        await db.SaveChangesAsync(ct);
    }

    private static async Task<Guardian> SeedStudentsAndGuardiansAsync(
        ApplicationDbContext db, Guid schoolId, List<SchoolLevel> levels, List<SchoolSection> sections, CancellationToken ct)
    {
        var guardian = new Guardian { SchoolId = schoolId, FullName = "Tutor Demo Uno", Email = "tutor1@demo.schoolcafeteria.local", Phone = "+50760000001" };
        db.Guardians.Add(guardian);
        await db.SaveChangesAsync(ct);

        var studentDefs = new (string Code, string First, string Last)[]
        {
            ("S-1001", "Ana", "Gómez Demo"),
            ("S-1002", "Luis", "Gómez Demo")
        };

        foreach (var (code, first, last) in studentDefs)
        {
            var buyer = new Buyer { SchoolId = schoolId, Type = BuyerType.Student, FullName = $"{first} {last}" };
            db.Buyers.Add(buyer);
            await db.SaveChangesAsync(ct);

            db.Wallets.Add(new Wallet { SchoolId = schoolId, BuyerId = buyer.Id, Currency = "USD", Balance = 20.00m, LowBalanceThreshold = 5.00m });

            var student = new Student
            {
                SchoolId = schoolId, BuyerId = buyer.Id, StudentCode = code, FirstName = first, LastName = last,
                SchoolLevelId = levels[0].Id, SchoolSectionId = sections[0].Id, Status = StudentStatus.Active
            };
            db.Students.Add(student);
            await db.SaveChangesAsync(ct);

            db.GuardianStudents.Add(new GuardianStudent
            {
                GuardianId = guardian.Id, StudentId = student.Id, Relationship = "Padre/Madre", IsPrimary = true,
                CanRecharge = true, CanViewHistory = true, CanManageRfid = true, CanConfigureAlerts = true
            });
        }
        await db.SaveChangesAsync(ct);
        return guardian;
    }

    private static async Task SeedGuardianLoginAsync(ApplicationDbContext db, Guid schoolId, Guardian guardian, Role tutorRole, IPasswordHasher hasher, CancellationToken ct)
    {
        var user = new User
        {
            SchoolId = schoolId, Email = guardian.Email, FullName = guardian.FullName,
            PasswordHash = hasher.Hash(DemoPassword), IsActive = true, GuardianId = guardian.Id
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);
        db.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = tutorRole.Id });
        await db.SaveChangesAsync(ct);
    }
}
