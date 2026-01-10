using System;
using System.Collections.ObjectModel;

namespace PurpleExplorer.Models;

public class ServiceBusResource
{
    public string? Name { get; set; }
    public DateTimeOffset CreatedTime { get; init; }
    public ServiceBusConnectionString? ConnectionString { get; init; }
    public ObservableCollection<ServiceBusQueue> Queues { get; } = [];
    public ObservableCollection<ServiceBusTopic> Topics { get; } = [];

    public void AddTopics(params ServiceBusTopic[] topics)
    {
        foreach (ServiceBusTopic topic in topics)
        {
            topic.ServiceBus = this;
            Topics.Add(topic);
        }
    }

    public void AddQueues(params ServiceBusQueue[] queues)
    {
        foreach (ServiceBusQueue queue in queues)
        {
            queue.ServiceBus = this;
            Queues.Add(queue);
        }
    }

    public override bool Equals(object? obj)
    {
        var comparingResource = obj as ServiceBusResource;
        return comparingResource != null && string.Equals(Name, comparingResource.Name) &&
               CreatedTime.Equals(comparingResource.CreatedTime);
    }

    // protected bool Equals(ServiceBusResource other) => Name == other.Name && CreatedTime.Equals(other.CreatedTime);

    // public override int GetHashCode() => HashCode.Combine(Name ?? "Uknown", CreatedTime);
}
