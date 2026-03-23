using MassTransit;
using MassTransit.Testing;
using MercuryPay.AgentPlatform.Agents;
using MercuryPay.BuildingBlocks.AgentPlatform;
using MercuryPay.BuildingBlocks.Events;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Resilience.Tests;

public class RetryAgentConsumerTests
{
    [Fact]
    public async Task RetryAgent_PublishesRetryCommand_Once_ForDuplicateDelivery()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var metricSink = new InMemoryAgentTaskMetricSink();
        services.AddSingleton<IAgentTaskMetricSink>(metricSink);

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<RetryAgentConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        var consumerHarness = provider.GetRequiredService<IConsumerTestHarness<RetryAgentConsumer>>();

        await harness.Start();

        try
        {
            var paymentId = Guid.NewGuid();
            var captureAttemptId = Guid.NewGuid();
            var messageId = Guid.NewGuid();

            var evt = new PaymentCaptureFailed(paymentId, captureAttemptId, "network", DateTimeOffset.UtcNow);

            await harness.Bus.Publish(evt, ctx => ctx.MessageId = messageId);
            await harness.Bus.Publish(evt, ctx => ctx.MessageId = messageId);

            Assert.True(await consumerHarness.Consumed.Any<PaymentCaptureFailed>());

            var retries = harness.Published.Select<RetryPaymentCommand>().Where(x =>
                x.Context.Message.PaymentId == paymentId && x.Context.Message.CaptureAttemptId == captureAttemptId);

            Assert.Single(retries);
            Assert.Equal(2, metricSink.Snapshot().Count);
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task RetryAgent_EscalatesAfterMaxRetries()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        var metricSink = new InMemoryAgentTaskMetricSink();
        services.AddSingleton<IAgentTaskMetricSink>(metricSink);

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<RetryAgentConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        var consumerHarness = provider.GetRequiredService<IConsumerTestHarness<RetryAgentConsumer>>();

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
        }
    }
}
