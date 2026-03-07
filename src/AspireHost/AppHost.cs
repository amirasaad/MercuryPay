var builder = DistributedApplication.CreateBuilder(args);

var paymentService = builder.AddProject<Projects.MercuryPay_PaymentService>("paymentservice")
    .WithHttpHealthCheck("/health");

var walletService = builder.AddProject<Projects.MercuryPay_WalletService>("walletservice")
    .WithHttpHealthCheck("/health");

var lendingService = builder.AddProject<Projects.MercuryPay_LendingService>("lendingservice")
    .WithHttpHealthCheck("/health");

var riskService = builder.AddProject<Projects.MercuryPay_RiskService>("riskservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.MercuryPay_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(paymentService)
    .WaitFor(paymentService);

builder.Build().Run();
