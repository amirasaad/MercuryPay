using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Testing;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.LendingService.Consumers;
using MercuryPay.LendingService.Domain;
using MercuryPay.LendingService.Infrastructure;
using MercuryPay.LendingService.Services;
using MercuryPay.WalletService.Consumers;
using MercuryPay.WalletService.Infrastructure;
using MercuryPay.WalletService.Services;
using MercuryPay.PaymentService.Consumers;
using MercuryPay.PaymentService.Infrastructure;
using MercuryPay.PaymentService.Services;
using MercuryPay.PaymentService.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Data.Sqlite;
using Xunit;

namespace MercuryPay.Workflow.Tests;

public class LoanWorkflowTests
{
    private SqliteConnection CreateAndOpenConnection(string dbName)
    {
        // Use shared cache to allow multiple connections to the same in-memory DB
        var connectionString = $"DataSource={dbName};mode=memory;cache=shared";
        var connection = new SqliteConnection(connectionString);
        connection.Open();
        return connection;
    }

    [Fact]
    public async Task LoanWorkflow_FullCycle_InMemory_ShouldSucceed()
    {
        var lendingDbName = "Lending_" + Guid.NewGuid();
        var walletDbName = "Wallet_" + Guid.NewGuid();
        var paymentDbName = "Payment_" + Guid.NewGuid();

        using var lendingKeepAlive = CreateAndOpenConnection(lendingDbName);
        using var walletKeepAlive = CreateAndOpenConnection(walletDbName);
        using var paymentKeepAlive = CreateAndOpenConnection(paymentDbName);

        // 1. Setup DI
        var services = new ServiceCollection();

        // Logging
        services.AddLogging(builder => builder.AddConsole());

        // DbContexts (Sqlite with shared cache)
        services.AddDbContext<LendingDbContext>(options => options.UseSqlite(lendingKeepAlive.ConnectionString));
        services.AddDbContext<WalletDbContext>(options => options.UseSqlite(walletKeepAlive.ConnectionString));
        services.AddDbContext<PaymentDbContext>(options => options.UseSqlite(paymentKeepAlive.ConnectionString));

        // Domain Services
        services.AddScoped<ILendingService, LendingService.Services.LendingService>();
        services.AddScoped<IWalletService, WalletService.Services.WalletService>();
        services.AddScoped<IPaymentService, PaymentService.Services.PaymentService>();

        // MassTransit Test Harness
        services.AddMassTransitTestHarness(x =>
        {
            // Register Consumers
            x.AddConsumer<LoanCreatedConsumer>();
            
            // LoanApproved is consumed by PaymentService to create a payment
            x.AddConsumer<MercuryPay.PaymentService.Consumers.LoanApprovedConsumer>();
            
            // PaymentCreated is consumed by WalletService to transfer funds
            x.AddConsumer<PaymentCreatedConsumer>();
            
            x.AddConsumer<LoanRepaymentRequestedConsumer>();
            x.AddConsumer<LoanRepaymentProcessedConsumer>();
        });

        var provider = services.BuildServiceProvider();

        // Create Schema
        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<LendingDbContext>().Database.EnsureCreated();
            scope.ServiceProvider.GetRequiredService<WalletDbContext>().Database.EnsureCreated();
            scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.EnsureCreated();
        }

        // 2. Start Test Harness
        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            // 3. Prepare Data
            using var scope = provider.CreateScope();
            var lendingService = scope.ServiceProvider.GetRequiredService<ILendingService>();
            
            var userId = "user-workflow-1";
            var amount = 1000m;
            var currency = "USD";

            // Action: Create Loan
            var loan = await lendingService.CreateLoan(userId, amount, currency);

            Assert.NotNull(loan);
            Assert.Equal("Processing", loan.Status);

            // Wait for LoanCreated event
            Assert.True(await harness.Published.Any<LoanCreated>(), "LoanCreated event should be published");
            
            // Wait for LoanCreatedConsumer to consume
            Assert.True(await harness.Consumed.Any<LoanCreated>(), "LoanCreated event should be consumed");

            // Wait for LoanApproved event (published by LoanCreatedConsumer)
            Assert.True(await harness.Published.Any<LoanApproved>(), "LoanApproved event should be published");

