using Microsoft.EntityFrameworkCore;
using SchoolCafeteria.Application.Common;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Domain.Entities;

namespace SchoolCafeteria.Application.Services;

public class ProductService
{
    private readonly IAppDbContext _db;
    public ProductService(IAppDbContext db) => _db = db;

    public async Task<ProductCategoryDto> CreateCategoryAsync(Guid schoolId, CreateProductCategoryRequest request, CancellationToken ct = default)
    {
        var category = new ProductCategory { SchoolId = schoolId, Name = request.Name, Description = request.Description };
        _db.ProductCategories.Add(category);
        await _db.SaveChangesAsync(ct);
        return new ProductCategoryDto(category.Id, category.Name, category.Description);
    }

    public async Task<IReadOnlyList<ProductCategoryDto>> GetCategoriesAsync(Guid schoolId, CancellationToken ct = default) =>
        await _db.ProductCategories.Where(c => c.SchoolId == schoolId && !c.IsDeleted)
            .Select(c => new ProductCategoryDto(c.Id, c.Name, c.Description)).ToListAsync(ct);

    public async Task<ProductDto> CreateAsync(Guid schoolId, CreateProductRequest request, CancellationToken ct = default)
    {
        var duplicate = await _db.Products.AnyAsync(p => p.SchoolId == schoolId && p.Code == request.Code && !p.IsDeleted, ct);
        if (duplicate) throw new BusinessRuleException("product.duplicate_code", $"Ya existe un producto con código '{request.Code}'.");

        var product = new Product
        {
            SchoolId = schoolId, Code = request.Code, BarCode = request.BarCode, Name = request.Name,
            Description = request.Description, CategoryId = request.CategoryId, UnitOfMeasure = request.UnitOfMeasure,
            Cost = request.Cost, BasePrice = request.BasePrice, TaxRate = request.TaxRate,
            TrackInventory = request.TrackInventory, MinStockLevel = request.MinStockLevel,
            ReorderLevel = request.ReorderLevel, Allergens = request.Allergens
        };
        _db.Products.Add(product);
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(schoolId, product.Id, ct) ?? throw new NotFoundException(nameof(Product), product.Id);
    }

    public async Task<ProductDto> UpdateAsync(Guid schoolId, Guid productId, UpdateProductRequest request, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == productId && p.SchoolId == schoolId, ct)
            ?? throw new NotFoundException(nameof(Product), productId);

        product.Name = request.Name;
        product.Description = request.Description;
        product.Cost = request.Cost;
        product.BasePrice = request.BasePrice;
        product.TaxRate = request.TaxRate;
        product.Status = request.Status;
        product.AvailableForSale = request.AvailableForSale;
        product.TrackInventory = request.TrackInventory;
        product.MinStockLevel = request.MinStockLevel;
        product.ReorderLevel = request.ReorderLevel;
        await _db.SaveChangesAsync(ct);
        return await GetByIdAsync(schoolId, productId, ct) ?? throw new NotFoundException(nameof(Product), productId);
    }

    public async Task ScheduleFuturePriceAsync(Guid schoolId, ScheduleProductPriceRequest request, CancellationToken ct = default)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == request.ProductId && p.SchoolId == schoolId, ct)
            ?? throw new NotFoundException(nameof(Product), request.ProductId);

        var defaultList = await _db.PriceLists.FirstOrDefaultAsync(pl => pl.SchoolId == schoolId && pl.IsDefault, ct);
        if (defaultList is null)
        {
            defaultList = new PriceList { SchoolId = schoolId, Name = "Lista general", IsDefault = true };
            _db.PriceLists.Add(defaultList);
            await _db.SaveChangesAsync(ct);
        }

        _db.ProductPrices.Add(new ProductPrice
        {
            ProductId = product.Id, PriceListId = defaultList.Id, UnitPrice = request.UnitPrice,
            ValidFromUtc = request.ValidFromUtc, ValidToUtc = request.ValidToUtc
        });
        await _db.SaveChangesAsync(ct);
        // Note: historical sales already store their own UnitPrice snapshot in SaleLine, so this
        // never alters a past sale (rule 6).
    }

    public async Task<PagedResult<ProductDto>> SearchAsync(Guid schoolId, PagedRequest request, Guid? categoryId, CancellationToken ct = default)
    {
        var query = _db.Products.Where(p => p.SchoolId == schoolId && !p.IsDeleted).Include(p => p.Category);
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId.Value);
        if (!string.IsNullOrWhiteSpace(request.Search))
        {
            var term = request.Search.Trim();
            query = query.Where(p => p.Name.Contains(term) || p.Code.Contains(term) || (p.BarCode != null && p.BarCode.Contains(term)));
        }

        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(p => p.Name).Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(ct);

        var dtos = new List<ProductDto>();
        foreach (var p in items) dtos.Add(await MapAsync(p, ct));
        return new PagedResult<ProductDto>(dtos, total, request.Page, request.PageSize);
    }

    public async Task<ProductDto?> GetByIdAsync(Guid schoolId, Guid productId, CancellationToken ct = default)
    {
        var p = await _db.Products.Include(x => x.Category).FirstOrDefaultAsync(x => x.Id == productId && x.SchoolId == schoolId, ct);
        return p is null ? null : await MapAsync(p, ct);
    }

    private async Task<ProductDto> MapAsync(Product p, CancellationToken ct)
    {
        var stock = p.TrackInventory
            ? await _db.InventoryBalances.Where(b => b.ProductId == p.Id).SumAsync(b => (decimal?)b.QuantityOnHand, ct)
            : null;
        return new ProductDto(p.Id, p.Code, p.BarCode, p.Name, p.Description, p.CategoryId, p.Category?.Name ?? string.Empty,
            p.ImageUrl, p.UnitOfMeasure, p.Cost, p.BasePrice, p.TaxRate, p.Status, p.AvailableForSale, p.TrackInventory,
            p.MinStockLevel, p.ReorderLevel, p.Allergens, stock);
    }
}
