using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Common.Enums;
using Eventus.Testing;

namespace Eventus.E2E.Tests;

/// <summary>
/// One trade, followed all the way through the system: a new user registers, funds a wallet,
/// and buys into a market. That single path crosses every service and every transport - HTTP,
/// JWT, Mongo, two Postgres schemas, RabbitMQ in both directions, and the gRPC hop - so it is
/// the test that fails when the services stop fitting together, which is precisely what the
/// per-service tests cannot tell you.
/// </summary>
public class HappyPathTests(EventusFixture system) : IClassFixture<EventusFixture>
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private static async Task<JsonElement> ReadJson(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode,
            $"{(int)response.StatusCode} {response.StatusCode} from {response.RequestMessage?.RequestUri}: {body}"
        );
        return JsonDocument.Parse(body).RootElement.Clone();
    }

    [Fact]
    public async Task A_new_user_can_register_fund_a_wallet_and_buy_into_a_market()
    {
        var email = $"trader-{Guid.NewGuid():N}@example.com";
        const string password = "correct horse battery staple";

        // 1. Registering creates the identity in Mongo and, through a service-to-service call,
        //    a wallet in Account's Postgres.
        var registered = await ReadJson(
            await system.Identity.PostAsJsonAsync(
                "/api/v1/identity/register",
                new
                {
                    name = "Ada",
                    email,
                    password,
                    isAdmin = false,
                }
            )
        );
        var userId = registered.GetProperty("id").GetString();
        Assert.False(string.IsNullOrWhiteSpace(userId));

        // 2. Log in for a real signed token.
        var login = await ReadJson(
            await system.Identity.PostAsJsonAsync("/api/v1/identity/login", new { email, password })
        );
        var token = login.GetProperty("token").GetString()!;

        var bearer = new AuthenticationHeaderValue("Bearer", token);
        system.Catalog.DefaultRequestHeaders.Authorization = bearer;
        system.AccountApi.DefaultRequestHeaders.Authorization = bearer;

        // 3. The token minted by Identity is accepted by Catalog, and the market opens balanced.
        var market = await ReadJson(
            await system.Catalog.PostAsJsonAsync(
                "/api/v1/events",
                new { title = "Will it rain tomorrow?" }
            )
        );
        var eventId = market.GetProperty("id").GetGuid();
        Assert.Equal(userId, market.GetProperty("ownerId").GetString());
        Assert.Equal(1m, market.GetProperty("pot").GetDecimal());
        Assert.Equal(0.5m, market.GetProperty("priceYes").GetDecimal());

        // 4. The wallet created during registration accepts a deposit.
        await ReadJson(
            await system.AccountApi.PostAsJsonAsync(
                "/api/v1/account/deposit",
                new { amount = 100m }
            )
        );
        var funded = await ReadJson(await system.AccountApi.GetAsync("/api/v1/account/balance"));
        Assert.Equal(100m, funded.GetProperty("amount").GetDecimal());

        // 5. Buying publishes to RabbitMQ and returns immediately; everything after this point
        //    happens asynchronously across Game, Catalog.GRPC and back into Account.
        var buy = await system.AccountApi.PostAsJsonAsync(
            "/api/v1/account/buy",
            new
            {
                stake = 10m,
                eventId,
                outcome = (int)MarketOutcome.Yes,
            }
        );
        Assert.Equal(HttpStatusCode.OK, buy.StatusCode);

        // 6. Settlement lands when the Buy transaction appears: Game only publishes its approval
        //    after Catalog has accepted the new pools, so this also proves the gRPC hop ran.
        var settled = await WaitFor.ValueAsync(
            async () =>
            {
                var response = await system.AccountApi.GetAsync("/api/v1/account/transactions");
                if (!response.IsSuccessStatusCode)
                    return null;
                var rows = await response.Content.ReadFromJsonAsync<List<TransactionRow>>(Json);
                return rows is { Count: > 0 } ? rows : null;
            },
            "the bet to settle back into the account",
            TimeSpan.FromSeconds(60)
        );

        var holding = Assert.Single(settled);
        Assert.Equal(eventId, holding.EventId);
        Assert.Equal(MarketOutcome.Yes, holding.Outcome);
        Assert.Equal(TransactionType.Buy, holding.Type);
        // 10 credits into a balanced market mints (1 + stake) - 1/(1 + stake) shares.
        Assert.Equal(11m - 1m / 11m, holding.ShareAmount, precision: 10);

        // 7. The stake is really gone and the hold has been released, not merely re-held.
        var afterTrade = await ReadJson(
            await system.AccountApi.GetAsync("/api/v1/account/balance")
        );
        Assert.Equal(90m, afterTrade.GetProperty("amount").GetDecimal());

        // 8. And the market moved: the pot grew by the stake and Yes got more expensive.
        var traded = await ReadJson(await system.Catalog.GetAsync($"/api/v1/events/{eventId}"));
        Assert.Equal(11m, traded.GetProperty("pot").GetDecimal());
        Assert.Equal(11m, traded.GetProperty("poolNo").GetDecimal());
        Assert.Equal(1m / 11m, traded.GetProperty("poolYes").GetDecimal(), precision: 10);
        Assert.True(traded.GetProperty("priceYes").GetDecimal() > 0.9m);
    }

    private sealed record TransactionRow(
        Guid EventId,
        Guid UserId,
        decimal ShareAmount,
        MarketOutcome Outcome,
        TransactionType Type
    );
}
