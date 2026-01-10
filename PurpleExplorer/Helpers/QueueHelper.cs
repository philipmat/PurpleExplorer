using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using PurpleExplorer.Models;
using Message = PurpleExplorer.Models.Message;
using AzureMessage = Azure.Messaging.ServiceBus.ServiceBusMessage;

namespace PurpleExplorer.Helpers;

public class QueueHelper : BaseHelper, IQueueHelper
{
    private readonly AppSettings _appSettings;

    public QueueHelper(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public async Task<IList<ServiceBusQueue>> GetQueues(ServiceBusConnectionString connectionString)
    {
        var client = GetManagementClient(connectionString);
        var queues = await GetQueues(client);
        return queues;
    }

    public async Task SendMessage(ServiceBusConnectionString connectionString, string queueName, string content)
    {
        var message = new AzureMessage(content);
        await SendMessage(connectionString, queueName, message);
    }

    public async Task SendMessage(ServiceBusConnectionString connectionString, string queueName, AzureMessage message)
    {
        await using var client = GetServiceBusClient(connectionString);
        var sender = client.CreateSender(queueName);
        await sender.SendMessageAsync(message);
    }

    public async Task<IList<Message>> GetMessages(ServiceBusConnectionString connectionString, string queueName)
    {
        await using var client = GetServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(queueName, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        var messages = await receiver.PeekMessagesAsync(_appSettings.QueueMessageFetchCount);
        return messages.Select(msg => new Message(msg, false)).ToList();
    }

    public async Task<IList<Message>> GetDlqMessages(ServiceBusConnectionString connectionString, string queueName)
    {
        var deadletterPath = $"{queueName}/$DeadLetterQueue";

        await using var client = GetServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(deadletterPath, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        var receivedMessages = await receiver.PeekMessagesAsync(_appSettings.QueueMessageFetchCount);

        return receivedMessages.Select(message => new Message(message, true)).ToList();
    }

    public async Task DeadletterMessage(ServiceBusConnectionString connectionString, string queue, Message message)
    {
        await using var client = GetServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(queue, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });

        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(_appSettings.QueueMessageFetchCount);
            if (messages == null || messages.Count == 0)
            {
                break;
            }

            var foundMessage = messages.FirstOrDefault(m => m.MessageId.Equals(message.MessageId));
            if (foundMessage != null)
            {
                await receiver.DeadLetterMessageAsync(foundMessage);
                break;
            }
        }
    }

    public async Task DeleteMessage(ServiceBusConnectionString connectionString, string queue,
        Message message, bool isDlq)
    {
        var path = isDlq ? $"{queue}/$DeadLetterQueue" : queue;

        await using var client = GetServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(path, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });

        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(_appSettings.QueueMessageFetchCount);
            if (messages == null || messages.Count == 0)
            {
                break;
            }

            var foundMessage = messages.FirstOrDefault(m => m.MessageId.Equals(message.MessageId));
            if (foundMessage != null)
            {
                await receiver.CompleteMessageAsync(foundMessage);
                break;
            }
        }
    }

    private async Task<ServiceBusReceivedMessage> PeekDlqMessageBySequenceNumber(ServiceBusConnectionString connectionString,
        string queue, long sequenceNumber)
    {
        var deadletterPath = $"{queue}/$DeadLetterQueue";

        await using var client = GetServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(deadletterPath, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        var azureMessage = await receiver.PeekMessageAsync(sequenceNumber);

        return azureMessage;
    }

    public async Task ResubmitDlqMessage(ServiceBusConnectionString connectionString, string queue, Message message)
    {
        var azureMessage = await PeekDlqMessageBySequenceNumber(connectionString, queue, message.SequenceNumber);
        var clonedMessage = new AzureMessage(azureMessage);

        await SendMessage(connectionString, queue, clonedMessage);

        await DeleteMessage(connectionString, queue, message, true);
    }

    public async Task<long> PurgeMessages(ServiceBusConnectionString connectionString, string queue, bool isDlq)
    {
        var path = isDlq ? $"{queue}/$DeadLetterQueue" : queue;

        long purgedCount = 0;

        await using var client = GetServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(path, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        });
        var operationTimeout = TimeSpan.FromSeconds(5);
        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(_appSettings.QueueMessageFetchCount, operationTimeout);
            if (messages == null || messages.Count == 0)
            {
                break;
            }

            purgedCount += messages.Count;
        }

        return purgedCount;
    }

    public async Task<long> TransferDlqMessages(ServiceBusConnectionString connectionString, string queuePath)
    {
        var path = $"{queuePath}/$DeadLetterQueue";

        long transferredCount = 0;
        await using var client = GetServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(path, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        });
        var sender = client.CreateSender(queuePath);
        
        var operationTimeout = TimeSpan.FromSeconds(5);
        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(_appSettings.QueueMessageFetchCount, operationTimeout);
            if (messages == null || messages.Count == 0)
            {
                break;
            }

            var messagesToSend = messages.Select(m => new AzureMessage(m));
            await sender.SendMessagesAsync(messagesToSend);

            transferredCount += messages.Count;
        }

        return transferredCount;
    }
    
    public async Task<ServiceBusQueue> GetQueue(ServiceBusConnectionString connectionString, string queuePath)
    {
        var client = GetManagementClient(connectionString);
        var runtimeInfo = await client.GetQueueRuntimePropertiesAsync(queuePath);
        return new ServiceBusQueue(runtimeInfo.Value)
        {
            Name = runtimeInfo.Value.Name
        };
    }

    private async Task<List<ServiceBusQueue>> GetQueues(ServiceBusAdministrationClient client)
    {
        var queueInfos = new List<QueueRuntimeProperties>();
        var numberOfPages = _appSettings.QueueListFetchCount / MaxRequestItemsPerPage;
        var remainder = _appSettings.QueueListFetchCount % (numberOfPages * MaxRequestItemsPerPage);

        var allQueues = client.GetQueuesRuntimePropertiesAsync();
        int count = 0;
        await foreach (var queue in allQueues)
        {
            queueInfos.Add(queue);
            count++;
            if (count >= _appSettings.QueueListFetchCount)
                break;
        }

        return queueInfos.Select(q => new ServiceBusQueue(q)
        {
            Name = q.Name
        }).ToList();
    }
}