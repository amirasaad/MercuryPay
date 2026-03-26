using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.WalletService.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace MercuryPay.WalletService.Consumers;

public class FraudEvaluatedConsumer(WalletDbContext db, IPublishEndpoint publish, ILogger<FraudEvaluatedConsumer> logger) : IConsumer<FraudEvaluated>
{
    private readonly WalletDbContext _db = db;
    private readonly IPublishEndpoint _publish = publish;
    private readonly ILogger<FraudEvaluatedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<FraudEvaluated> context)
    {
        var message = context.Message;
        if (message.IsApproved) return;

        var txId = message.PaymentId.ToString();

        var wallets = await _db.Wallets
            .Include(w => w.Ledger)
            .Where(w => w.Ledger.Any(l => l.TransactionId == txId))
            .ToListAsync(context.CancellationToken);

        if (wallets.Count < 2) return;

        var positive = wallets.FirstOrDefault(w => w.Ledger.Any(l => l.TransactionId == txId && l.Amount > 0));
        var negative = wallets.FirstOrDefault(w => w.Ledger.Any(l => l.TransactionId == txId && l.Amount < 0));
        if (positive == null || negative == null) return;

        var amount = positive.Ledger.First(l => l.TransactionId == txId && l.Amount > 0).Amount;
        var reversalId = $"{message.PaymentId}:reversal";

        try
        {
            positive.Debit(amount, reversalId, $"Fraud reversal for {message.PaymentId}");
            negative.Credit(amount, reversalId, $"Fraud reversal for {message.PaymentId}");
            await _db.SaveChangesAsync(context.CancellationToken);
        }
        catch (Exception ex) when (ex is Domain.InsufficientFundsException)
        {
            var captureAttemptId = context.MessageId ?? Guid.NewGuid();
            await _publish.Publish(new PaymentRequiresReview(
                message.PaymentId,
                captureAttemptId,
                "Insufficient funds for fraud reversal",
                DateTimeOffset.UtcNow
            ), context.CancellationToken);
        }
    }
}
