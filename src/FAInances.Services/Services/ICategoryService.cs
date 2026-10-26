using FAInances.Services.Entities;
using FAInances.Services.Models;

namespace FAInances.Services.Services;

public interface ICategoryService
{
    Task<IReadOnlyList<Category>> GetAllAsync(string userId, CancellationToken cancellationToken = default);

    Task<Category> GetByIdAsync(string userId, string categoryId, CancellationToken cancellationToken = default);

    Task<Category> CreateAsync(string userId, CategoryInput input, CancellationToken cancellationToken = default);

    Task<Category> UpdateAsync(string userId, string categoryId, CategoryInput input, CancellationToken cancellationToken = default);

    /// <summary>Deletes a category. Fails if any transaction still uses it.</summary>
    Task DeleteAsync(string userId, string categoryId, CancellationToken cancellationToken = default);
}
