var builder = DistributedApplication.CreateBuilder(args);

var messaging = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin();

var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin();

var paymentDb = postgres.AddDatabase("paymentdb");
var walletDb = postgres.AddDatabase("walletdb");

var paymentService = builder.AddProject<Projects.MercuryPay_PaymentService>("paymentservice")
    .WithReference(messaging)
    .WithReference(paymentDb)
    .WithHttpHealthCheck("/health");

var walletService = builder.AddProject<Projects.MercuryPay_WalletService>("walletservice")
    .WithReference(messaging)
    .WithReference(walletDb)
    .WithHttpHealthCheck("/health");

var lendingService = builder.AddProject<Projects.MercuryPay_LendingService>("lendingservice")
    .WithHttpHealthCheck("/health");

var riskService = builder.AddProject<Projects.MercuryPay_RiskService>("riskservice")
    .WithHttpHealthCheck("/health");

builder.AddProject<Projects.MercuryPay_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithHttpHealthCheck("/health")
    .WithReference(paymentService)
    .WithReference(walletService)
    .WaitFor(paymentService);

builder.Build().Run();
