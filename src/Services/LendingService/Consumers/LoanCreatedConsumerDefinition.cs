using MassTransit;

namespace MercuryPay.LendingService.Consumers;

public class LoanCreatedConsumerDefinition : ConsumerDefinition<LoanCreatedConsumer>
{
    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<LoanCreatedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        // Retry to handle the window where the publishing transaction has not yet committed
        // when the consumer runs (can occur on the in-memory transport in tests, or on very
        // fast brokers). Short intervals keep test suites snappy while providing resilience.
        endpointConfigurator.UseMessageRetry(r =>
            r.Interval(5, TimeSpan.FromMilliseconds(200)));
    }
}
