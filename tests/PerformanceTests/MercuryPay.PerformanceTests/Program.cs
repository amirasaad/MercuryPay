using NBomber.Contracts;
using NBomber.CSharp;
using NBomber.Http.CSharp;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

var handler = new HttpClientHandler();
handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
var httpClient = new HttpClient(handler);

// Default to Lending Service URL from launchSettings.json (https profile)
var baseUrl = args.Length > 0 ? args[0] : "https://localhost:7250"; 

Console.WriteLine($"Targeting Lending Service at: {baseUrl}");

string CreateDummyToken(string userId)
{
    var header = Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"alg\":\"none\",\"typ\":\"JWT\"}"))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    var payload = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{{\"sub\":\"{userId}\",\"name\":\"Perf User\",\"aud\":\"account\",\"iss\":\"dummy\"}}"))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');
    return $"{header}.{payload}.";
}

// Scenario 1: Create Loan
var createLoanScenario = Scenario.Create("create_loan_scenario", async context =>
{
    var userId = $"perf_user_{new Random().Next(1, 1000)}";
    var token = CreateDummyToken(userId);
    
    var request = Http.CreateRequest("POST", $"{baseUrl}/loans")
        .WithHeader("Content-Type", "application/json")
        .WithHeader("Authorization", $"Bearer {token}")
        .WithBody(new StringContent(JsonSerializer.Serialize(new 
        { 
            UserId = userId, 
            Amount = 1000m, 
            Currency = "USD",
            TermMonths = 12
        }), Encoding.UTF8, "application/json"));

    var response = await Http.Send(httpClient, request);

    return response;
})
.WithoutWarmUp()
.WithLoadSimulations(
    Simulation.Inject(rate: 10, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
);

// Scenario 2: Get Loans
var getLoansScenario = Scenario.Create("get_loans_scenario", async context =>
{
    var userId = $"perf_user_{new Random().Next(1, 1000)}";
    var token = CreateDummyToken(userId);
    
    var request = Http.CreateRequest("GET", $"{baseUrl}/loans/user/{userId}")
        .WithHeader("Authorization", $"Bearer {token}");

    var response = await Http.Send(httpClient, request);

    return response;
})
.WithoutWarmUp()
.WithLoadSimulations(
    Simulation.Inject(rate: 20, interval: TimeSpan.FromSeconds(1), during: TimeSpan.FromSeconds(30))
);

NBomberRunner
    .RegisterScenarios(createLoanScenario, getLoansScenario)
    .Run();
