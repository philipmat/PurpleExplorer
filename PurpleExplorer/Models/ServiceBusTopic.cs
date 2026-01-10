using System.Collections.ObjectModel;
using Azure.Messaging.ServiceBus.Administration;

namespace PurpleExplorer.Models;

public class ServiceBusTopic()
{
    public ServiceBusTopic(TopicProperties topicDescription) : this()
    {
        Name = topicDescription.Name;
    }

    public string Name { get; set; } = string.Empty;
    public ObservableCollection<ServiceBusSubscription> Subscriptions { get; } = [];
    public ServiceBusResource? ServiceBus { get; set; }


    public void AddSubscriptions(params ServiceBusSubscription[] subscriptions)
    {
        foreach (ServiceBusSubscription subscription in subscriptions)
        {
            subscription.Topic = this;
            Subscriptions.Add(subscription);
        }
    }
}
