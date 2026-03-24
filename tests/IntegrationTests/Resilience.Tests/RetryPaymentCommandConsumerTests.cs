using MassTransit;
using MassTransit.Testing;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.PaymentService.Consumers;
using MercuryPay.PaymentService.Domain;
using MercuryPay.PaymentService.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Resilience.Tests;

public class RetryPaymentCommandConsumerTests
{
    [Fact]
    public async Task RetryPaymentCommand_RepublishesPaymentCreated_WithCaptureAttemptIdAsMessageId()
    {
        var services = new ServiceCollection();
        services.AddLogging();

        var connection = new SqliteConnection("DataSource=:memory:");
        await connection.OpenAsync();
        services.AddSingleton(connection);
        services.AddDbContext<PaymentDbContext>((sp, options) =>
            options.UseSqlite(sp.GetRequiredService<SqliteConnection>()));

        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<RetryPaymentCommandConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();
        var consumerHarness = provider.GetRequiredService<IConsumerTestHarness<RetryPaymentCommandConsumer>>();

        using (var scope = provider.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
            await db.Database.EnsureCreatedAsync();
        }

        await harness.Start();

        try
        {
            var paymentId = Guid.NewGuid();
            var captureAttemptId = Guid.NewGuid();

            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
                db.Payments.Add(new Payment(paymentId, "user_a", "user_b", 10m, "USD", "Approved", null));
                await db.SaveChangesAsync();
            }

            await harness.Bus.Publish(new RetryPaymentCommand(paymentId, captureAttemptId, 1, DateTimeOffset.UtcNow, "network"));

            Assert.True(await consumerHarness.Consumed.Any<RetryPaymentCommand>());
            Assert.True(await harness.Published.Any<PaymentCreated>());

            var published = harness.Published.Select<PaymentCreated>().Single(x => x.Context.Message.PaymentId == paymentId);
            Assert.Equal(captureAttemptId, published.Context.MessageId);
        }
        finally
        {
            await harness.Stop();
            await connection.CloseAsync();
        }
    }
}
