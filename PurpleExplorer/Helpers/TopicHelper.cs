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

public class TopicHelper(AppSettings appSettings) : BaseHelper, ITopicHelper
{
    public async Task<IList<ServiceBusTopic>> GetTopicsAndSubscriptions(ServiceBusConnectionString connectionString)
    {
        ServiceBusAdministrationClient client = GetManagementClient(connectionString);
        List<ServiceBusTopic> topics = await GetTopicsWithSubscriptions(client);
        return topics;
    }

    public async Task<ServiceBusSubscription> GetSubscription(
        ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionName)
    {
        ServiceBusAdministrationClient client = GetManagementClient(connectionString);
        Response<SubscriptionRuntimeProperties>? runtimeInfo =
            await client.GetSubscriptionRuntimePropertiesAsync(topicPath, subscriptionName);

        return new ServiceBusSubscription(runtimeInfo.Value);
    }

    public async Task<IList<Message>> GetMessagesBySubscription(
        ServiceBusConnectionString connectionString,
        string topicName,
        string subscriptionName)
    {
        await using ServiceBusClient client = GetServiceBusClient(connectionString);
        ServiceBusReceiver? messageReceiver = client.CreateReceiver(
            topicName,
            subscriptionName,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

        IReadOnlyList<ServiceBusReceivedMessage> subscriptionMessages = new List<ServiceBusReceivedMessage>();
        try
        {
            subscriptionMessages = await messageReceiver.PeekMessagesAsync(appSettings.TopicMessageFetchCount);
        }
        catch (ServiceBusException ex)
        {
            Console.WriteLine($"Error trying to get messages for {topicName} / {subscriptionName}:\n{ex}");
        }

        List<Message> result = subscriptionMessages.Select(message => new Message(message, false)).ToList();
        return result;
    }

    public async Task<IList<Message>> GetDlqMessages(
        ServiceBusConnectionString connectionString,
        string topic,
        string subscription)
    {
        var path = $"{topic}/Subscriptions/{subscription}";
        var deadLetterPath = $"{path}/$DeadLetterQueue";

        await using ServiceBusClient client = GetServiceBusClient(connectionString);
        ServiceBusReceiver? receiver = client.CreateReceiver(
            deadLetterPath,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });

        IReadOnlyList<ServiceBusReceivedMessage> receivedMessages = new List<ServiceBusReceivedMessage>();
        try
        {
            receivedMessages = await receiver.PeekMessagesAsync(appSettings.TopicMessageFetchCount);
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
        ServiceBusAdministrationClient client = GetManagementClient(connectionString);
        Response<NamespaceProperties>? result = await client.GetNamespacePropertiesAsync();
        return result.Value;
    }

    public async Task SendMessage(ServiceBusConnectionString connectionString, string topicPath, string content)
    {
        var message = new AzureMessage(content);
        await SendMessage(connectionString, topicPath, message);
    }

    public async Task SendMessage(ServiceBusConnectionString connectionString, string topicPath, AzureMessage message)
    {
        await using ServiceBusClient client = GetServiceBusClient(connectionString);
        ServiceBusSender? sender = client.CreateSender(topicPath);
        await sender.SendMessageAsync(message);
    }

    public async Task DeleteMessage(
        ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionPath,
        Message message,
        bool isDlq)
    {
        var path = $"{topicPath}/Subscriptions/{subscriptionPath}";
        path = isDlq ? $"{path}/$DeadLetterQueue" : path;

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
                await receiver.ReceiveMessagesAsync(appSettings.TopicMessageFetchCount);
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

    public async Task<long> PurgeMessages(
        ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionPath,
        bool isDlq)
    {
        var path = $"{topicPath}/Subscriptions/{subscriptionPath}";
        path = isDlq ? $"{path}/$DeadLetterQueue" : path;

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
                appSettings.TopicMessageFetchCount,
                operationTimeout);
            if (messages == null || messages.Count == 0) break;

            purgedCount += messages.Count;
        }

        return purgedCount;
    }

    public async Task<long> TransferDlqMessages(
        ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionPath)
    {
        var path = $"{topicPath}/Subscriptions/{subscriptionPath}/$DeadLetterQueue";

        long transferredCount = 0;
        await using ServiceBusClient client = GetServiceBusClient(connectionString);
        ServiceBusReceiver? receiver = client.CreateReceiver(
            path,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete
            });
        ServiceBusSender? sender = client.CreateSender(topicPath);

