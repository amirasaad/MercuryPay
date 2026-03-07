using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.WalletService.Domain;
using MercuryPay.WalletService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.WalletService.Consumers;

public class PaymentCreatedConsumer(WalletDbContext context, IPublishEndpoint publishEndpoint) : IConsumer<PaymentCreated>
{
    private readonly WalletDbContext _context = context;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;

    public async Task Consume(ConsumeContext<PaymentCreated> context)
    {
        var message = context.Message;
        
        // Find wallets
        var fromWallet = await _context.Wallets
            .Include(w => w.Ledger)
            .FirstOrDefaultAsync(w => w.UserId == message.FromUserId && w.Currency == message.Currency);
            
        var toWallet = await _context.Wallets
            .Include(w => w.Ledger)
            .FirstOrDefaultAsync(w => w.UserId == message.ToUserId && w.Currency == message.Currency);

        if (fromWallet == null)
        {
            // Should publish PaymentFailed?
            // For now, log and return. In real app, we need to handle this.
            return;
        }
        
        if (toWallet == null)
        {
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
            
            // Publish PaymentProcessed event
            // await _publishEndpoint.Publish(new PaymentProcessed(...));
        }
        catch (InvalidOperationException)
        {
            // Insufficient funds
            // Publish PaymentFailed
            // await _publishEndpoint.Publish(new PaymentFailed(...));
        }
    }
}
