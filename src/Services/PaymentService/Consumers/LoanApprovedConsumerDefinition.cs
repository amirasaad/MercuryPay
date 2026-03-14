using MassTransit;

namespace MercuryPay.PaymentService.Consumers;

public class LoanApprovedConsumerDefinition : ConsumerDefinition<LoanApprovedConsumer>
{
    public LoanApprovedConsumerDefinition()
    {
        EndpointName = "loan-approved";
    }

    protected override void ConfigureConsumer(
        IReceiveEndpointConfigurator endpointConfigurator,
        IConsumerConfigurator<LoanApprovedConsumer> consumerConfigurator,
        IRegistrationContext context)
    {
        endpointConfigurator.UseMessageRetry(r =>
            r.Exponential(5, TimeSpan.FromMilliseconds(500), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(500)));
    }
}
