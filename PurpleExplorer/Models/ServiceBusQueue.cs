using Azure.Messaging.ServiceBus.Administration;

namespace PurpleExplorer.Models;

public class ServiceBusQueue(QueueRuntimeProperties runtimeInfo) : MessageCollection(
    runtimeInfo.ActiveMessageCount,
    runtimeInfo.DeadLetterMessageCount)
{
    public string Name { get; set; } = runtimeInfo.Name;
    public ServiceBusResource? ServiceBus { get; set; }
}
