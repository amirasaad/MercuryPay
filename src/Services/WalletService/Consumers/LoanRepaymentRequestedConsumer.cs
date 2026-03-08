using MassTransit;
using MercuryPay.BuildingBlocks.Events;
using MercuryPay.WalletService.Services;

namespace MercuryPay.WalletService.Consumers;

public class LoanRepaymentRequestedConsumer(IWalletService walletService, IPublishEndpoint publishEndpoint, ILogger<LoanRepaymentRequestedConsumer> logger) : IConsumer<LoanRepaymentRequested>
{
    private readonly IWalletService _walletService = walletService;
    private readonly IPublishEndpoint _publishEndpoint = publishEndpoint;
    private readonly ILogger<LoanRepaymentRequestedConsumer> _logger = logger;

    public async Task Consume(ConsumeContext<LoanRepaymentRequested> context)
    {
        var message = context.Message;
        _logger.LogInformation("Processing loan repayment for Loan {LoanId}, User {UserId}", message.LoanId, message.UserId);

        var wallets = _walletService.GetWalletsByUserId(message.UserId);
        var wallet = wallets.FirstOrDefault(w => w.Currency == message.Currency);

        if (wallet == null)
        {
            _logger.LogWarning("No {Currency} wallet found for user {UserId}", message.Currency, message.UserId);
            await _publishEndpoint.Publish(new LoanRepaymentProcessed(message.LoanId, message.UserId, false, "Wallet not found", DateTimeOffset.UtcNow));
            return;
        }

        try
        {
            _walletService.DebitWallet(wallet.Id, message.Amount);
            _logger.LogInformation("Debited {Amount} {Currency} from Wallet {WalletId}", message.Amount, message.Currency, wallet.Id);
            
            await _publishEndpoint.Publish(new LoanRepaymentProcessed(message.LoanId, message.UserId, true, string.Empty, DateTimeOffset.UtcNow));
        }
        catch (InvalidOperationException ex) // Insufficient funds
        {
            _logger.LogWarning("Insufficient funds for repayment of Loan {LoanId}: {Message}", message.LoanId, ex.Message);
            await _publishEndpoint.Publish(new LoanRepaymentProcessed(message.LoanId, message.UserId, false, "Insufficient funds", DateTimeOffset.UtcNow));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process repayment for Loan {LoanId}", message.LoanId);
            throw; // Retry
        }
    }
}
