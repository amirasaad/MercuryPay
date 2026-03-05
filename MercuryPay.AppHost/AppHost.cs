var builder = DistributedApplication.CreateBuilder(args);

var apiService = builder.AddProject<Projects.MercuryPay_PaymentService>("paymentservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.MercuryPay_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(apiService)
    .WaitFor(apiService);

builder.Build().Run();
