using System.Net.Mail;
using FAInances.Services.Data;
using FAInances.Services.Entities;
using FAInances.Services.Exceptions;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace FAInances.Services.Services;

public class UserService(MongoDbContext db, IPasswordHasher<User> passwordHasher) : IUserService
{
    public const int MinPasswordLength = 6;
    public const int MaxDisplayNameLength = 50;

    public async Task<User> RegisterAsync(string email, string displayName, string password, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        displayName = displayName?.Trim() ?? string.Empty;

        if (!MailAddress.TryCreate(normalizedEmail, out _))
            throw new BusinessRuleException("Please enter a valid email address.");
        if (displayName.Length == 0)
            throw new BusinessRuleException("Please enter your name.");
        if (displayName.Length > MaxDisplayNameLength)
            throw new BusinessRuleException($"Name must be at most {MaxDisplayNameLength} characters.");
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
            throw new BusinessRuleException($"Password must be at least {MinPasswordLength} characters.");

        if (await db.Users.Find(u => u.Email == normalizedEmail).AnyAsync(cancellationToken))
            throw new BusinessRuleException("An account with this email already exists.");

        var user = new User { Email = normalizedEmail, DisplayName = displayName };
        user.PasswordHash = passwordHasher.HashPassword(user, password);

        try
        {
            await db.Users.InsertOneAsync(user, cancellationToken: cancellationToken);
        }
        catch (MongoWriteException ex) when (ex.WriteError.Category == ServerErrorCategory.DuplicateKey)
        {
            throw new BusinessRuleException("An account with this email already exists.");
        }

        return user;
    }

    public async Task<User?> ValidateCredentialsAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrEmpty(password))
            return null;

        var normalizedEmail = NormalizeEmail(email);
        var user = await db.Users.Find(u => u.Email == normalizedEmail).FirstOrDefaultAsync(cancellationToken);
        if (user is null)
            return null;

        var result = passwordHasher.VerifyHashedPassword(user, user.PasswordHash, password);
        if (result == PasswordVerificationResult.Failed)
            return null;

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = passwordHasher.HashPassword(user, password);
            await db.Users.UpdateOneAsync(
                u => u.Id == user.Id,
                Builders<User>.Update.Set(u => u.PasswordHash, user.PasswordHash),
                cancellationToken: cancellationToken);
        }

        return user;
    }

    private static string NormalizeEmail(string email) => (email ?? string.Empty).Trim().ToLowerInvariant();
}
