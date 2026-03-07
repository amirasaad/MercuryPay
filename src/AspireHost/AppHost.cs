var builder = DistributedApplication.CreateBuilder(args);

var messaging = builder.AddRabbitMQ("messaging");

var paymentService = builder.AddProject<Projects.MercuryPay_PaymentService>("paymentservice")
    .WithReference(messaging)
    .WithHttpHealthCheck("/health");

var walletService = builder.AddProject<Projects.MercuryPay_WalletService>("walletservice")
    .WithReference(messaging)
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
