using AzureMessage = Azure.Messaging.ServiceBus.ServiceBusMessage;

namespace PurpleExplorer.Helpers;

public static class Extensions
{
    public static AzureMessage CloneMessage(this AzureMessage original)
    {
        return new AzureMessage(original.Body)
        {
            Subject = original.Subject,
            To = original.To,
            SessionId = original.SessionId,
            ContentType = original.ContentType,
            CorrelationId = original.CorrelationId,
            MessageId = original.MessageId,
            PartitionKey = original.PartitionKey,
            ReplyTo = original.ReplyTo,
            ReplyToSessionId = original.ReplyToSessionId,
            TransactionPartitionKey = original.TransactionPartitionKey
        };
    }
}
