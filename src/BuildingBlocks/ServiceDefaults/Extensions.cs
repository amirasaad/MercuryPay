using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Trace;
using MassTransit;
using System.Security.Claims;
using System.Security.Principal;

namespace Microsoft.Extensions.Hosting;

// Adds common Aspire services: service discovery, resilience, health checks, and OpenTelemetry.
// This project should be referenced by each service project in your solution.
// To learn more about using this project, see https://aka.ms/dotnet/aspire/service-defaults
public static class Extensions
{
    private const string HealthEndpointPath = "/health";
    private const string AlivenessEndpointPath = "/alive";

    public static TBuilder AddDefaultAuthentication<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var identitySection = builder.Configuration.GetSection("Identity");

        builder.Services.AddAuthorization();

        if (!identitySection.Exists())
        {
            return builder;
        }

        var disableAuth = identitySection.GetValue<bool>("DisableAuthValidation");

        // Prevent mapping "sub" claim to nameidentifier.
        // JsonWebTokenHandler.DefaultInboundClaimTypeMap.Remove("sub");

        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                var identityUrl = identitySection.GetValue<string>("Authority");
                var audience = identitySection.GetValue<string>("Audience");

                options.Authority = identityUrl;
                options.Audience = audience;
                options.TokenValidationParameters.ValidateAudience = false; 
                options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();

