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
        var managementConnectionString = GetManagementConnectionString(connectionString.ConnectionString);

        if (connectionString.UseManagedIdentity)
            return new ServiceBusAdministrationClient(managementConnectionString, new DefaultAzureCredential());

        return new ServiceBusAdministrationClient(managementConnectionString);
    }

    protected ServiceBusReceiver GetMessageReceiver(
        ServiceBusConnectionString connectionString,
        string path,
        ServiceBusReceiveMode receiveMode)
    {
        ServiceBusClient client = GetServiceBusClient(connectionString);
        return client.CreateReceiver(
            path,
            new ServiceBusReceiverOptions
            {
                ReceiveMode = receiveMode
            });
    }

    protected ServiceBusSender GetTopicClient(ServiceBusConnectionString connectionString, string path)
    {
        ServiceBusClient client = GetServiceBusClient(connectionString);
        return client.CreateSender(path);
    }

    protected ServiceBusSender GetQueueClient(ServiceBusConnectionString connectionString, string queueName)
    {
        ServiceBusClient client = GetServiceBusClient(connectionString);
        return client.CreateSender(queueName);
    }

    protected ServiceBusClient GetServiceBusClient(ServiceBusConnectionString connectionString)
    {
        var messagingConnectionString = GetMessagingConnectionString(connectionString.ConnectionString);

        if (connectionString.UseManagedIdentity)
            return new ServiceBusClient(messagingConnectionString, new DefaultAzureCredential());

        return new ServiceBusClient(messagingConnectionString);
    }

    private string GetManagementConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return string.Empty;

        // If emulator mode, ensure port 5300 for management operations
        if (connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase))
        {
            return EnsurePort(connectionString, "5300");
        }
        return connectionString;
    }

    private string GetMessagingConnectionString(string? connectionString)
    {
        if (string.IsNullOrEmpty(connectionString))
            return string.Empty;

        // If emulator mode, ensure port 5672 for messaging operations
        if (connectionString.Contains("UseDevelopmentEmulator=true", StringComparison.OrdinalIgnoreCase))
        {
            return EnsurePort(connectionString, "5672");
        }
        return connectionString;
    }

    private string EnsurePort(string connectionString, string port)
    {
        // Parse the connection string to modify the Endpoint
        var parts = connectionString.Split(';');
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].StartsWith("Endpoint=", StringComparison.OrdinalIgnoreCase))
            {
                var endpoint = parts[i].Substring("Endpoint=".Length);

                // Remove any existing port
                if (endpoint.Contains(":"))
                {
                    var uriParts = endpoint.Split(':');
                    if (uriParts.Length >= 2)
                    {
                        // Reconstruct without port: sb://hostname
                        endpoint = $"{uriParts[0]}:{uriParts[1]}";
                    }
                }

                // Add the specified port
                parts[i] = $"Endpoint={endpoint}:{port}";
                break;
            }
        }

        return string.Join(";", parts);
    }
}
