using System;
using System.Collections.Generic;
using Azure.Messaging.ServiceBus;

namespace PurpleExplorer.Models;

public class Message
{
    public Message(ServiceBusReceivedMessage azureMessage, bool isDlq)
    {
        Content = azureMessage.Body != null ? azureMessage.Body.ToString() : string.Empty;
        MessageId = azureMessage.MessageId;
        CorrelationId = azureMessage.CorrelationId;
        DeliveryCount = azureMessage.DeliveryCount;
        ContentType = azureMessage.ContentType;
        Label = azureMessage.Subject;
        SequenceNumber = azureMessage.SequenceNumber;
        Size = azureMessage.Body != null ? azureMessage.Body.ToArray().Length : 0;
        TimeToLive = azureMessage.TimeToLive;
        IsDlq = isDlq;
        EnqueueTimeUtc = azureMessage.EnqueuedTime;
        ApplicationProperties = azureMessage.ApplicationProperties;
        DeadLetterReason = (azureMessage.ApplicationProperties.TryGetValue("DeadLetterReason", out object? property)
            ? property.ToString()
            : string.Empty) ?? string.Empty;
    }

    public string MessageId { get; set; }
    public string ContentType { get; set; }
    public string Content { get; set; }
    public string Label { get; set; }
    public long Size { get; set; }
    public string CorrelationId { get; set; }
    public int DeliveryCount { get; set; }
    public long SequenceNumber { get; set; }
    public TimeSpan TimeToLive { get; set; }
    public DateTimeOffset EnqueueTimeUtc { get; set; }
    public string DeadLetterReason { get; set; }
    public bool IsDlq { get; }
    public IReadOnlyDictionary<string, object> ApplicationProperties { get; set; }
}