        TimeSpan operationTimeout = TimeSpan.FromSeconds(5);
        while (true)
        {
            IReadOnlyList<ServiceBusReceivedMessage>? messages = await receiver.ReceiveMessagesAsync(
                appSettings.TopicMessageFetchCount,
                operationTimeout);
            if (messages == null || messages.Count == 0) break;

            IEnumerable<AzureMessage> messagesToSend = messages.Select(m => new AzureMessage(m));
            await sender.SendMessagesAsync(messagesToSend);

            transferredCount += messages.Count;
        }

        return transferredCount;
    }

    public async Task ResubmitDlqMessage(
        ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionPath,
        Message message)
    {
        ServiceBusReceivedMessage azureMessage = await PeekDlqMessageBySequenceNumber(
            connectionString,
            topicPath,
            subscriptionPath,
            message.SequenceNumber);
        var clonedMessage = new AzureMessage(azureMessage);

        await SendMessage(connectionString, topicPath, clonedMessage);

        await DeleteMessage(connectionString, topicPath, subscriptionPath, message, true);
    }

    public async Task DeadLetterMessage(
        ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionPath,
        Message message)
    {
        var path = $"{topicPath}/Subscriptions/{subscriptionPath}";

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
                await receiver.ReceiveMessagesAsync(appSettings.TopicMessageFetchCount);
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

    private async Task<ServiceBusTopic> CreateTopicWithSubscriptions(
        ServiceBusAdministrationClient client,
        TopicProperties topicProperties)
    {
        var topic = new ServiceBusTopic(topicProperties);
        IList<ServiceBusSubscription> subscriptions = await GetSubscriptions(client, topicProperties.Name);
        topic.AddSubscriptions(subscriptions.ToArray());
        return topic;
    }

    private async Task<List<ServiceBusTopic>> GetTopicsWithSubscriptions(ServiceBusAdministrationClient client)
    {
        var topicPropertiesList = new List<TopicProperties>();

        AsyncPageable<TopicProperties>? allTopics = client.GetTopicsAsync();
        var count = 0;
        await foreach (TopicProperties topic in allTopics)
        {
            topicPropertiesList.Add(topic);
            count++;
            if (count >= appSettings.TopicListFetchCount)
                break;
        }

        ServiceBusTopic[] topics = await Task.WhenAll(
            topicPropertiesList
                .Select(async topic => await CreateTopicWithSubscriptions(client, topic)));
        return topics.ToList();
    }

    public async Task<ServiceBusTopic> GetTopic(
        ServiceBusConnectionString connectionString,
        string topicPath,
        bool retrieveSubscriptions)
    {
        ServiceBusAdministrationClient client = GetManagementClient(connectionString);
        Response<TopicProperties>? busTopic = await client.GetTopicAsync(topicPath);
        var newTopic = new ServiceBusTopic(busTopic.Value);

        if (retrieveSubscriptions)
        {
            IList<ServiceBusSubscription> subscriptions = await GetSubscriptions(client, newTopic.Name);
            newTopic.AddSubscriptions(subscriptions.ToArray());
        }

        return newTopic;
    }

    private async Task<IList<ServiceBusSubscription>> GetSubscriptions(
        ServiceBusAdministrationClient client,
        string topicPath)
    {
        IList<ServiceBusSubscription> subscriptions = new List<ServiceBusSubscription>();
        AsyncPageable<SubscriptionRuntimeProperties>? topicSubscriptions =
            client.GetSubscriptionsRuntimePropertiesAsync(topicPath);

        await foreach (SubscriptionRuntimeProperties sub in topicSubscriptions)
            subscriptions.Add(new ServiceBusSubscription(sub));

        return subscriptions;
    }

    private async Task<ServiceBusReceivedMessage> PeekDlqMessageBySequenceNumber(
        ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionPath,
        long sequenceNumber)
    {
        var path = $"{topicPath}/Subscriptions/{subscriptionPath}/$DeadLetterQueue";

        await using ServiceBusClient client = GetServiceBusClient(connectionString);
        ServiceBusReceiver? receiver = client.CreateReceiver(
            path,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = ServiceBusReceiveMode.PeekLock
            });
        ServiceBusReceivedMessage? azureMessage = await receiver.PeekMessageAsync(sequenceNumber);

        return azureMessage;
    }
}
