using Azure.Messaging.ServiceBus.Administration;

namespace PurpleExplorer.Models;

public class ServiceBusSubscription(SubscriptionRuntimeProperties subscription) : MessageCollection(
    subscription.ActiveMessageCount,
    subscription.DeadLetterMessageCount)
{
    public string Name { get; set; } = subscription.SubscriptionName;

    public ServiceBusTopic? Topic { get; set; }
}
