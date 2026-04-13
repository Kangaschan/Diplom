using Application.Categories;
using Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Presentation.Extensions;

namespace Presentation.Controllers;

public sealed record CreateCategoryRequest(string Name, CategoryType Type);
public sealed record UpdateCategoryRequest(string Name, bool IsActive);

[ApiController]
[Route("api/categories")]
[Authorize]
public sealed class CategoriesController(CategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var result = await categoryService.GetAvailableAsync(ct);
        return this.ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var result = await categoryService.CreateAsync(request.Name, request.Type, ct);
        return this.ToActionResult(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        var result = await categoryService.UpdateAsync(id, request.Name, request.IsActive, ct);
        return this.ToActionResult(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        var result = await categoryService.DeleteAsync(id, ct);
        return this.ToActionResult(result);
    }
}
