using System.Net.Http.Json;

namespace MercuryPay.Web;

public class LendingApiClient(HttpClient httpClient)
{
    private readonly HttpClient _httpClient = httpClient;

    public async Task<LoanResponseModel?> CreateLoanAsync(LoanRequestModel request, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsJsonAsync("/loans", request, cancellationToken);
        
        if (response.IsSuccessStatusCode)
        {
            return await response.Content.ReadFromJsonAsync<LoanResponseModel>(cancellationToken);
        }

        return null;
    }

    public async Task<List<LoanResponseModel>> GetLoansAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<List<LoanResponseModel>>($"/loans/user/{userId}", cancellationToken) 
               ?? new List<LoanResponseModel>();
    }
}

public record LoanRequestModel(string UserId, decimal Amount, string Currency);
public record LoanResponseModel(Guid Id, string UserId, decimal Amount, string Currency, string Status);
