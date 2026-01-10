using Azure.Messaging.ServiceBus.Administration;

namespace PurpleExplorer.Models;

public class ServiceBusQueue : MessageCollection
{
    public string Name { get; set; }
    public ServiceBusResource ServiceBus { get; set; }
        
    public ServiceBusQueue(QueueRuntimeProperties runtimeInfo)
        : base(runtimeInfo.ActiveMessageCount, runtimeInfo.DeadLetterMessageCount)
    {
        Name = runtimeInfo.Name;
    }
}