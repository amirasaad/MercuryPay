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

    public async Task<List<LoanResponseModel>> GetMyLoansAsync(CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<List<LoanResponseModel>>("/loans", cancellationToken) 
               ?? [];
    }

    public async Task<List<LoanResponseModel>> GetLoansAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _httpClient.GetFromJsonAsync<List<LoanResponseModel>>($"/loans/user/{userId}", cancellationToken) 
               ?? [];
    }

    public async Task<bool> RetryLoanDisbursementAsync(Guid loanId, CancellationToken cancellationToken = default)
    {
        var response = await _httpClient.PostAsync($"/loans/{loanId}/retry", null, cancellationToken);
        return response.IsSuccessStatusCode;
    }
}

public record LoanRequestModel(string UserId, decimal Amount, string Currency);
public record LoanResponseModel(Guid Id, string UserId, decimal Amount, string Currency, string Status, DateTime CreatedAt);
