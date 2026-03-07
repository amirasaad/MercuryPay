var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var paymentDb = postgres.AddDatabase("paymentdb");
var walletDb = postgres.AddDatabase("walletdb");
var lendingDb = postgres.AddDatabase("lendingdb");

var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin();

var paymentService = builder.AddProject<Projects.MercuryPay_PaymentService>("paymentservice")
    .WithReference(paymentDb)
    .WithReference(rabbitmq);

var walletService = builder.AddProject<Projects.MercuryPay_WalletService>("walletservice")
    .WithReference(walletDb)
    .WithReference(rabbitmq);

var lendingService = builder.AddProject<Projects.MercuryPay_LendingService>("lendingservice")
    .WithReference(lendingDb)
    .WithReference(rabbitmq);

builder.AddProject<Projects.MercuryPay_ApiGateway>("apigateway")
    .WithReference(paymentService)
    .WithReference(walletService)
    .WithReference(lendingService);

builder.AddProject<Projects.MercuryPay_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(paymentService)
    .WithReference(walletService)
    .WithReference(lendingService);

builder.Build().Run();
