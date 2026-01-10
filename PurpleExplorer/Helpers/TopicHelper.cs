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

public class TopicHelper : BaseHelper, ITopicHelper
{
    private readonly AppSettings _appSettings;

    public TopicHelper(AppSettings appSettings)
    {
        _appSettings = appSettings;
    }

    public async Task<IList<ServiceBusTopic>> GetTopicsAndSubscriptions(ServiceBusConnectionString connectionString)
    {
        var client = GetManagementClient(connectionString);
        var topics = await GetTopicsWithSubscriptions(client);
        return topics;
    }

    private async Task<ServiceBusTopic> CreateTopicWithSubscriptions(ServiceBusAdministrationClient client, TopicProperties topicProperties)
    {
        var topic = new ServiceBusTopic(topicProperties);
        var subscriptions = await GetSubscriptions(client, topicProperties.Name);
        topic.AddSubscriptions(subscriptions.ToArray());
        return topic;
    }

    private async Task<List<ServiceBusTopic>> GetTopicsWithSubscriptions(ServiceBusAdministrationClient client)
    {
        var topicPropertiesList = new List<TopicProperties>();
        
        var allTopics = client.GetTopicsAsync();
        int count = 0;
        await foreach (var topic in allTopics)
        {
            topicPropertiesList.Add(topic);
            count++;
            if (count >= _appSettings.TopicListFetchCount)
                break;
        }

        var topics = await Task.WhenAll(topicPropertiesList
            .Select(async topic => await CreateTopicWithSubscriptions(client, topic)));
        return topics.ToList();
    }

    public async Task<ServiceBusTopic> GetTopic(ServiceBusConnectionString connectionString, string topicPath,
        bool retrieveSubscriptions)
    {
        var client = GetManagementClient(connectionString);
        var busTopic = await client.GetTopicAsync(topicPath);
        var newTopic = new ServiceBusTopic(busTopic.Value);

        if (retrieveSubscriptions)
        {
            var subscriptions = await GetSubscriptions(client, newTopic.Name);
            newTopic.AddSubscriptions(subscriptions.ToArray());
        }
        
        return newTopic;
    }

    public async Task<ServiceBusSubscription> GetSubscription(ServiceBusConnectionString connectionString,
        string topicPath, string subscriptionName)
    {
        var client = GetManagementClient(connectionString);
        var runtimeInfo = await client.GetSubscriptionRuntimePropertiesAsync(topicPath, subscriptionName);

        return new ServiceBusSubscription(runtimeInfo.Value);
    }

    private async Task<IList<ServiceBusSubscription>> GetSubscriptions(
        ServiceBusAdministrationClient client,
        string topicPath)
    {
        IList<ServiceBusSubscription> subscriptions = new List<ServiceBusSubscription>();
        var topicSubscriptions = client.GetSubscriptionsRuntimePropertiesAsync(topicPath);

        await foreach (var sub in topicSubscriptions)
        {
            subscriptions.Add(new ServiceBusSubscription(sub));
        }

        return subscriptions;
    }

    public async Task<IList<Message>> GetMessagesBySubscription(ServiceBusConnectionString connectionString,
        string topicName,
        string subscriptionName)
    {
        await using var client = GetServiceBusClient(connectionString);
        var messageReceiver = client.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        
        IReadOnlyList<ServiceBusReceivedMessage> subscriptionMessages = new List<ServiceBusReceivedMessage>();
        try
        {
            subscriptionMessages = await messageReceiver.PeekMessagesAsync(_appSettings.TopicMessageFetchCount);
        }
        catch (ServiceBusException ex)
        {
            Console.WriteLine($"Error trying to get messages for {topicName} / {subscriptionName}:\n{ex}");
        }

        List<Message> result = subscriptionMessages.Select(message => new Message(message, false)).ToList();
        return result;
    }

