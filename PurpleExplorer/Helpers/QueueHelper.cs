using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using PurpleExplorer.Models;
using Message = PurpleExplorer.Models.Message;
using AzureMessage = Azure.Messaging.ServiceBus.ServiceBusMessage;

namespace PurpleExplorer.Helpers;

public class QueueHelper(AppSettings appSettings) : BaseHelper, IQueueHelper
{
    public async Task<IList<ServiceBusQueue>> GetQueues(ServiceBusConnectionString connectionString)
    {
        ServiceBusAdministrationClient client = GetManagementClient(connectionString);
        List<ServiceBusQueue> queues = await GetQueues(client);
        return queues;
    }

    public async Task SendMessage(ServiceBusConnectionString connectionString, string queueName, string content)
    {
        var message = new AzureMessage(content);
        await SendMessage(connectionString, queueName, message);
    }

    public async Task SendMessage(ServiceBusConnectionString connectionString, string queueName, AzureMessage message)
    {
        await using ServiceBusClient client = GetServiceBusClient(connectionString);
        ServiceBusSender? sender = client.CreateSender(queueName);
        await sender.SendMessageAsync(message);
    }

    public async Task<IList<Message>> GetMessages(ServiceBusConnectionString connectionString, string queueName)
    {
        await using ServiceBusClient client = GetServiceBusClient(connectionString);
        ServiceBusReceiver? receiver = client.CreateReceiver(
            queueName,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });
        IReadOnlyList<ServiceBusReceivedMessage>? messages =
            await receiver.PeekMessagesAsync(appSettings.QueueMessageFetchCount);
        return messages.Select(msg => new Message(msg, false)).ToList();
    }

    public async Task<IList<Message>> GetDlqMessages(ServiceBusConnectionString connectionString, string queueName)
    {
        var deadletterPath = $"{queueName}/$DeadLetterQueue";

        await using ServiceBusClient client = GetServiceBusClient(connectionString);
        ServiceBusReceiver? receiver = client.CreateReceiver(
            deadletterPath,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });
        IReadOnlyList<ServiceBusReceivedMessage>? receivedMessages =
            await receiver.PeekMessagesAsync(appSettings.QueueMessageFetchCount);

        return receivedMessages.Select(message => new Message(message, true)).ToList();
    }

    public async Task DeadletterMessage(ServiceBusConnectionString connectionString, string queue, Message message)
    {
        await using ServiceBusClient client = GetServiceBusClient(connectionString);
        ServiceBusReceiver? receiver = client.CreateReceiver(
            queue,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

        while (true)
        {
            IReadOnlyList<ServiceBusReceivedMessage>? messages =
                await receiver.ReceiveMessagesAsync(appSettings.QueueMessageFetchCount);
            if (messages == null || messages.Count == 0) break;

            ServiceBusReceivedMessage? foundMessage =
                messages.FirstOrDefault(m => m.MessageId.Equals(message.MessageId));
            if (foundMessage != null)
            {
                await receiver.DeadLetterMessageAsync(foundMessage);
                break;
            }
        }
    }

    public async Task DeleteMessage(
        ServiceBusConnectionString connectionString,
        string queue,
        Message message,
        bool isDlq)
    {
        string path = isDlq ? $"{queue}/$DeadLetterQueue" : queue;

        await using ServiceBusClient client = GetServiceBusClient(connectionString);
        ServiceBusReceiver? receiver = client.CreateReceiver(
            path,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

        while (true)
        {
            IReadOnlyList<ServiceBusReceivedMessage>? messages =
                await receiver.ReceiveMessagesAsync(appSettings.QueueMessageFetchCount);
            if (messages == null || messages.Count == 0) break;

            ServiceBusReceivedMessage? foundMessage =
                messages.FirstOrDefault(m => m.MessageId.Equals(message.MessageId));
            if (foundMessage != null)
            {
                await receiver.CompleteMessageAsync(foundMessage);
                break;
            }
        }
    }

    public async Task ResubmitDlqMessage(ServiceBusConnectionString connectionString, string queue, Message message)
    {
        ServiceBusReceivedMessage azureMessage =
            await PeekDlqMessageBySequenceNumber(connectionString, queue, message.SequenceNumber);
        var clonedMessage = new AzureMessage(azureMessage);

        await SendMessage(connectionString, queue, clonedMessage);

        await DeleteMessage(connectionString, queue, message, true);
    }

    public async Task<long> PurgeMessages(ServiceBusConnectionString connectionString, string queue, bool isDlq)
    {
        string path = isDlq ? $"{queue}/$DeadLetterQueue" : queue;

        long purgedCount = 0;

        await using ServiceBusClient client = GetServiceBusClient(connectionString);
        ServiceBusReceiver? receiver = client.CreateReceiver(
            path,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
            });
        TimeSpan operationTimeout = TimeSpan.FromSeconds(5);
        while (true)
        {
            IReadOnlyList<ServiceBusReceivedMessage>? messages = await receiver.ReceiveMessagesAsync(
                appSettings.QueueMessageFetchCount,
                operationTimeout);
            if (messages == null || messages.Count == 0) break;

            purgedCount += messages.Count;
        }

        return purgedCount;
    }

    public async Task<long> TransferDlqMessages(ServiceBusConnectionString connectionString, string queuePath)
    {
        var path = $"{queuePath}/$DeadLetterQueue";

        long transferredCount = 0;
        await using ServiceBusClient client = GetServiceBusClient(connectionString);
        ServiceBusReceiver? receiver = client.CreateReceiver(
            path,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
            });
        ServiceBusSender? sender = client.CreateSender(queuePath);

        TimeSpan operationTimeout = TimeSpan.FromSeconds(5);
        while (true)
        {
            IReadOnlyList<ServiceBusReceivedMessage>? messages = await receiver.ReceiveMessagesAsync(
                appSettings.QueueMessageFetchCount,
                operationTimeout);
            if (messages == null || messages.Count == 0) break;

            IEnumerable<AzureMessage> messagesToSend = messages.Select(m => new AzureMessage(m));
            await sender.SendMessagesAsync(messagesToSend);

            transferredCount += messages.Count;
        }

        return transferredCount;
    }

    public async Task<ServiceBusQueue> GetQueue(ServiceBusConnectionString connectionString, string queuePath)
    {
        ServiceBusAdministrationClient client = GetManagementClient(connectionString);
        Response<QueueRuntimeProperties>? runtimeInfo = await client.GetQueueRuntimePropertiesAsync(queuePath);
        return new ServiceBusQueue(runtimeInfo.Value)
        {
            Name = runtimeInfo.Value.Name
        };
    }

    private async Task<ServiceBusReceivedMessage> PeekDlqMessageBySequenceNumber(
        ServiceBusConnectionString connectionString,
        string queue,
        long sequenceNumber)
    {
        var deadletterPath = $"{queue}/$DeadLetterQueue";

        await using ServiceBusClient client = GetServiceBusClient(connectionString);
        ServiceBusReceiver? receiver = client.CreateReceiver(
            deadletterPath,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });
        ServiceBusReceivedMessage? azureMessage = await receiver.PeekMessageAsync(sequenceNumber);

        return azureMessage;
    }

    private async Task<List<ServiceBusQueue>> GetQueues(ServiceBusAdministrationClient client)
    {
        var queueInfos = new List<QueueRuntimeProperties>();
        int numberOfPages = appSettings.QueueListFetchCount / MaxRequestItemsPerPage;
        int remainder = appSettings.QueueListFetchCount % (numberOfPages * MaxRequestItemsPerPage);

        AsyncPageable<QueueRuntimeProperties>? allQueues = client.GetQueuesRuntimePropertiesAsync();
        var count = 0;
        await foreach (QueueRuntimeProperties queue in allQueues)
        {
            queueInfos.Add(queue);
            count++;
            if (count >= appSettings.QueueListFetchCount)
                break;
        }

        return queueInfos.Select(q => new ServiceBusQueue(q)
            {
                Name = q.Name
            })
            .ToList();
    }
}
