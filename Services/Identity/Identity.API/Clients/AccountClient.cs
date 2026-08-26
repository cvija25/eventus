namespace Identity.API.Clients;

public class AccountClient(HttpClient http)
{
    public async Task CreateWalletAsync(Guid userId, CancellationToken ct = default)
    {
        var response = await http.PostAsJsonAsync("wallet", new { userId }, ct);
        response.EnsureSuccessStatusCode();
    }
}
