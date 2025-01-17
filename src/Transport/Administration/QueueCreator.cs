﻿namespace NServiceBus.Transport.AzureServiceBus
{
    using System;
    using System.Threading;
    using System.Threading.Tasks;
    using Microsoft.Azure.ServiceBus;
    using Microsoft.Azure.ServiceBus.Management;

    class QueueCreator
    {
        readonly AzureServiceBusTransport transportSettings;
        readonly ServiceBusConnectionStringBuilder connectionStringBuilder;
        readonly NamespacePermissions namespacePermissions;
        readonly int maxSizeInMb;
        readonly TimeSpan autoDeleteOnIdle;

        public QueueCreator(
            AzureServiceBusTransport transportSettings,
            ServiceBusConnectionStringBuilder connectionStringBuilder,
            NamespacePermissions namespacePermissions)
        {
            this.transportSettings = transportSettings;
            this.connectionStringBuilder = connectionStringBuilder;
            this.namespacePermissions = namespacePermissions;
            maxSizeInMb = transportSettings.EntityMaximumSize * 1024;
            autoDeleteOnIdle = transportSettings.AutoDeleteOnIdle;
        }

        public async Task CreateQueues(string[] queues, CancellationToken cancellationToken = default)
        {
            await namespacePermissions.CanManage(cancellationToken).ConfigureAwait(false);

            var client = new ManagementClient(connectionStringBuilder, transportSettings.TokenProvider);

            try
            {
                var topic = new TopicDescription(transportSettings.TopicName)
                {
                    EnableBatchedOperations = true,
                    EnablePartitioning = transportSettings.EnablePartitioning,
                    MaxSizeInMB = maxSizeInMb,
                    AutoDeleteOnIdle = autoDeleteOnIdle
                };

                try
                {
                    await client.CreateTopicAsync(topic, cancellationToken).ConfigureAwait(false);
                }
                catch (MessagingEntityAlreadyExistsException)
                {
                }
                // TODO: refactor when https://github.com/Azure/azure-service-bus-dotnet/issues/525 is fixed
                catch (ServiceBusException sbe) when (sbe.Message.Contains("SubCode=40901.")) // An operation is in progress.
                {
                }

                foreach (var address in queues)
                {
                    var queue = new QueueDescription(address)
                    {
                        EnableBatchedOperations = true,
                        LockDuration = TimeSpan.FromMinutes(5),
                        MaxDeliveryCount = int.MaxValue,
                        MaxSizeInMB = maxSizeInMb,
                        AutoDeleteOnIdle = autoDeleteOnIdle,
                        EnablePartitioning = transportSettings.EnablePartitioning
                    };

                    try
                    {
                        await client.CreateQueueAsync(queue, cancellationToken).ConfigureAwait(false);
                    }
                    catch (MessagingEntityAlreadyExistsException)
                    {
                    }
                    // TODO: refactor when https://github.com/Azure/azure-service-bus-dotnet/issues/525 is fixed
                    catch (ServiceBusException sbe) when (sbe.Message.Contains("SubCode=40901.")) // An operation is in progress.
                    {
                    }
                }
            }
            finally
            {
                await client.CloseAsync().ConfigureAwait(false);
            }
        }
    }
}