                if (builder.Environment.IsDevelopment() && disableAuth)
                {
                    options.Authority = null;
                    options.TokenValidationParameters.ValidateIssuer = false;
                    options.TokenValidationParameters.ValidateAudience = false;
                    options.TokenValidationParameters.ValidateLifetime = false;
                    options.TokenValidationParameters.RequireSignedTokens = false;
                    options.TokenValidationParameters.ValidateIssuerSigningKey = false;
                    options.TokenValidationParameters.SignatureValidator = (token, parameters) => new Microsoft.IdentityModel.JsonWebTokens.JsonWebToken(token);
                    options.Configuration = new Microsoft.IdentityModel.Protocols.OpenIdConnect.OpenIdConnectConfiguration
                    {
                        Issuer = "dummy"
                    };
                }
            });

        return builder;
    }

    public static TBuilder AddServiceDefaults<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.AddDefaultAuthentication();

        builder.ConfigureOpenTelemetry();

        builder.AddDefaultHealthChecks();

        builder.Services.AddServiceDiscovery();

        builder.Services.ConfigureHttpClientDefaults(http =>
        {
            // Turn on resilience by default
            http.AddStandardResilienceHandler();

            // Turn on service discovery by default
            http.AddServiceDiscovery();
        });

        // Uncomment the following to restrict the allowed schemes for service discovery.
        // builder.Services.Configure<ServiceDiscoveryOptions>(options =>
        // {
        //     options.AllowedSchemes = ["https"];
        // });

        return builder;
    }

    public static TBuilder AddEventBus<TBuilder>(
        this TBuilder builder,
        Action<IBusRegistrationConfigurator>? configure = null,
        Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? configureRabbitMq = null,
        Action<IBusRegistrationContext, IInMemoryBusFactoryConfigurator>? configureInMemory = null
    ) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddMassTransit(x =>
        {
            x.SetEndpointNameFormatter(new KebabCaseEndpointNameFormatter(includeNamespace: true));
            
            configure?.Invoke(x);

            var connectionString = builder.Configuration.GetConnectionString("messaging");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                x.UsingInMemory((context, cfg) =>
                {
                    configureInMemory?.Invoke(context, cfg);
                    cfg.ConfigureEndpoints(context);
                });
                return;
            }

            x.UsingRabbitMq((context, cfg) =>
            {
                ConfigureRabbitMqHost(cfg, connectionString);
                configureRabbitMq?.Invoke(context, cfg);
                cfg.ConfigureEndpoints(context);
            });
        });

        builder.Services.Configure<MassTransitHostOptions>(options =>
        {
            options.WaitUntilStarted = true;
            options.StartTimeout = TimeSpan.FromSeconds(60);
            options.StopTimeout = TimeSpan.FromSeconds(30);
        });

        return builder;
    }

    private static void ConfigureRabbitMqHost(IRabbitMqBusFactoryConfigurator cfg, string connectionString)
    {
        if (Uri.TryCreate(connectionString, UriKind.Absolute, out var uri))
        {
            var vhost = uri.AbsolutePath.Trim('/');
            var host = uri.Host;
            var port = (ushort)(uri.IsDefaultPort ? 5672 : uri.Port);

            cfg.Host(host, port, string.IsNullOrWhiteSpace(vhost) ? "/" : vhost, h =>
            {
                if (!string.IsNullOrWhiteSpace(uri.UserInfo))
                {
                    var parts = uri.UserInfo.Split(':', 2);
                    if (parts.Length >= 1 && !string.IsNullOrWhiteSpace(parts[0]))
                    {
                        h.Username(Uri.UnescapeDataString(parts[0]));
                    }

                    if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[1]))
                    {
                        h.Password(Uri.UnescapeDataString(parts[1]));
                    }
                }
            });

            return;
        }

        cfg.Host(connectionString);
    }

    public static TBuilder ConfigureOpenTelemetry<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Logging.AddOpenTelemetry(logging =>
        {
            logging.IncludeFormattedMessage = true;
            logging.IncludeScopes = true;
        });

        builder.Services.AddOpenTelemetry()
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation()
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation();
            })
            .WithTracing(tracing =>
            {
                tracing.AddSource(builder.Environment.ApplicationName)
                    .AddAspNetCoreInstrumentation(tracing =>
                        // Exclude health check requests from tracing
                        tracing.Filter = context =>
                            !context.Request.Path.StartsWithSegments(HealthEndpointPath)
                            && !context.Request.Path.StartsWithSegments(AlivenessEndpointPath)
                    )
                    // Uncomment the following line to enable gRPC instrumentation (requires the OpenTelemetry.Instrumentation.GrpcNetClient package)
                    //.AddGrpcClientInstrumentation()
                    .AddHttpClientInstrumentation();
            });

        builder.AddOpenTelemetryExporters();

        return builder;
    }

    private static TBuilder AddOpenTelemetryExporters<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        var useOtlpExporter = !string.IsNullOrWhiteSpace(builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]);

        if (useOtlpExporter)
        {
            builder.Services.AddOpenTelemetry().UseOtlpExporter();
        }

        // Uncomment the following lines to enable the Azure Monitor exporter (requires the Azure.Monitor.OpenTelemetry.AspNetCore package)
        //if (!string.IsNullOrEmpty(builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]))
        //{
        //    builder.Services.AddOpenTelemetry()
        //       .UseAzureMonitor();
        //}

        return builder;
    }

    public static TBuilder AddDefaultHealthChecks<TBuilder>(this TBuilder builder) where TBuilder : IHostApplicationBuilder
    {
        builder.Services.AddHealthChecks()
            // Add a default liveness check to ensure app is responsive
            .AddCheck("self", () => HealthCheckResult.Healthy(), ["live"]);

        return builder;
    }

    public static WebApplication MapDefaultEndpoints(this WebApplication app)
    {
        // Adding health checks endpoints to applications in non-development environments has security implications.
        // See https://aka.ms/dotnet/aspire/healthchecks for details before enabling these endpoints in non-development environments.
        
        // For debugging/testing purposes, we map these in all environments for now.
        // if (app.Environment.IsDevelopment())
        {
            // All health checks must pass for app to be considered ready to accept traffic after starting
            app.MapHealthChecks(HealthEndpointPath).AllowAnonymous();

            // Only health checks tagged with the "live" tag must pass for app to be considered alive
            app.MapHealthChecks(AlivenessEndpointPath, new HealthCheckOptions
            {
                Predicate = r => r.Tags.Contains("live")
            }).AllowAnonymous();
        }
        
        // Add dev auth bypass middleware if in development with auth disabled
        // Check if Identity section exists and DisableAuthValidation is set
        var identitySection = app.Configuration.GetSection("Identity");
        if (identitySection.Exists())
        {
            var disableAuth = identitySection.GetValue<bool>("DisableAuthValidation");
            if (app.Environment.IsDevelopment() && disableAuth)
            {
                app.UseMiddleware<DevAuthBypassMiddleware>();
            }
        }

        return app;
    }
}

public class DevAuthBypassMiddleware(RequestDelegate next)
{
    private readonly RequestDelegate _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        // If no auth header is present, create a default identity for development
        if (context.Request.Headers.Authorization.Count == 0)
        {
            var claims = new[]
            {
                new Claim(ClaimTypes.NameIdentifier, "dev-user"),
                new Claim(ClaimTypes.Name, "dev-user"),
                new Claim(ClaimTypes.Role, "User")
            };
            var identity = new ClaimsIdentity(claims, "Dev");
            var principal = new ClaimsPrincipal(identity);
            context.User = principal;
        }
        
        await _next(context);
    }
}
