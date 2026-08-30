using Account.Data;
using Account.Entities;
using Account.Mappings;
using Account.Repositories;
using AutoMapper;
using Eventus.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Account.Tests.Integration;

[CollectionDefinition(Name)]
public class AccountDatabaseCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "account-postgres";
}

/// <summary>
/// Base for tests needing a real Account schema, built by running the service's own migrations.
/// Postgres rather than an in-memory provider on purpose: these tests are about money in
/// <c>numeric</c> columns and about real <c>BEGIN</c>/<c>ROLLBACK</c> semantics, neither of
/// which a fake provider reproduces.
/// </summary>
[Collection(AccountDatabaseCollection.Name)]
public abstract class AccountDatabaseTest(PostgresFixture postgres) : IAsyncLifetime
{
    private string _connectionString = null!;

    protected AccountDbContext Context { get; private set; } = null!;
    protected IWalletRepository Wallets { get; private set; } = null!;
    protected ITransactionRepository Transactions { get; private set; } = null!;

    protected IMapper Mapper { get; } =
        new MapperConfiguration(
            cfg => cfg.AddProfile<TransactionMappingProfile>(),
            new NullLoggerFactory()
        ).CreateMapper();

    public async Task InitializeAsync()
    {
        _connectionString = await postgres.CreateDatabaseAsync(GetType().Name);
        Context = NewContext();
        await Context.Database.MigrateAsync();

        // One context shared by both repositories and the handler under test, matching the
        // scoped lifetime the service registers - the ambient transaction depends on it.
        Wallets = new WalletRepository(Context);
        Transactions = new TransactionRepository(Context, Mapper);
    }

    protected AccountDbContext NewContext() =>
        new(
            new DbContextOptionsBuilder<AccountDbContext>()
                .UseNpgsql(_connectionString, b => b.MigrationsAssembly("Account"))
                .Options
        );

    protected async Task<Guid> GivenWallet(decimal available = 0m, decimal reserved = 0m)
    {
        var accountId = Guid.NewGuid();
        await using var context = NewContext();
        context.Wallets.Add(
            new Wallet
            {
                AccountId = accountId,
                AvailableFunds = available,
                ReserveFunds = reserved,
            }
        );
        await context.SaveChangesAsync();
        return accountId;
    }

    /// <summary>Reads the wallet straight from Postgres, bypassing any tracked state.</summary>
    protected async Task<Wallet> StoredWallet(Guid accountId)
    {
        await using var context = NewContext();
        return await context.Wallets.SingleAsync(w => w.AccountId == accountId);
    }

    protected async Task<List<Transaction>> StoredTransactions(Guid accountId)
    {
        await using var context = NewContext();
        return await context.Transactions.Where(t => t.AccountId == accountId).ToListAsync();
    }

    public async Task DisposeAsync() => await Context.DisposeAsync();
}
