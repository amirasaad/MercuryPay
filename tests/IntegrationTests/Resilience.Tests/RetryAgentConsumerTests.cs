using MassTransit;
using MassTransit.Testing;
using MercuryPay.AgentPlatform.Agents;
using MercuryPay.AgentPlatform.Infrastructure;
using MercuryPay.BuildingBlocks.AgentPlatform;
using MercuryPay.BuildingBlocks.Events;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Resilience.Tests;

public class RetryAgentConsumerTests
{
    [Fact]
    public async Task RetryAgent_PublishesRetryCommand_AndPersistsRetryCount()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var metricSink = new InMemoryAgentTaskMetricSink();
        services.AddSingleton<IAgentTaskMetricSink>(metricSink);

        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        services.AddSingleton(connection);
        services.AddDbContext<AgentPlatformDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<RetryAgentConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        var consumerHarness = provider.GetRequiredService<IConsumerTestHarness<RetryAgentConsumer>>();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgentPlatformDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        await harness.Start();

        try
        {
            var paymentId = Guid.NewGuid();
            var captureAttemptId = Guid.NewGuid();
            await harness.Bus.Publish(new PaymentCaptureFailed(paymentId, captureAttemptId, "network", DateTimeOffset.UtcNow));
            await harness.Bus.Publish(new PaymentCaptureFailed(paymentId, captureAttemptId, "network", DateTimeOffset.UtcNow));

            Assert.True(await consumerHarness.Consumed.Any<PaymentCaptureFailed>());

            var retries = harness.Published.Select<RetryPaymentCommand>().Where(x =>
                x.Context.Message.PaymentId == paymentId && x.Context.Message.CaptureAttemptId == captureAttemptId);

            Assert.Equal(2, retries.Count());
            Assert.Equal(2, metricSink.Snapshot().Count);

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AgentPlatformDbContext>();
            var state = await db.RetryAttempts.FirstOrDefaultAsync(x => x.CaptureAttemptId == captureAttemptId);
            Assert.NotNull(state);
            Assert.Equal(2, state!.RetryCount);
        }
        finally
        {
            await harness.Stop();
            await connection.CloseAsync();
        }
    }

    [Fact]
    public async Task RetryAgent_EscalatesAfterMaxRetries()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var metricSink = new InMemoryAgentTaskMetricSink();
        services.AddSingleton<IAgentTaskMetricSink>(metricSink);

        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        services.AddSingleton(connection);
        services.AddDbContext<AgentPlatformDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<RetryAgentConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        var consumerHarness = provider.GetRequiredService<IConsumerTestHarness<RetryAgentConsumer>>();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgentPlatformDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        await harness.Start();

        try
        {
            var paymentId = Guid.NewGuid();
            var captureAttemptId = Guid.NewGuid();

            for (var i = 0; i < 4; i++)
            {
                await harness.Bus.Publish(
                    new PaymentCaptureFailed(paymentId, captureAttemptId, $"fail-{i}", DateTimeOffset.UtcNow),
                    ctx => ctx.MessageId = Guid.NewGuid()
                );
            }

            Assert.True(await consumerHarness.Consumed.Any<PaymentCaptureFailed>());

            var retries = harness.Published.Select<RetryPaymentCommand>().Where(x =>
                x.Context.Message.PaymentId == paymentId && x.Context.Message.CaptureAttemptId == captureAttemptId);
            Assert.Equal(3, retries.Count());

            var escalations = harness.Published.Select<PaymentRequiresReview>().Where(x =>
                x.Context.Message.PaymentId == paymentId && x.Context.Message.CaptureAttemptId == captureAttemptId);
            Assert.Single(escalations);
            Assert.Equal(4, metricSink.Snapshot().Count);
        }
        finally
        {
            await harness.Stop();
            await connection.CloseAsync();
        }
    }

    [Fact]
    public async Task RetryAgent_EscalatesImmediately_ForInsufficientFunds()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var metricSink = new InMemoryAgentTaskMetricSink();
        services.AddSingleton<IAgentTaskMetricSink>(metricSink);

        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        services.AddSingleton(connection);
        services.AddDbContext<AgentPlatformDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<RetryAgentConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AgentPlatformDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        await harness.Start();

        try
        {
            var paymentId = Guid.NewGuid();
            var captureAttemptId = Guid.NewGuid();

            await harness.Bus.Publish(new PaymentCaptureFailed(paymentId, captureAttemptId, "Insufficient funds", DateTimeOffset.UtcNow));

            var retries = harness.Published.Select<RetryPaymentCommand>().Where(x =>
                x.Context.Message.PaymentId == paymentId && x.Context.Message.CaptureAttemptId == captureAttemptId);
            Assert.Empty(retries);

            var escalations = harness.Published.Select<PaymentRequiresReview>().Where(x =>
                x.Context.Message.PaymentId == paymentId && x.Context.Message.CaptureAttemptId == captureAttemptId);
            Assert.Single(escalations);
        }
        finally
        {
            await harness.Stop();
            await connection.CloseAsync();
        }
    }
}
