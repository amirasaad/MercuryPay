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
        _logger.LogInformation("Processing PaymentCreated: {PaymentId} from {FromUserId} to {ToUserId} Amount {Amount} {Currency}", 
            message.PaymentId, message.FromUserId, message.ToUserId, message.Amount, message.Currency);
        
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
                // Should publish PaymentFailed?
                _logger.LogWarning("Sender wallet not found for user {UserId} with currency {Currency}. Payment {PaymentId} cannot be processed.", 
                    message.FromUserId, message.Currency, message.PaymentId);
                // For now, log and return. In real app, we need to handle this.
                return;
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

        try
        {
            // Debit from sender
            fromWallet.Debit(message.Amount, message.PaymentId.ToString(), $"Payment to {message.ToUserId}");
            
            // Credit to receiver
            toWallet.Credit(message.Amount, message.PaymentId.ToString(), $"Payment from {message.FromUserId}");
            
            await _context.SaveChangesAsync();
            
            _logger.LogInformation("Payment {PaymentId} processed successfully. Funds transferred.", message.PaymentId);
            
            // Publish PaymentProcessed event
            // await _publishEndpoint.Publish(new PaymentProcessed(...));
        }
        catch (InvalidOperationException ex)
        {
            // Insufficient funds
            _logger.LogWarning(ex, "Payment {PaymentId} failed due to insufficient funds in wallet {WalletId}", 
                message.PaymentId, fromWallet.Id);
            // Publish PaymentFailed
            // await _publishEndpoint.Publish(new PaymentFailed(...));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing payment {PaymentId}", message.PaymentId);
            throw; // Retry via MassTransit
        }
    }
}
