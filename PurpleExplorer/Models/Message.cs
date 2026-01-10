using System;
using System.Collections.Generic;
using Azure.Messaging.ServiceBus;

namespace PurpleExplorer.Models;

public class Message
{
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
        
    public Message(ServiceBusReceivedMessage azureMessage, bool isDlq)
    {
        this.Content = azureMessage.Body != null ? azureMessage.Body.ToString() : string.Empty;
        this.MessageId = azureMessage.MessageId;
        this.CorrelationId = azureMessage.CorrelationId;
        this.DeliveryCount = azureMessage.DeliveryCount;
        this.ContentType = azureMessage.ContentType;
        this.Label = azureMessage.Subject;
        this.SequenceNumber = azureMessage.SequenceNumber;
        this.Size = azureMessage.Body != null ? azureMessage.Body.ToArray().Length : 0;
        this.TimeToLive = azureMessage.TimeToLive;
        this.IsDlq = isDlq;
        this.EnqueueTimeUtc = azureMessage.EnqueuedTime;
        this.ApplicationProperties = azureMessage.ApplicationProperties;
        this.DeadLetterReason = azureMessage.ApplicationProperties.ContainsKey("DeadLetterReason")
            ? azureMessage.ApplicationProperties["DeadLetterReason"].ToString()
            : string.Empty;
    }
}