using FAInances.Services.Entities;

namespace FAInances.Services.Services;

public interface IUserService
{
    Task<User> RegisterAsync(string email, string displayName, string password, CancellationToken cancellationToken = default);

    /// <summary>Returns the user when the credentials are valid, otherwise null.</summary>
    Task<User?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default);
}
