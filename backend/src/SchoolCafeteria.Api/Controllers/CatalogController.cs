using Microsoft.AspNetCore.Mvc;
using SchoolCafeteria.Api.Auth;
using SchoolCafeteria.Application.DTOs;
using SchoolCafeteria.Application.Services;

namespace SchoolCafeteria.Api.Controllers;

[Route("api/v1/catalog")]
public class CatalogController : ApiControllerBase
{
    private readonly ProductService _service;
    public CatalogController(ProductService service) => _service = service;

    [HttpGet("categories")]
    [RequirePermission("products.read")]
    public async Task<ActionResult<IReadOnlyList<ProductCategoryDto>>> GetCategories(CancellationToken ct)
        => Ok(await _service.GetCategoriesAsync(SchoolId, ct));

    [HttpPost("categories")]
    [RequirePermission("products.write")]
    public async Task<ActionResult<ProductCategoryDto>> CreateCategory(CreateProductCategoryRequest request, CancellationToken ct)
        => Ok(await _service.CreateCategoryAsync(SchoolId, request, ct));

    [HttpGet("products")]
    [RequirePermission("products.read")]
    public async Task<ActionResult<PagedResult<ProductDto>>> SearchProducts([FromQuery] PagedRequest request, [FromQuery] Guid? categoryId, CancellationToken ct)
        => Ok(await _service.SearchAsync(SchoolId, request, categoryId, ct));

    [HttpGet("products/{id:guid}")]
    [RequirePermission("products.read")]
    public async Task<ActionResult<ProductDto>> GetProduct(Guid id, CancellationToken ct)
    {
        var product = await _service.GetByIdAsync(SchoolId, id, ct);
        return product is null ? NotFound() : Ok(product);
    }

    [HttpPost("products")]
    [RequirePermission("products.write")]
    public async Task<ActionResult<ProductDto>> CreateProduct(CreateProductRequest request, CancellationToken ct)
    {
        var product = await _service.CreateAsync(SchoolId, request, ct);
        return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
    }

    [HttpPut("products/{id:guid}")]
    [RequirePermission("products.write")]
    public async Task<ActionResult<ProductDto>> UpdateProduct(Guid id, UpdateProductRequest request, CancellationToken ct)
        => Ok(await _service.UpdateAsync(SchoolId, id, request, ct));

    [HttpPost("products/prices")]
    [RequirePermission("prices.write")]
    public async Task<IActionResult> SchedulePrice(ScheduleProductPriceRequest request, CancellationToken ct)
    {
        await _service.ScheduleFuturePriceAsync(SchoolId, request, ct);
        return NoContent();
    }
}
