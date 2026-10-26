using FAInances.Services.Models;
using FAInances.Services.Services;
using FAInances.Web.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FAInances.Web.Controllers.Api;

[ApiController]
[Authorize]
[Route("api/categories")]
public class CategoriesController(ICategoryService categoryService) : ControllerBase
{
    [HttpGet]
    public async Task<IEnumerable<CategoryResponse>> GetAll(CancellationToken cancellationToken)
    {
        var categories = await categoryService.GetAllAsync(User.GetUserId(), cancellationToken);
        return categories.Select(CategoryResponse.From);
    }

    [HttpGet("{id}")]
    public async Task<CategoryResponse> Get(string id, CancellationToken cancellationToken) =>
        CategoryResponse.From(await categoryService.GetByIdAsync(User.GetUserId(), id, cancellationToken));

    [HttpPost]
    public async Task<ActionResult<CategoryResponse>> Create(CategoryInput input, CancellationToken cancellationToken)
    {
        var category = await categoryService.CreateAsync(User.GetUserId(), input, cancellationToken);
        return CreatedAtAction(nameof(Get), new { id = category.Id }, CategoryResponse.From(category));
    }

    [HttpPut("{id}")]
    public async Task<CategoryResponse> Update(string id, CategoryInput input, CancellationToken cancellationToken) =>
        CategoryResponse.From(await categoryService.UpdateAsync(User.GetUserId(), id, input, cancellationToken));

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken cancellationToken)
    {
        await categoryService.DeleteAsync(User.GetUserId(), id, cancellationToken);
        return NoContent();
    }
}
