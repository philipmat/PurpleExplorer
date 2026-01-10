using Azure.Messaging.ServiceBus.Administration;

namespace PurpleExplorer.Models;

public class ServiceBusSubscription : MessageCollection
{
    public string Name { get; set; }
       
    public ServiceBusTopic Topic { get; set; }

    public ServiceBusSubscription(SubscriptionRuntimeProperties subscription)
        : base(subscription.ActiveMessageCount, subscription.DeadLetterMessageCount)
    {
        Name = subscription.SubscriptionName;
    }
}