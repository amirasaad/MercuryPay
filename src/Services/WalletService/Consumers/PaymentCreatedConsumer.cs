using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.WalletService.Domain;
using MercuryPay.WalletService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.WalletService.Consumers;

public class PaymentCreatedConsumer(WalletDbContext context, IPublishEndpoint publishEndpoint, ILogger<PaymentCreatedConsumer> logger) : IConsumer<PaymentCreated>
{
    private readonly WalletDbContext _context = context;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ILogger<PaymentCreatedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<PaymentCreated> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing PaymentCreated: {PaymentId} (Ref: {ReferenceId}) from {FromUserId} to {ToUserId} Amount {Amount} {Currency}", 
            message.PaymentId, message.ReferenceId, message.FromUserId, message.ToUserId, message.Amount, message.Currency);
        
        // Find wallets
        var fromWallet = await _context.Wallets
            .Include(w => w.Ledger)
            .FirstOrDefaultAsync(w => w.UserId == message.FromUserId && w.Currency == message.Currency);
            
        var toWallet = await _context.Wallets
            .Include(w => w.Ledger)
            .FirstOrDefaultAsync(w => w.UserId == message.ToUserId && w.Currency == message.Currency);

        if (fromWallet == null)
        {
            if (message.FromUserId == "LendingService")
            {
                _logger.LogInformation("Auto-provisioning system wallet for LendingService");
                fromWallet = new Wallet(Guid.NewGuid(), message.FromUserId, message.Currency);
                // Seed with initial capital for lending
                fromWallet.Credit(10000000, "System", "Initial Capital");
                _context.Wallets.Add(fromWallet);
            }
            else
            {
                // Sender wallet does not exist. This should not happen in a correctly
                // deployed system (wallet must be created before initiating a payment).
                // Throwing causes MassTransit to move the message to the error queue
                // after exhausting retries, rather than silently discarding it.
                _logger.LogWarning("Sender wallet not found for user {UserId} with currency {Currency}. Payment {PaymentId} will be retried.",
                    message.FromUserId, message.Currency, message.PaymentId);
                throw new InvalidOperationException($"Sender wallet not found for user '{message.FromUserId}' currency '{message.Currency}'.");
            }
        }
        
        if (toWallet == null)
        {
            _logger.LogInformation("Receiver wallet not found. Auto-provisioning wallet for user {UserId} with currency {Currency}", 
                message.ToUserId, message.Currency);
            // Auto-provision wallet for receiver?
            // Assuming we should create one if it doesn't exist for receiving money
            toWallet = new Wallet(Guid.NewGuid(), message.ToUserId, message.Currency);
            _context.Wallets.Add(toWallet);
        }

        // Apply domain operations first — these throw only for business-rule violations
        // (e.g. insufficient funds) that should NOT be retried.
        try
        {
            fromWallet.Debit(message.Amount, message.PaymentId.ToString(), $"Payment to {message.ToUserId}");
            toWallet.Credit(message.Amount, message.PaymentId.ToString(), $"Payment from {message.FromUserId}");
        }
        catch (InsufficientFundsException ex)
        {
            // Permanent business-rule failure — do not retry.
            _logger.LogWarning(ex, "Payment {PaymentId} rejected: insufficient funds in wallet {WalletId}",
                message.PaymentId, fromWallet.Id);
            // Publish PaymentFailed
            // await _publishEndpoint.Publish(new PaymentFailed(...));
            return;
        }

        // Persist the balance changes — any DB / infrastructure exception propagates so
        // MassTransit retries the message (the domain operations are idempotent via TransactionId).
        await _context.SaveChangesAsync();
        _logger.LogInformation("Payment {PaymentId} processed successfully. Funds transferred.", message.PaymentId);
    }
}
