using System.Net.Http.Json;

namespace MercuryPay.Web;

public class WalletApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<List<WalletModel>> GetWalletsAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<List<WalletModel>>("/wallets", cancellationToken) 
               ?? [];
    }

    public async Task<WalletModel?> CreateWalletAsync(CreateWalletRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/wallets", request, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<WalletModel>(cancellationToken);
        }

        return null;
    }
}

public record WalletModel(Guid Id, string UserId, string Currency, decimal Balance);
public record CreateWalletRequest(string UserId, string Currency);
