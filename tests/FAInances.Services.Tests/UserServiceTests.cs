using FAInances.Services.Entities;
using FAInances.Services.Exceptions;
using FAInances.Services.Services;
using FAInances.Services.Tests.Infrastructure;
using Microsoft.AspNetCore.Identity;
using MongoDB.Driver;

namespace FAInances.Services.Tests;

public class UserServiceTests(MongoDbFixture fixture) : MongoDbTestBase(fixture)
{
    private UserService CreateService() => new(Db, new PasswordHasher<User>());

    [Fact]
    public async Task RegisterAsync_StoresUserWithNormalizedEmailAndHashedPassword()
    {
        var service = CreateService();

        var user = await service.RegisterAsync("  Alice@Example.COM ", "  Alice ", "secret123", Ct);

        var stored = await Db.Users.Find(u => u.Id == user.Id).SingleAsync(Ct);
        Assert.Equal("alice@example.com", stored.Email);
        Assert.Equal("Alice", stored.DisplayName);
        Assert.False(string.IsNullOrEmpty(stored.PasswordHash));
        Assert.NotEqual("secret123", stored.PasswordHash);
    }

    [Theory]
    [InlineData("not-an-email", "Alice", "secret123")]
    [InlineData("", "Alice", "secret123")]
    [InlineData("alice@example.com", "   ", "secret123")]
    [InlineData("alice@example.com", "Alice", "12345")]
    [InlineData("alice@example.com", "Alice", "")]
    public async Task RegisterAsync_RejectsInvalidInput(string email, string displayName, string password)
    {
        var service = CreateService();

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegisterAsync(email, displayName, password, Ct));
        Assert.Equal(0, await Db.Users.CountDocumentsAsync(FilterDefinition<User>.Empty, cancellationToken: Ct));
    }

    [Fact]
    public async Task RegisterAsync_RejectsTooLongDisplayName()
    {
        var service = CreateService();
        var name = new string('a', UserService.MaxDisplayNameLength + 1);

        await Assert.ThrowsAsync<BusinessRuleException>(() => service.RegisterAsync("alice@example.com", name, "secret123", Ct));
    }

    [Fact]
    public async Task RegisterAsync_RejectsDuplicateEmailIgnoringCase()
    {
        var service = CreateService();
        await service.RegisterAsync("alice@example.com", "Alice", "secret123", Ct);

        var ex = await Assert.ThrowsAsync<BusinessRuleException>(
            () => service.RegisterAsync("ALICE@example.com", "Other Alice", "secret456", Ct));

        Assert.Contains("already exists", ex.Message);
        Assert.Equal(1, await Db.Users.CountDocumentsAsync(FilterDefinition<User>.Empty, cancellationToken: Ct));
    }

    [Fact]
    public async Task UniqueEmailIndex_RejectsDuplicateInserts()
    {
        await Db.Users.InsertOneAsync(new User { Email = "alice@example.com" }, cancellationToken: Ct);

        var ex = await Assert.ThrowsAsync<MongoWriteException>(
            () => Db.Users.InsertOneAsync(new User { Email = "alice@example.com" }, cancellationToken: Ct));

        Assert.Equal(ServerErrorCategory.DuplicateKey, ex.WriteError.Category);
    }

    [Fact]
    public async Task ValidateCredentialsAsync_ReturnsUserForCorrectPassword()
    {
        var service = CreateService();
        var registered = await service.RegisterAsync("alice@example.com", "Alice", "secret123", Ct);

        var user = await service.ValidateCredentialsAsync(" ALICE@example.com ", "secret123", Ct);

        Assert.NotNull(user);
        Assert.Equal(registered.Id, user.Id);
    }

    [Theory]
    [InlineData("alice@example.com", "wrong-password")]
    [InlineData("bob@example.com", "secret123")]
    [InlineData("", "secret123")]
    [InlineData("alice@example.com", "")]
    public async Task ValidateCredentialsAsync_ReturnsNullForInvalidCredentials(string email, string password)
    {
        var service = CreateService();
        await service.RegisterAsync("alice@example.com", "Alice", "secret123", Ct);

        Assert.Null(await service.ValidateCredentialsAsync(email, password, Ct));
    }
}