    public async Task<IList<Message>> GetDlqMessages(ServiceBusConnectionString connectionString, string topic,
        string subscription)
    {
        var path = $"{topic}/Subscriptions/{subscription}";
        var deadletterPath = $"{path}/$DeadLetterQueue";

        await using var client = GetServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(deadletterPath, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        
        IReadOnlyList<ServiceBusReceivedMessage> receivedMessages = new List<ServiceBusReceivedMessage>();
        try
        {
            receivedMessages = await receiver.PeekMessagesAsync(_appSettings.TopicMessageFetchCount);
        }
        catch (ServiceBusException ex)
        {
            Console.WriteLine($"Error trying to get DQL messages for {topic} / {subscription}:\n{ex}");
        }

        List<Message> result = receivedMessages.Select(message => new Message(message, true)).ToList();
        return result;
    }

    public async Task<NamespaceProperties> GetNamespaceInfo(ServiceBusConnectionString connectionString)
    {
        var client = GetManagementClient(connectionString);
        var result = await client.GetNamespacePropertiesAsync();
        return result.Value;
    }

    public async Task SendMessage(ServiceBusConnectionString connectionString, string topicPath, string content)
    {
        var message = new AzureMessage(content);
        await SendMessage(connectionString, topicPath, message);
    }

    public async Task SendMessage(ServiceBusConnectionString connectionString, string topicPath, AzureMessage message)
    {
        await using var client = GetServiceBusClient(connectionString);
        var sender = client.CreateSender(topicPath);
        await sender.SendMessageAsync(message);
    }

    public async Task DeleteMessage(ServiceBusConnectionString connectionString, string topicPath,
        string subscriptionPath,
        Message message, bool isDlq)
    {
        var path = $"{topicPath}/Subscriptions/{subscriptionPath}";
        path = isDlq ? $"{path}/$DeadLetterQueue" : path;

        await using var client = GetServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(path, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });

        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(_appSettings.TopicMessageFetchCount);
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

    public async Task<long> PurgeMessages(ServiceBusConnectionString connectionString, string topicPath,
        string subscriptionPath,
        bool isDlq)
    {
        var path = $"{topicPath}/Subscriptions/{subscriptionPath}";
        path = isDlq ? $"{path}/$DeadLetterQueue" : path;

        long purgedCount = 0;
        await using var client = GetServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(path, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        });
        var operationTimeout = TimeSpan.FromSeconds(5);
        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(_appSettings.TopicMessageFetchCount, operationTimeout);
            if (messages == null || messages.Count == 0)
            {
                break;
            }

            purgedCount += messages.Count;
        }

        return purgedCount;
    }

    public async Task<long> TransferDlqMessages(ServiceBusConnectionString connectionString, string topicPath,
        string subscriptionPath)
    {
        var path = $"{topicPath}/Subscriptions/{subscriptionPath}/$DeadLetterQueue";

        long transferredCount = 0;
        await using var client = GetServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(path, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
        });
        var sender = client.CreateSender(topicPath);
        
        var operationTimeout = TimeSpan.FromSeconds(5);
        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(_appSettings.TopicMessageFetchCount, operationTimeout);
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

    private async Task<ServiceBusReceivedMessage> PeekDlqMessageBySequenceNumber(ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionPath, long sequenceNumber)
    {
        var path = $"{topicPath}/Subscriptions/{subscriptionPath}/$DeadLetterQueue";

        await using var client = GetServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(path, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });
        var azureMessage = await receiver.PeekMessageAsync(sequenceNumber);

        return azureMessage;
    }

    public async Task ResubmitDlqMessage(ServiceBusConnectionString connectionString, string topicPath,
        string subscriptionPath,
        Message message)
    {
        var azureMessage = await PeekDlqMessageBySequenceNumber(connectionString, topicPath, subscriptionPath,
            message.SequenceNumber);
        var clonedMessage = new AzureMessage(azureMessage);

        await SendMessage(connectionString, topicPath, clonedMessage);

        await DeleteMessage(connectionString, topicPath, subscriptionPath, message, true);
    }

    public async Task DeadletterMessage(ServiceBusConnectionString connectionString, string topicPath,
        string subscriptionPath,
        Message message)
    {
        var path = $"{topicPath}/Subscriptions/{subscriptionPath}";

        await using var client = GetServiceBusClient(connectionString);
        var receiver = client.CreateReceiver(path, new ServiceBusReceiverOptions()
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock
        });

        while (true)
        {
            var messages = await receiver.ReceiveMessagesAsync(_appSettings.TopicMessageFetchCount);
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
}