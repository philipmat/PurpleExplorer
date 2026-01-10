using System;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using PurpleExplorer.Models;

namespace PurpleExplorer.Helpers;

public abstract class BaseHelper
{
    protected const int MaxRequestItemsPerPage = 100;

    protected ServiceBusAdministrationClient GetManagementClient(ServiceBusConnectionString connectionString)
    {
        if (connectionString.UseManagedIdentity)
        {
            return new ServiceBusAdministrationClient(connectionString.ConnectionString, new DefaultAzureCredential());
        }
        else
        {
            return new ServiceBusAdministrationClient(connectionString.ConnectionString);
        }
    }

    protected ServiceBusReceiver GetMessageReceiver(ServiceBusConnectionString connectionString, string path, ServiceBusReceiveMode receiveMode)
    {
        var client = GetServiceBusClient(connectionString);
        return client.CreateReceiver(path, new ServiceBusReceiverOptions()
        {
            ReceiveMode = receiveMode
        });
    }

    protected ServiceBusSender GetTopicClient(ServiceBusConnectionString connectionString, string path)
    {
        var client = GetServiceBusClient(connectionString);
        return client.CreateSender(path);
    }

    protected ServiceBusSender GetQueueClient(ServiceBusConnectionString connectionString, string queueName)
    {
        var client = GetServiceBusClient(connectionString);
        return client.CreateSender(queueName);
    }

    protected ServiceBusClient GetServiceBusClient(ServiceBusConnectionString connectionString)
    {
        if (connectionString.UseManagedIdentity)
        {
            return new ServiceBusClient(connectionString.ConnectionString, new DefaultAzureCredential());
        }
        else
        {
            return new ServiceBusClient(connectionString.ConnectionString);
        }
    }
}