            // Wait for LoanApprovedConsumer (Payment Service) to consume
            Assert.True(await harness.Consumed.Any<LoanApproved>(), "LoanApproved event should be consumed by PaymentService");

            // Wait for PaymentCreated event (published by PaymentService)
            Assert.True(await harness.Published.Any<PaymentCreated>(), "PaymentCreated event should be published");

            // Wait for PaymentCreatedConsumer (Wallet Service) to consume
            Assert.True(await harness.Consumed.Any<PaymentCreated>(), "PaymentCreated event should be consumed by WalletService");

            // Verification: Check Wallet Balance
            using (var verifyScope = provider.CreateScope())
            {
                var ws = verifyScope.ServiceProvider.GetRequiredService<IWalletService>();
                var wallets = ws.GetWalletsByUserId(userId);
                var w = wallets.FirstOrDefault(w => w.Currency == currency);
                Assert.NotNull(w);
                Assert.Equal(amount, w!.Balance);
                
                // Check Loan Status in DB
                var db = verifyScope.ServiceProvider.GetRequiredService<LendingDbContext>();
                var l = await db.Loans.FindAsync(loan.Id);
                Assert.NotNull(l);
                Assert.Equal("Approved", l.Status);
                
                // Check Payment Status in DB
                var pdb = verifyScope.ServiceProvider.GetRequiredService<PaymentDbContext>();
                var p = await pdb.Payments.FirstOrDefaultAsync(p => p.ToUserId == userId && p.Amount == amount);
                Assert.NotNull(p);
                Assert.Equal("Pending", p.Status); // Assuming initial status is Pending
            }

            // 4. Repayment Flow
            // Action: Repay Loan
            using (var repayScope = provider.CreateScope())
            {
                var repayService = repayScope.ServiceProvider.GetRequiredService<ILendingService>();
                var repayResult = await repayService.RepayLoan(loan.Id);
                Assert.True(repayResult, "RepayLoan should return true");
            }

            // Wait for LoanRepaymentRequested event
            Assert.True(await harness.Published.Any<LoanRepaymentRequested>(), "LoanRepaymentRequested event should be published");

            // Wait for LoanRepaymentRequestedConsumer (Wallet Service)
            Assert.True(await harness.Consumed.Any<LoanRepaymentRequested>(), "LoanRepaymentRequested event should be consumed");

            // Wait for LoanRepaymentProcessed event (published by Wallet Service after debit)
            Assert.True(await harness.Published.Any<LoanRepaymentProcessed>(), "LoanRepaymentProcessed event should be published");

            // Wait for LoanRepaymentProcessedConsumer (Lending Service)
            Assert.True(await harness.Consumed.Any<LoanRepaymentProcessed>(), "LoanRepaymentProcessed event should be consumed");

