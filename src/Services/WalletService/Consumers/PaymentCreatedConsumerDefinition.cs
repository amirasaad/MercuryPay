using MassTransit;

namespace MercuryPay.WalletService.Consumers;

public class PaymentCreatedConsumerDefinition : ConsumerDefinition<PaymentCreatedConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<PaymentCreatedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
            r.Exponential(5, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(30), TimeSpan.FromMilliseconds(500)));
    }
}
