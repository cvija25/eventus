using Eventus.Testing;
using Identity.API.DTOs;
using Identity.API.Entities;
using Identity.API.Extensions;
using Identity.API.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Driver;

namespace Identity.Tests.Integration;

[CollectionDefinition(Name)]
public class MongoCollection : ICollectionFixture<MongoFixture>
{
    public const string Name = "identity-mongo";
}

/// <summary>
/// Exercises the repository through the service's own DI wiring, so the unique-email index that
/// AddIdentityServices creates is part of what is under test - the Conflict response on
/// duplicate registration depends on it existing.
/// </summary>
[Collection(MongoCollection.Name)]
public class UserRepositoryTests : IAsyncLifetime
{
    private readonly MongoFixture _mongo;
    private ServiceProvider _services = null!;
    private IUserRepository _repository = null!;

    public UserRepositoryTests(MongoFixture mongo) => _mongo = mongo;

    public Task InitializeAsync()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["ConnectionStrings:IdentityDb"] = _mongo.ConnectionString,
                }
            )
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        services.AddIdentityServices(configuration);

        _services = services.BuildServiceProvider();
        _repository = _services.GetRequiredService<IUserRepository>();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync() => await _services.DisposeAsync();

    /// <summary>Unique per test, since the collection is shared across the class.</summary>
    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static RegisterRequest Registration(string email, bool isAdmin = false) =>
        new("Ada", email, "correct horse battery staple", isAdmin);

    [Fact]
    public async Task A_registered_user_comes_back_with_an_id_and_a_timestamp()
    {
        var email = NewEmail();

        var created = await _repository.CreateUserAsync(Registration(email));

        Assert.True(Guid.TryParse(created.Id, out var id) && id != Guid.Empty);
        Assert.Equal("Ada", created.Name);
        Assert.Equal(email, created.Email);
        Assert.InRange(
            created.CreatedAt,
            DateTime.UtcNow.AddMinutes(-1),
            DateTime.UtcNow.AddMinutes(1)
        );
    }

    [Fact]
    public async Task Registering_stores_a_bcrypt_hash_and_never_the_password()
    {
        var email = NewEmail();
        await _repository.CreateUserAsync(Registration(email));

        var stored = await _repository.FindUserWithHashByEmailAsync(email);

        Assert.NotNull(stored);
        Assert.NotEqual("correct horse battery staple", stored.PasswordHash);
        Assert.StartsWith("$2", stored.PasswordHash); // bcrypt marker
        Assert.True(BCrypt.Net.BCrypt.Verify("correct horse battery staple", stored.PasswordHash));
        Assert.False(BCrypt.Net.BCrypt.Verify("wrong password", stored.PasswordHash));
    }

    [Theory]
    [InlineData(false, "user")]
    [InlineData(true, "admin")]
    public async Task The_role_follows_the_is_admin_flag(bool isAdmin, string expectedRole)
    {
        var created = await _repository.CreateUserAsync(Registration(NewEmail(), isAdmin));

        Assert.Equal(expectedRole, created.Role);
    }

    [Fact]
    public async Task A_user_can_be_looked_up_by_email()
    {
        var email = NewEmail();
        var created = await _repository.CreateUserAsync(Registration(email));

        var found = await _repository.FindByEmailAsync(email);

        Assert.NotNull(found);
        Assert.Equal(created.Id, found.Id);
        Assert.Equal(email, found.Email);
    }

    [Fact]
    public async Task An_unknown_email_finds_nothing()
    {
        Assert.Null(await _repository.FindByEmailAsync(NewEmail()));
        Assert.Null(await _repository.FindUserWithHashByEmailAsync(NewEmail()));
    }

    [Fact]
    public async Task Email_lookup_is_case_sensitive_as_stored()
    {
        var email = NewEmail();
        await _repository.CreateUserAsync(Registration(email));

        // Documents current behaviour: nothing normalises case, so Ada@… and ada@… are two
        // different accounts as far as both lookup and the unique index are concerned.
        Assert.Null(await _repository.FindByEmailAsync(email.ToUpperInvariant()));
    }

    [Fact]
    public async Task Registering_the_same_email_twice_is_a_duplicate_key_error()
    {
        var email = NewEmail();
        await _repository.CreateUserAsync(Registration(email));

        var ex = await Assert.ThrowsAsync<MongoWriteException>(() =>
            _repository.CreateUserAsync(Registration(email))
        );

        // IdentityController turns exactly this code into a 409 Conflict.
        Assert.Equal(11000, ex.WriteError.Code);
    }

    [Fact]
    public async Task Two_registrations_get_distinct_ids()
    {
        var first = await _repository.CreateUserAsync(Registration(NewEmail()));
        var second = await _repository.CreateUserAsync(Registration(NewEmail()));

        Assert.NotEqual(first.Id, second.Id);
    }

    [Fact]
    public async Task The_stored_id_is_the_one_handed_back()
    {
        var email = NewEmail();
        var created = await _repository.CreateUserAsync(Registration(email));

        var collection = _services.GetRequiredService<IMongoCollection<User>>();
        var stored = await collection.Find(u => u.Email == email).SingleAsync();

        Assert.Equal(created.Id, stored.Id.ToString());
    }
}
