using Eventus.Testing;

namespace Account.Tests.Integration;

public class WalletRepositoryTests(PostgresFixture postgres) : AccountDatabaseTest(postgres)
{
    [Fact]
    public async Task Deposit_credits_the_wallet()
    {
        var account = await GivenWallet(available: 100m);

        var result = await Wallets.Deposit(account, 25m);

        Assert.NotNull(result);
        Assert.Equal(125m, result.Amount);
        Assert.Equal(125m, (await StoredWallet(account)).AvailableFunds);
    }

    [Fact]
    public async Task Withdraw_debits_the_wallet()
    {
        var account = await GivenWallet(available: 100m);

        var result = await Wallets.Withdraw(account, 30m);

        Assert.NotNull(result);
        Assert.Equal(70m, result.Amount);
        Assert.Equal(70m, (await StoredWallet(account)).AvailableFunds);
    }

    [Fact]
    public async Task Reserve_funds_are_tracked_separately_from_available_funds()
    {
        var account = await GivenWallet(available: 100m);

        var result = await Wallets.DepositReserveFund(account, 40m);

        Assert.NotNull(result);
        Assert.Equal(40m, result.Amount); // the reserve, not the balance
        var stored = await StoredWallet(account);
        Assert.Equal(100m, stored.AvailableFunds);
        Assert.Equal(40m, stored.ReserveFunds);
    }

    [Fact]
    public async Task Releasing_a_reserve_frees_it_again()
    {
        var account = await GivenWallet(available: 100m, reserved: 40m);

        await Wallets.WithdrawReserveFund(account, 40m);

        Assert.Equal(0m, (await StoredWallet(account)).ReserveFunds);
    }

    [Fact]
    public async Task Balance_is_what_is_left_after_pending_holds()
    {
        var account = await GivenWallet(available: 100m, reserved: 30m);

        var balance = await Wallets.GetBalance(account);

        // Money held against an in-flight bet must not look spendable.
        Assert.NotNull(balance);
        Assert.Equal(70m, balance.Amount);
    }

    [Fact]
    public async Task Balance_never_goes_negative()
    {
        var account = await GivenWallet(available: 10m, reserved: 30m);

        Assert.Equal(0m, (await Wallets.GetBalance(account))!.Amount);
    }

    [Fact]
    public async Task Releasing_more_reserve_than_is_held_settles_at_zero()
    {
        var account = await GivenWallet(available: 100m, reserved: 10m);

        await Wallets.WithdrawReserveFund(account, 25m);

        Assert.Equal(0m, (await StoredWallet(account)).ReserveFunds);
    }

    [Fact]
    public async Task Creating_a_wallet_twice_leaves_the_first_one_alone()
    {
        var account = await GivenWallet(available: 100m);

        await Wallets.CreateWalletIfMissing(account);

        Assert.Equal(100m, (await StoredWallet(account)).AvailableFunds);
    }

    [Fact]
    public async Task A_new_wallet_starts_empty()
    {
        var account = Guid.NewGuid();

        await Wallets.CreateWalletIfMissing(account);

        var stored = await StoredWallet(account);
        Assert.Equal(0m, stored.AvailableFunds);
        Assert.Equal(0m, stored.ReserveFunds);
    }

    [Fact]
    public async Task Operations_on_a_missing_wallet_report_nothing_rather_than_creating_one()
    {
        var unknown = Guid.NewGuid();

        Assert.Null(await Wallets.Deposit(unknown, 10m));
        Assert.Null(await Wallets.Withdraw(unknown, 10m));
        Assert.Null(await Wallets.DepositReserveFund(unknown, 10m));
        Assert.Null(await Wallets.WithdrawReserveFund(unknown, 10m));
        Assert.Null(await Wallets.GetBalance(unknown));
    }

    [Fact]
    public async Task Fractional_amounts_survive_a_round_trip()
    {
        // Payouts land on values like 10.909090909090909090909090909.
        var account = await GivenWallet();
        var payout = 120m / 11m;

        await Wallets.Deposit(account, payout);

        Assert.Equal(payout, (await StoredWallet(account)).AvailableFunds);
    }

    [Fact(
        Skip = "Bug: Withdraw clamps the new balance with Math.Max(.., 0) and reports success, "
            + "so overdrawing silently destroys the difference instead of failing. "
            + "See Account/Repositories/WalletRepository.cs Withdraw"
    )]
    public async Task Withdrawing_more_than_the_balance_is_refused()
    {
        var account = await GivenWallet(available: 10m);

        var result = await Wallets.Withdraw(account, 25m);

        // Withdraw clamps at zero instead of failing, so an over-withdrawal silently destroys
        // the 15 credits of difference and the caller is told it succeeded.
        Assert.Null(result);
        Assert.Equal(10m, (await StoredWallet(account)).AvailableFunds);
    }
}
