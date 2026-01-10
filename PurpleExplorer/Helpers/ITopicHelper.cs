using System.Collections.Generic;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus.Administration;
using PurpleExplorer.Models;
using AzureMessage = Azure.Messaging.ServiceBus.ServiceBusMessage;

namespace PurpleExplorer.Helpers;

public interface ITopicHelper
{
    public Task<NamespaceProperties> GetNamespaceInfo(ServiceBusConnectionString connectionString);
    public Task<IList<ServiceBusTopic>> GetTopicsAndSubscriptions(ServiceBusConnectionString connectionString);

    public Task<ServiceBusSubscription> GetSubscription(
        ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionName);

    public Task<IList<Message>> GetDlqMessages(
        ServiceBusConnectionString connectionString,
        string topic,
        string subscription);

    public Task<IList<Message>> GetMessagesBySubscription(
        ServiceBusConnectionString connectionString,
        string topicName,
        string subscriptionName);

    public Task SendMessage(ServiceBusConnectionString connectionString, string topicPath, string content);
    public Task SendMessage(ServiceBusConnectionString connectionString, string topicPath, AzureMessage message);

    public Task DeleteMessage(
        ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionPath,
        Message message,
        bool isDlq);

    public Task<long> PurgeMessages(
        ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionPath,
        bool isDlq);

    public Task<long> TransferDlqMessages(
        ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionPath);

    public Task ResubmitDlqMessage(
        ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionPath,
        Message message);

    public Task DeadLetterMessage(
        ServiceBusConnectionString connectionString,
        string topicPath,
        string subscriptionPath,
        Message message);
}
