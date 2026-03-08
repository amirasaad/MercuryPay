var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithPgAdmin();

var paymentDb = postgres.AddDatabase("paymentdb");
var walletDb = postgres.AddDatabase("walletdb");
var lendingDb = postgres.AddDatabase("lendingdb");
var riskDb = postgres.AddDatabase("riskdb");

var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin();

var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume()
    .WithRealmImport("./realms");

var keycloakEndpoint = keycloak.GetEndpoint("http");

var paymentService = builder.AddProject<Projects.MercuryPay_PaymentService>("paymentservice")
    .WithReference(paymentDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Identity__Authority", $"{keycloakEndpoint}/realms/mercury")
    .WithEnvironment("Identity__Audience", "account");

var walletService = builder.AddProject<Projects.MercuryPay_WalletService>("walletservice")
    .WithReference(walletDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Identity__Authority", $"{keycloakEndpoint}/realms/mercury")
    .WithEnvironment("Identity__Audience", "account");

var lendingService = builder.AddProject<Projects.MercuryPay_LendingService>("lendingservice")
    .WithReference(lendingDb)
    .WithReference(rabbitmq)
    .WithReference(keycloak)
    .WithEnvironment("Identity__Authority", $"{keycloakEndpoint}/realms/mercury")
    .WithEnvironment("Identity__Audience", "account");

var riskService = builder.AddProject<Projects.MercuryPay_RiskService>("riskservice")
    .WithReference(riskDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Identity__Authority", $"{keycloakEndpoint}/realms/mercury")
    .WithEnvironment("Identity__Audience", "account");

builder.AddProject<Projects.MercuryPay_ApiGateway>("apigateway")
    .WithReference(paymentService)
    .WithReference(walletService)
    .WithReference(lendingService);

builder.AddProject<Projects.MercuryPay_Web>("webfrontend")
    .WithExternalHttpEndpoints()
    .WithReference(paymentService)
    .WithReference(walletService)
    .WithReference(lendingService)
    .WithReference(keycloak)
    .WithEnvironment("Identity__Authority", $"{keycloakEndpoint}/realms/mercury")
    .WithEnvironment("Identity__Audience", "account");

builder.Build().Run();
