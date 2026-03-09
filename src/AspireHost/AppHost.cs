var builder = DistributedApplication.CreateBuilder(args);

var postgresBuilder = builder.AddPostgres("postgres");
var ephemeral = Environment.GetEnvironmentVariable("ASPIRE_EPHEMERAL_POSTGRES") == "true";
var volumeName = ephemeral ? $"mercurypay-postgres-data-{Guid.NewGuid():N}" : "mercurypay-postgres-data";
var postgres = postgresBuilder
    .WithDataVolume(volumeName)
    .WithPgAdmin();

var paymentDb = postgres.AddDatabase("paymentdb");
var walletDb = postgres.AddDatabase("walletdb");
var lendingDb = postgres.AddDatabase("lendingdb");
var riskDb = postgres.AddDatabase("riskdb");

var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithManagementPlugin();

var keycloakVolumeName = ephemeral ? $"mercurypay-keycloak-data-{Guid.NewGuid():N}" : "mercurypay-keycloak-data";

var keycloak = builder.AddKeycloak("keycloak", 8080)
    .WithDataVolume(keycloakVolumeName)
    .WithRealmImport("./realms");

var keycloakEndpoint = keycloak.GetEndpoint("http");

var paymentService = builder.AddProject<Projects.MercuryPay_PaymentService>("paymentservice")
    .WithReference(paymentDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Identity__Authority", $"{keycloakEndpoint}/realms/mercury")
    .WithEnvironment("Identity__Audience", "account")
    .WithEnvironment("Identity__DisableAuthValidation", "true")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

var walletService = builder.AddProject<Projects.MercuryPay_WalletService>("walletservice")
    .WithReference(walletDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Identity__Authority", $"{keycloakEndpoint}/realms/mercury")
    .WithEnvironment("Identity__Audience", "account")
    .WithEnvironment("Identity__DisableAuthValidation", "true")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

var lendingService = builder.AddProject<Projects.MercuryPay_LendingService>("lendingservice")
    .WithReference(lendingDb)
    .WithReference(rabbitmq)
    .WithReference(keycloak)
    .WithEnvironment("Identity__Authority", $"{keycloakEndpoint}/realms/mercury")
    .WithEnvironment("Identity__Audience", "account")
    .WithEnvironment("Identity__DisableAuthValidation", "true")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

var riskService = builder.AddProject<Projects.MercuryPay_RiskService>("riskservice")
    .WithReference(riskDb)
    .WithReference(rabbitmq)
    .WithEnvironment("Identity__Authority", $"{keycloakEndpoint}/realms/mercury")
    .WithEnvironment("Identity__Audience", "account")
    .WithEnvironment("Identity__DisableAuthValidation", "true")
    .WithEnvironment("ASPNETCORE_ENVIRONMENT", "Development");

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