            // Verification: Check Final State
            using (var finalScope = provider.CreateScope())
            {
                var ws = finalScope.ServiceProvider.GetRequiredService<IWalletService>();
                var wallets = ws.GetWalletsByUserId(userId);
                var w = wallets.FirstOrDefault(w => w.Currency == currency);
                Assert.NotNull(w);
                Assert.Equal(0, w!.Balance); // Should be back to 0

                var db = finalScope.ServiceProvider.GetRequiredService<LendingDbContext>();
                var l = await db.Loans.FindAsync(loan.Id);
                Assert.NotNull(l);
                Assert.Equal("Repaid", l.Status);
            }
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task LoanWorkflow_InsufficientFunds_ShouldFailRepayment()
    {
        var lendingDbName = "Lending_" + Guid.NewGuid();
        var walletDbName = "Wallet_" + Guid.NewGuid();
        var paymentDbName = "Payment_" + Guid.NewGuid();

        using var lendingKeepAlive = CreateAndOpenConnection(lendingDbName);
        using var walletKeepAlive = CreateAndOpenConnection(walletDbName);
        using var paymentKeepAlive = CreateAndOpenConnection(paymentDbName);

        // 1. Setup DI
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDbContext<LendingDbContext>(options => options.UseSqlite(lendingKeepAlive.ConnectionString));
        services.AddDbContext<WalletDbContext>(options => options.UseSqlite(walletKeepAlive.ConnectionString));
        services.AddDbContext<PaymentDbContext>(options => options.UseSqlite(paymentKeepAlive.ConnectionString));
        
        services.AddScoped<ILendingService, LendingService.Services.LendingService>();
        services.AddScoped<IWalletService, WalletService.Services.WalletService>();
        services.AddScoped<IPaymentService, PaymentService.Services.PaymentService>();
        
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<LoanCreatedConsumer>();
            x.AddConsumer<MercuryPay.PaymentService.Consumers.LoanApprovedConsumer>();
            x.AddConsumer<PaymentCreatedConsumer>();
            x.AddConsumer<LoanRepaymentRequestedConsumer>();
            x.AddConsumer<LoanRepaymentProcessedConsumer>();
        });

        var provider = services.BuildServiceProvider();

        // Create Schema
        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<LendingDbContext>().Database.EnsureCreated();
            scope.ServiceProvider.GetRequiredService<WalletDbContext>().Database.EnsureCreated();
            scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.EnsureCreated();
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            using var scope = provider.CreateScope();
            var lendingService = scope.ServiceProvider.GetRequiredService<ILendingService>();
            var userId = "user-fail-1";
            var amount = 1000m;
            var currency = "USD";

            // 1. Create and Disburse Loan
            var loan = await lendingService.CreateLoan(userId, amount, currency);
            Assert.True(await harness.Published.Any<LoanCreated>());
            Assert.True(await harness.Consumed.Any<LoanCreated>()); // Approves
            Assert.True(await harness.Published.Any<LoanApproved>());
            Assert.True(await harness.Consumed.Any<LoanApproved>()); // Creates Payment
            Assert.True(await harness.Published.Any<PaymentCreated>());
            Assert.True(await harness.Consumed.Any<PaymentCreated>()); // Credits Wallet

            // 2. Drain Wallet (Simulate user spending money)
            using (var drainScope = provider.CreateScope())
            {
                var walletService = drainScope.ServiceProvider.GetRequiredService<IWalletService>();
                var wallets = walletService.GetWalletsByUserId(userId);
                var w = wallets.FirstOrDefault(w => w.Currency == currency);
                Assert.NotNull(w);
                
                // Manually debit everything
                walletService.DebitWallet(w!.Id, w.Balance);
                
                var updated = walletService.GetWallet(w.Id);
                Assert.Equal(0, updated!.Balance);
            }

            // 3. Attempt Repayment
            using (var repayScope = provider.CreateScope())
            {
                var service = repayScope.ServiceProvider.GetRequiredService<ILendingService>();
                await service.RepayLoan(loan.Id);
            }

            // 4. Verify Failure Flow
            Assert.True(await harness.Published.Any<LoanRepaymentRequested>());
            Assert.True(await harness.Consumed.Any<LoanRepaymentRequested>());
            
            // Should publish LoanRepaymentProcessed with Success=false
            var processedEvents = await harness.Published.SelectAsync<LoanRepaymentProcessed>().ToListAsync();
            var failEvent = processedEvents.FirstOrDefault(e => e.Context.Message.Success == false);
            Assert.NotNull(failEvent);
            Assert.Equal("Insufficient funds", failEvent!.Context.Message.FailureReason);

            Assert.True(await harness.Consumed.Any<LoanRepaymentProcessed>());

            // 5. Verify Loan Status
            using (var verifyScope = provider.CreateScope())
            {
                var db = verifyScope.ServiceProvider.GetRequiredService<LendingDbContext>();
                var l = await db.Loans.FindAsync(loan.Id);
                Assert.Equal("RepaymentFailed", l!.Status);
            }
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Loan_Create_InvalidAmount_ShouldThrow()
    {
        var lendingDbName = "Lending_" + Guid.NewGuid();
        var walletDbName = "Wallet_" + Guid.NewGuid();
        var paymentDbName = "Payment_" + Guid.NewGuid();

        using var lendingKeepAlive = CreateAndOpenConnection(lendingDbName);
        using var walletKeepAlive = CreateAndOpenConnection(walletDbName);
        using var paymentKeepAlive = CreateAndOpenConnection(paymentDbName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDbContext<LendingDbContext>(options => options.UseSqlite(lendingKeepAlive.ConnectionString));
        services.AddDbContext<WalletDbContext>(options => options.UseSqlite(walletKeepAlive.ConnectionString));
        services.AddDbContext<PaymentDbContext>(options => options.UseSqlite(paymentKeepAlive.ConnectionString));
        
        services.AddScoped<ILendingService, LendingService.Services.LendingService>();
        services.AddScoped<IWalletService, WalletService.Services.WalletService>();
        services.AddScoped<IPaymentService, PaymentService.Services.PaymentService>();
        
        services.AddMassTransitTestHarness();

        var provider = services.BuildServiceProvider();

        // Create Schema
        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<LendingDbContext>().Database.EnsureCreated();
            scope.ServiceProvider.GetRequiredService<WalletDbContext>().Database.EnsureCreated();
            scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.EnsureCreated();
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            using var scope = provider.CreateScope();
            var lendingService = scope.ServiceProvider.GetRequiredService<ILendingService>();
            
            await Assert.ThrowsAsync<ArgumentException>(async () => 
                await lendingService.CreateLoan("user-invalid", -100, "USD"));
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task Loan_ConcurrentRepayment_ShouldFail()
    {
        var lendingDbName = "Lending_" + Guid.NewGuid();
        var walletDbName = "Wallet_" + Guid.NewGuid();
        var paymentDbName = "Payment_" + Guid.NewGuid();

        using var lendingKeepAlive = CreateAndOpenConnection(lendingDbName);
        using var walletKeepAlive = CreateAndOpenConnection(walletDbName);
        using var paymentKeepAlive = CreateAndOpenConnection(paymentDbName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDbContext<LendingDbContext>(options => options.UseSqlite(lendingKeepAlive.ConnectionString));
        services.AddDbContext<WalletDbContext>(options => options.UseSqlite(walletKeepAlive.ConnectionString));
        services.AddDbContext<PaymentDbContext>(options => options.UseSqlite(paymentKeepAlive.ConnectionString));
        
        services.AddScoped<ILendingService, LendingService.Services.LendingService>();
        services.AddScoped<IWalletService, WalletService.Services.WalletService>();
        services.AddScoped<IPaymentService, PaymentService.Services.PaymentService>();
        
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<LoanCreatedConsumer>();
            x.AddConsumer<MercuryPay.PaymentService.Consumers.LoanApprovedConsumer>();
            x.AddConsumer<PaymentCreatedConsumer>();
            x.AddConsumer<LoanRepaymentRequestedConsumer>();
            x.AddConsumer<LoanRepaymentProcessedConsumer>();
        });

        var provider = services.BuildServiceProvider();

        // Create Schema
        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<LendingDbContext>().Database.EnsureCreated();
            scope.ServiceProvider.GetRequiredService<WalletDbContext>().Database.EnsureCreated();
            scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.EnsureCreated();
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            using var scope = provider.CreateScope();
            var lendingService = scope.ServiceProvider.GetRequiredService<ILendingService>();
            var userId = "user-concurrent";
            var amount = 1000m;
            var currency = "USD";

            // 1. Create and Disburse Loan
            var loan = await lendingService.CreateLoan(userId, amount, currency);
            
            // Wait for disbursement
            Assert.True(await harness.Published.Any<LoanApproved>());
            Assert.True(await harness.Consumed.Any<LoanApproved>()); // Payment created
            Assert.True(await harness.Published.Any<PaymentCreated>());
            Assert.True(await harness.Consumed.Any<PaymentCreated>()); // Wallet credited

            // 2. Attempt Concurrent Repayment
            var task1 = Task.Run(async () => 
            {
                using var tScope = provider.CreateScope();
                var service = tScope.ServiceProvider.GetRequiredService<ILendingService>();
                return await service.RepayLoan(loan.Id);
            });

            var task2 = Task.Run(async () => 
            {
                using var tScope = provider.CreateScope();
                var service = tScope.ServiceProvider.GetRequiredService<ILendingService>();
                return await service.RepayLoan(loan.Id);
            });

            var results = await Task.WhenAll(task1, task2);

            int successCount = results.Count(x => x == true);
            
            // We expect at most 1 success.
            Assert.True(successCount <= 1, "Only one repayment should succeed");
        }
        finally
        {
            await harness.Stop();
        }
    }

    [Fact]
    public async Task LoanWorkflow_HighLoad_ShouldProcessEfficiently()
    {
        var lendingDbName = "Lending_" + Guid.NewGuid();
        var walletDbName = "Wallet_" + Guid.NewGuid();
        var paymentDbName = "Payment_" + Guid.NewGuid();

        using var lendingKeepAlive = CreateAndOpenConnection(lendingDbName);
        using var walletKeepAlive = CreateAndOpenConnection(walletDbName);
        using var paymentKeepAlive = CreateAndOpenConnection(paymentDbName);

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole());
        services.AddDbContext<LendingDbContext>(options => options.UseSqlite(lendingKeepAlive.ConnectionString));
        services.AddDbContext<WalletDbContext>(options => options.UseSqlite(walletKeepAlive.ConnectionString));
        services.AddDbContext<PaymentDbContext>(options => options.UseSqlite(paymentKeepAlive.ConnectionString));
        
        services.AddScoped<ILendingService, LendingService.Services.LendingService>();
        services.AddScoped<IWalletService, WalletService.Services.WalletService>();
        services.AddScoped<IPaymentService, PaymentService.Services.PaymentService>();
        
        // Increase concurrent message limit for load test
        services.AddMassTransitTestHarness(x =>
        {
            x.AddConsumer<LoanCreatedConsumer>();
            x.AddConsumer<MercuryPay.PaymentService.Consumers.LoanApprovedConsumer>();
            x.AddConsumer<PaymentCreatedConsumer>();
            x.AddConsumer<LoanRepaymentRequestedConsumer>();
            x.AddConsumer<LoanRepaymentProcessedConsumer>();
        });

        var provider = services.BuildServiceProvider();

        // Create Schema
        using (var scope = provider.CreateScope())
        {
            scope.ServiceProvider.GetRequiredService<LendingDbContext>().Database.EnsureCreated();
            scope.ServiceProvider.GetRequiredService<WalletDbContext>().Database.EnsureCreated();
            scope.ServiceProvider.GetRequiredService<PaymentDbContext>().Database.EnsureCreated();
        }

        var harness = provider.GetRequiredService<ITestHarness>();
        await harness.Start();

        try
        {
            int loanCount = 20; 
            var userId = "user-load-test";
            var currency = "USD";
            
            var tasks = new List<Task<Loan>>();

            // 1. Concurrent Loan Creation
            for (int i = 0; i < loanCount; i++)
            {
                int index = i;
                tasks.Add(Task.Run(async () => 
                {
                    using var scope = provider.CreateScope();
                    var service = scope.ServiceProvider.GetRequiredService<ILendingService>();
                    return await service.CreateLoan(userId, 100 + index, currency);
                }));
            }

            var loans = await Task.WhenAll(tasks);
            Assert.Equal(loanCount, loans.Length);

            // 2. Wait for all to be processed (Approved & Disbursed)
            // Wait for PaymentCreated events to be consumed (which means wallet credited)
            var processedCount = await System.Linq.AsyncEnumerable.CountAsync(
                System.Linq.AsyncEnumerable.Take(harness.Consumed.SelectAsync<PaymentCreated>(), loanCount));
            Assert.Equal(loanCount, processedCount);
            
            // Verify all loans are Approved in DB
            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<LendingDbContext>();
                var approvedCount = await db.Loans.CountAsync(l => l.Status == "Approved");
                Assert.Equal(loanCount, approvedCount);
                
                var ws = scope.ServiceProvider.GetRequiredService<IWalletService>();
                var wallet = ws.GetWalletsByUserId(userId).FirstOrDefault(w => w.Currency == currency);
                Assert.NotNull(wallet);
                
                decimal expectedBalance = loans.Sum(l => l.Amount);
                Assert.Equal(expectedBalance, wallet!.Balance);
            }
        }
        finally
        {
            await harness.Stop();
        }
    }
}
