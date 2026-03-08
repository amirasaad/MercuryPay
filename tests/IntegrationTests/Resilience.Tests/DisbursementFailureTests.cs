using System;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Testing;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.LendingService.Consumers;
using MercuryPay.LendingService.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using MercuryPay.LendingService.Domain;

namespace Resilience.Tests;

public class DisbursementFailureTests
{
    [Fact]
    public async Task LendingService_ShouldUpdateLoanStatus_WhenDisbursementFails()
    {
        // Arrange
        var services = new ServiceCollection();
        
        // Setup In-Memory Lending Db
        services.AddDbContext<LendingDbContext>(options =>
            options.UseInMemoryDatabase("LendingDb_ResilienceTest"));
            
        services.AddLogging();
        
        // Add MassTransit Test Harness
        services.AddMassTransitTestHarness(x =>
        {
            // Register the Fault Consumer (The one we are testing)
            x.AddConsumer<LoanApprovedFaultConsumer>();
            
            // Register a Failing Consumer to simulate Payment Service failure
            x.AddConsumer<FailingLoanApprovedConsumer>();
        });

        var provider = services.BuildServiceProvider();
        var harness = provider.GetRequiredService<ITestHarness>();

        await harness.Start();

        try
        {
            // Seed a Loan
            var dbContext = provider.GetRequiredService<LendingDbContext>();
            var loanId = Guid.NewGuid();
            var loan = new Loan(loanId, "user_123", 1000, "USD", "Approved", DateTime.UtcNow, 12, 0.05m);
            dbContext.Loans.Add(loan);
            await dbContext.SaveChangesAsync();

            // Act
            // Publish LoanApproved event
            // The FailingLoanApprovedConsumer will consume it, throw an exception,
            // and MassTransit will generate a Fault<LoanApproved>.
            // Then LoanApprovedFaultConsumer should pick up the fault.
            
            var originalMessage = new LoanApproved(loanId, "user_123", 1000, "USD", DateTimeOffset.UtcNow);
            
            await harness.Bus.Publish(originalMessage);

            // Assert
            // 1. Verify the message was consumed by the failing consumer
            Assert.True(await harness.Consumed.Any<LoanApproved>());
            
            // 2. Verify a Fault<LoanApproved> was published (by MassTransit error handling)
            Assert.True(await harness.Published.Any<Fault<LoanApproved>>());
            
            // 3. Verify the Fault was consumed by our recovery consumer
            Assert.True(await harness.Consumed.Any<Fault<LoanApproved>>());

            // 4. Check the database for status update
            // We need to create a new scope or context to verify persistence
            var checkContext = provider.CreateScope().ServiceProvider.GetRequiredService<LendingDbContext>();
            var updatedLoan = await checkContext.Loans.FindAsync(loanId);
            
            Assert.NotNull(updatedLoan);
            Assert.Equal("DisbursementFailed", updatedLoan.Status);
        }
        finally
        {
            await harness.Stop();
        }
    }
    
    // Simulates the PaymentService consumer failing
    public class FailingLoanApprovedConsumer : IConsumer<LoanApproved>
    {
        public Task Consume(ConsumeContext<LoanApproved> context)
        {
            throw new Exception("Simulated Payment Service Failure");
        }
    }
}
