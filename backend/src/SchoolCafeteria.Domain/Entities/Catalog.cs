using SchoolCafeteria.Domain.Common;
using SchoolCafeteria.Domain.Enums;

namespace SchoolCafeteria.Domain.Entities;

public class ProductCategory : SoftDeletableSchoolEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
}

public class Product : SoftDeletableSchoolEntity
{
    public string Code { get; set; } = string.Empty;
    public string? BarCode { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public Guid CategoryId { get; set; }
    public ProductCategory? Category { get; set; }
    public string? ImageUrl { get; set; }
    public UnitOfMeasure UnitOfMeasure { get; set; } = UnitOfMeasure.Unit;

    public decimal Cost { get; set; }
    public decimal BasePrice { get; set; }
    public decimal TaxRate { get; set; }

    public ProductStatus Status { get; set; } = ProductStatus.Active;
    public bool AvailableForSale { get; set; } = true;
    public bool TrackInventory { get; set; } = true;

    public decimal MinStockLevel { get; set; }
    public decimal ReorderLevel { get; set; }

    public string? Allergens { get; set; }
    public string? RestrictedToLevelIds { get; set; } // CSV of SchoolLevel ids, optional gate

    public ICollection<ProductPrice> Prices { get; set; } = new List<ProductPrice>();
}

public class PriceList : SoftDeletableSchoolEntity
{
    public string Name { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public ICollection<ProductPrice> Prices { get; set; } = new List<ProductPrice>();
}

/// <summary>A sale always copies UnitPrice into SaleLine — price changes never alter historical sales.</summary>
public class ProductPrice : BaseEntity
{
    public Guid ProductId { get; set; }
    public Product? Product { get; set; }
    public Guid PriceListId { get; set; }
    public PriceList? PriceList { get; set; }

    public decimal UnitPrice { get; set; }
    public DateTime ValidFromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ValidToUtc { get; set; }
}
