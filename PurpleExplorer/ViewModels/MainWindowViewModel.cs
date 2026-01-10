using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reactive;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using DynamicData;
using MsBox.Avalonia.Enums;
using PurpleExplorer.Helpers;
using PurpleExplorer.Models;
using PurpleExplorer.Services;
using PurpleExplorer.Views;
using ReactiveUI;
using Splat;
using Message = PurpleExplorer.Models.Message;

namespace PurpleExplorer.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly IAppState _appState;
    private readonly IQueueHelper _queueHelper;
    private readonly ITopicHelper _topicHelper;
    private Message? _currentMessage;
#pragma warning disable CS0169 // Field is never used
    private MessageCollection? _currentMessageCollection;
#pragma warning restore CS0169 // Field is never used
    private ServiceBusQueue? _currentQueue;

    private ServiceBusSubscription? _currentSubscription;
    private ServiceBusTopic? _currentTopic;
    private string? _dlqTabHeader;
    private bool _isBusy;
    private bool _isRegex;
    private string? _messageTabHeader;
    private string? _queueTabHeader;
    private string? _searchText;
    private string? _topicTabHeader;

    public MainWindowViewModel()
    {
        LoggingService = Locator.Current.GetService<ILoggingService>()!;
        _topicHelper = Locator.Current.GetService<ITopicHelper>()!;
        _queueHelper = Locator.Current.GetService<IQueueHelper>()!;
        _appState = Locator.Current.GetService<IAppState>()!;

        Messages = [];
        DlqMessages = [];
        ConnectedServiceBuses = [];
        FilteredConnectedServiceBuses = [];
        ConnectedServiceBuses.CollectionChanged += (_, _) => FilterTree();

        IObservable<bool> canExecuteQueueLevelAction = this.WhenAnyValue(
            x => x.CurrentSubscription,
            x => x.CurrentQueue,
            (subscription, queue) => subscription != null || queue != null
        );

        RefreshCommand = ReactiveCommand.CreateFromTask(Refresh, canExecuteQueueLevelAction);
        AddMessageCommand = ReactiveCommand.CreateFromTask(AddMessage, canExecuteQueueLevelAction);
        PurgeMessagesCommand = ReactiveCommand.CreateFromTask<string>(PurgeMessages, canExecuteQueueLevelAction);
        TransferDeadLetterMessagesCommand =
            ReactiveCommand.CreateFromTask(TransferDeadletterMessages, canExecuteQueueLevelAction);

        RefreshTabHeaders();

        AppVersionText = AppVersion?.ToString(3);
        LoggingService.Log($"PurpleExplorer v{AppVersionText}");

        // Checking for new version asynchronous. no need to await it
#pragma warning disable 4014
        CheckForNewVersion();
#pragma warning restore 4014
    }


    public ObservableCollection<Message> Messages { get; }
    public ObservableCollection<Message> DlqMessages { get; }
    private ObservableCollection<ServiceBusResource> ConnectedServiceBuses { get; }
    public ObservableCollection<ServiceBusResource> FilteredConnectedServiceBuses { get; }

    public ServiceBusConnectionString? ConnectionString { get; private set; }

    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public string? MessagesTabHeader
    {
        get => _messageTabHeader;
        set => this.RaiseAndSetIfChanged(ref _messageTabHeader, value);
    }

    public string? DlqTabHeader
    {
        get => _dlqTabHeader;
        set => this.RaiseAndSetIfChanged(ref _dlqTabHeader, value);
    }

    public string? TopicTabHeader
    {
        get => _topicTabHeader;
        set => this.RaiseAndSetIfChanged(ref _topicTabHeader, value);
    }

    public string? QueueTabHeader
    {
        get => _queueTabHeader;
        set => this.RaiseAndSetIfChanged(ref _queueTabHeader, value);
    }

    public string? SearchText
    {
        get => _searchText;
        set
        {
            this.RaiseAndSetIfChanged(ref _searchText, value);
            FilterTree();
        }
    }

    public bool IsRegex
    {
        get => _isRegex;
        set
        {
            this.RaiseAndSetIfChanged(ref _isRegex, value);
            FilterTree();
        }
    }

    public ServiceBusSubscription? CurrentSubscription
    {
        get => _currentSubscription;
        private set => this.RaiseAndSetIfChanged(ref _currentSubscription, value);
    }

    private ServiceBusTopic? CurrentTopic
    {
        get => _currentTopic;
        set => this.RaiseAndSetIfChanged(ref _currentTopic, value);
    }

    public Message? CurrentMessage
    {
        get => _currentMessage;
        set => this.RaiseAndSetIfChanged(ref _currentMessage, value);
    }

    public ServiceBusQueue? CurrentQueue
    {
        get => _currentQueue;
        private set => this.RaiseAndSetIfChanged(ref _currentQueue, value);
    }

    public double TopicListWidth
    {
        get => _appState.AppSettings.TopicListWidth;
        set
        {
            _appState.AppSettings.TopicListWidth = value;
            this.RaisePropertyChanged();
        }
    }

    private MessageCollection? CurrentMessageCollection
    {
        get
        {
            if (CurrentSubscription != null)
                return CurrentSubscription;
            return CurrentQueue ?? null;
        }
    }

    private Version? AppVersion => Assembly.GetExecutingAssembly().GetName().Version;
    public string? AppVersionText { get; set; }
    public ILoggingService LoggingService { get; }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> AddMessageCommand { get; }
    public ReactiveCommand<string, Unit> PurgeMessagesCommand { get; }
    public ReactiveCommand<Unit, Unit> TransferDeadLetterMessagesCommand { get; }

    private async Task CheckForNewVersion()
    {
        GithubRelease? latestRelease = await AppVersionHelper.GetLatestRelease();
        if (latestRelease == null) return;

        var latestReleaseVersion = new Version(latestRelease.Name);
        if (latestReleaseVersion > AppVersion)
        {
            AppVersionText = $"new v{latestReleaseVersion} is available";
            this.RaisePropertyChanged(nameof(AppVersionText));

            var message =
                $"New version v{latestReleaseVersion} is available. \n Download today at {latestRelease.HtmlUrl}";
            LoggingService.Log(message);
            await MessageBoxHelper.ShowMessage("New version available", message);
        }
        else
        {
            LoggingService.Log($"v{AppVersion} is the latest released version");
        }
    }

    // TODO: catch exceptions inside the method and log to console
    public async Task ConnectionBtnPopupCommand()
    {
        var viewModel = new ConnectionStringWindowViewModel();

        ConnectionStringWindowViewModel? returnedViewModel =
            await ModalWindowHelper.ShowModalWindow<ConnectionStringWindow, ConnectionStringWindowViewModel>(
                viewModel);
        if (returnedViewModel == null) return;

        if (returnedViewModel.Cancel) return;

        if (string.IsNullOrEmpty(returnedViewModel.ConnectionString)) return;

        ConnectionString = new ServiceBusConnectionString
        {
            ConnectionString = returnedViewModel.ConnectionString,
            UseManagedIdentity = returnedViewModel.UseManagedIdentity
        };


        if (ConnectedServiceBuses.Any(x =>
                x.ConnectionString?.ConnectionString == ConnectionString.ConnectionString &&
                x.ConnectionString.UseManagedIdentity == ConnectionString.UseManagedIdentity))
        {
            await MessageBoxHelper.ShowMessage("Duplicate connection", "This connection is already open.");
            return;
        }

        try
        {
            IsBusy = true;
            LoggingService.Log("Connecting...");

            NamespaceProperties namespaceInfo = await _topicHelper.GetNamespaceInfo(ConnectionString);
            IList<ServiceBusTopic> topics = await _topicHelper.GetTopicsAndSubscriptions(ConnectionString);
            IList<ServiceBusQueue> queues = await _queueHelper.GetQueues(ConnectionString);

            var serviceBusResource = new ServiceBusResource
            {
                Name = namespaceInfo.Name,
                CreatedTime = namespaceInfo.CreatedTime,
                ConnectionString = ConnectionString
            };

            serviceBusResource.AddQueues(queues.ToArray());
            serviceBusResource.AddTopics(topics.ToArray());
            ConnectedServiceBuses.Add(serviceBusResource);
            LoggingService.Log("Connected to Service Bus: " + namespaceInfo.Name);
        }
        catch (ArgumentException)
        {
            await MessageBoxHelper.ShowError("The connection string is invalid.");
            LoggingService.Log("Connection failed: The connection string is invalid");
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.GeneralError &&
                                             ex.Message.Contains("unauthorized", StringComparison.OrdinalIgnoreCase))
        {
            await MessageBoxHelper.ShowError("Unable to connect to Service Bus; unauthorized.");
            LoggingService.Log("Connection failed: Unauthorized");
        }
        catch (ServiceBusException ex)
        {
            await MessageBoxHelper.ShowError($"Unable to connect to Service Bus; {ex.Message}");
            LoggingService.Log($"Connection failed: {ex.Message}");
        }
        finally
        {
            IsBusy = false;
            RefreshTabHeaders();
        }
    }

    /**/
    private async Task FetchSubscriptionMessages()
    {
        if (CurrentSubscription == null) return;

        ServiceBusConnectionString? connectionString = CurrentSubscription.Topic?.ServiceBus?.ConnectionString;
        if (connectionString == null) return;
        string topicName = CurrentSubscription.Topic!.Name;
        string subscriptionName = CurrentSubscription.Name;

        try
        {
            ServiceBusSubscription subscription = await _topicHelper.GetSubscription(
                connectionString,
                topicName,
                subscriptionName);
            CurrentSubscription.MessageCount = subscription.MessageCount;
            CurrentSubscription.DlqCount = subscription.DlqCount;
        }
        catch (Exception ex)
        {
            LoggingService.Log($"Error fetching subscription runtime info: {ex.Message}");
        }

        Messages.Clear();
        CurrentSubscription.ClearMessages();
        IList<Message> messages =
            await _topicHelper.GetMessagesBySubscription(
                connectionString,
                topicName,
                subscriptionName);
        if (messages is { Count: > 0 })
        {
            CurrentSubscription?.AddMessages(messages);
            Messages.AddRange(messages);
        }
    }

    private async Task FetchSubscriptionDlqMessages()
    {
        ServiceBusConnectionString? connectionString = CurrentSubscription?.Topic?.ServiceBus?.ConnectionString;
        if (connectionString == null) return;
        string topicName = CurrentSubscription!.Topic!.Name;
        string subscriptionName = CurrentSubscription.Name;

        try
        {
            ServiceBusSubscription subscription = await _topicHelper.GetSubscription(
                connectionString,
                topicName,
                subscriptionName);
            CurrentSubscription.MessageCount = subscription.MessageCount;
            CurrentSubscription.DlqCount = subscription.DlqCount;
        }
        catch (Exception ex)
        {
            LoggingService.Log($"Error fetching subscription runtime info: {ex.Message}");
        }

        DlqMessages.Clear();
        CurrentSubscription?.ClearDlqMessages();
        IList<Message> dlqMessages =
            await _topicHelper.GetDlqMessages(
                connectionString,
                topicName,
                subscriptionName);
        if (dlqMessages is { Count: > 0 })
        {
            CurrentSubscription?.AddDlqMessages(dlqMessages);
            DlqMessages.AddRange(dlqMessages);
        }
    }

    private async Task FetchQueueMessages()
    {
        if (CurrentQueue == null) return;

        ServiceBusConnectionString? connectionString = CurrentQueue.ServiceBus?.ConnectionString;
        if (connectionString == null) return;
        string queuePath = CurrentQueue.Name;

        try
        {
            ServiceBusQueue queue = await _queueHelper.GetQueue(connectionString, queuePath);
            CurrentQueue.MessageCount = queue.MessageCount;
            CurrentQueue.DlqCount = queue.DlqCount;
        }
        catch (Exception ex)
        {
            LoggingService.Log($"Error fetching queue runtime info: {ex.Message}");
        }

        Messages.Clear();
        CurrentQueue.ClearMessages();
        IList<Message> messages = await _queueHelper.GetMessages(connectionString, queuePath);
        CurrentQueue.AddMessages(messages);
        Messages.AddRange(messages);
    }

    private async Task FetchQueueDlqMessages()
    {
        if (CurrentQueue == null) return;

        ServiceBusConnectionString? connectionString = CurrentQueue.ServiceBus?.ConnectionString;
        if (connectionString == null) return;
        string queuePath = CurrentQueue.Name;

        try
        {
            ServiceBusQueue queue = await _queueHelper.GetQueue(connectionString, queuePath);
            CurrentQueue.MessageCount = queue.MessageCount;
            CurrentQueue.DlqCount = queue.DlqCount;
        }
        catch (Exception ex)
        {
            LoggingService.Log($"Error fetching queue runtime info: {ex.Message}");
        }

        DlqMessages.Clear();
        CurrentQueue.ClearDlqMessages();
        IList<Message> messages = await _queueHelper.GetDlqMessages(connectionString, queuePath);
        CurrentQueue.AddDlqMessages(messages);
        DlqMessages.AddRange(messages);
    }

    public void RefreshTabHeaders()
    {
        if (CurrentMessageCollection != null)
        {
            MessagesTabHeader = $"Messages ({CurrentMessageCollection.MessageCount})";
            DlqTabHeader = $"Dead-letter ({CurrentMessageCollection.DlqCount})";
        }
        else
        {
            MessagesTabHeader = "Messages";
            DlqTabHeader = "Dead-letter";
        }

        int topicCount = ConnectedServiceBuses.Sum(x => x.Topics.Count);
        int queueCount = ConnectedServiceBuses.Sum(x => x.Queues.Count);
        if (topicCount > 0 || queueCount > 0)
        {
            TopicTabHeader = $"Topics ({topicCount})";
            QueueTabHeader = $"Queues ({queueCount})";
        }
        else
        {
            TopicTabHeader = "Topics";
            QueueTabHeader = "Queues";
        }
    }

    private async Task RefreshConnectedServiceBuses()
    {
        try
        {
            IsBusy = true;
            foreach (ServiceBusResource serviceBusResource in ConnectedServiceBuses)
            {
                if (serviceBusResource.ConnectionString == null) continue;

                IList<ServiceBusTopic> topicsAndSubscriptions =
                    await _topicHelper.GetTopicsAndSubscriptions(serviceBusResource.ConnectionString);
                IList<ServiceBusQueue> serviceBusQueues =
                    await _queueHelper.GetQueues(serviceBusResource.ConnectionString);

                serviceBusResource.Topics.Clear();
                serviceBusResource.Queues.Clear();
                serviceBusResource.AddTopics(topicsAndSubscriptions.ToArray());
                serviceBusResource.AddQueues(serviceBusQueues.ToArray());
            }

            FilterTree();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AddMessage()
    {
        var viewModal = new AddMessageWindowViewModal();

        AddMessageWindowViewModal? returnedViewModal =
            await ModalWindowHelper.ShowModalWindow<AddMessageWindow, AddMessageWindowViewModal>(viewModal);

        if (returnedViewModal == null) return;

        if (returnedViewModal.Cancel) return;

        string? messageText = returnedViewModal.Message?.Trim();
        if (string.IsNullOrEmpty(messageText)) return;

        try
        {
            IsBusy = true;
            LoggingService.Log("Sending message...");
            if (CurrentTopic != null)
            {
                ServiceBusConnectionString? connectionString = CurrentTopic.ServiceBus?.ConnectionString;
                if (connectionString != null)
                {
                    await _topicHelper.SendMessage(connectionString, CurrentTopic.Name, messageText);
                }
            }

            if (CurrentQueue != null)
            {
                ServiceBusConnectionString? connectionString = CurrentQueue.ServiceBus?.ConnectionString;
                if (connectionString != null)
                {
                    await _queueHelper.SendMessage(connectionString, CurrentQueue.Name, messageText);
                }
            }

            LoggingService.Log("Message sent");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task TransferDeadletterMessages()
    {
        string? dlqPath = null;
        string? transferTo = null;
        if (_currentSubscription != null && _currentTopic != null)
        {
            transferTo = $"{_currentTopic.Name}/{_currentSubscription.Name}";
            dlqPath = $"{transferTo}/$DeadLetterQueue";
        }

        if (_currentQueue != null)
        {
            transferTo = $"{_currentQueue.Name}";
            dlqPath = $"{transferTo}/$DeadLetterQueue";
        }

        ButtonResult buttonResult = await MessageBoxHelper.ShowConfirmation(
            "Transferring messages from DLQ",
            $"Are you sure you would like to transfer ALL the messages on {dlqPath} back to {transferTo}?");

        // Because buttonResult can be None or No
        if (buttonResult != ButtonResult.Yes)
        {
            CurrentMessage = null;
            return;
        }

        try
        {
            IsBusy = true;
            LoggingService.Log($"Transferring ALL messages in {dlqPath}... (might take some time)");
            long transferCount = -1;
            if (CurrentSubscription != null && CurrentTopic != null)
            {
                ServiceBusConnectionString? connectionString = CurrentSubscription.Topic?.ServiceBus?.ConnectionString;

                if (connectionString != null)
                {
                    transferCount = await _topicHelper.TransferDlqMessages(
                        connectionString,
                        CurrentTopic.Name,
                        CurrentSubscription.Name);
                }
            }

            if (CurrentQueue != null)
            {
                ServiceBusConnectionString? connectionString = CurrentQueue.ServiceBus?.ConnectionString;
                if (connectionString != null)
                {
                    transferCount = await _queueHelper.TransferDlqMessages(connectionString, CurrentQueue.Name);
                }
            }

            LoggingService.Log($"Transferred {transferCount} messages in {dlqPath}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task PurgeMessages(string isDlqText)
    {
        var isDlq = Convert.ToBoolean(isDlqText);
        string? purgingPath = null;
        if (CurrentSubscription != null && CurrentTopic != null)
            purgingPath = isDlq
                ? $"{CurrentTopic.Name}/{CurrentSubscription.Name}/$DeadLetterQueue"
                : $"{CurrentTopic.Name}/{CurrentSubscription.Name}";

        if (CurrentQueue != null)
            purgingPath = isDlq
                ? $"{CurrentQueue.Name}/$DeadLetterQueue"
                : $"{CurrentQueue.Name}";

        ButtonResult buttonResult = await MessageBoxHelper.ShowConfirmation(
            $"Purging messages from {purgingPath}",
            $"Are you sure you would like to purge ALL the messages from {purgingPath}?");

        // Because buttonResult can be None or No
        if (buttonResult != ButtonResult.Yes)
        {
            CurrentMessage = null;
            return;
        }

        try
        {
            IsBusy = true;
            LoggingService.Log($"Purging ALL messages in {purgingPath}... (might take some time)");
            long purgedCount = -1;
            if (CurrentSubscription != null && CurrentTopic != null)
            {
                ServiceBusConnectionString? connectionString = CurrentSubscription.Topic?.ServiceBus?.ConnectionString;

                if (connectionString != null)
                {
                    purgedCount = await _topicHelper.PurgeMessages(
                        connectionString,
                        CurrentTopic.Name,
                        CurrentSubscription.Name,
                        isDlq);
                }

                if (!isDlq)
                {
                    CurrentSubscription?.ClearMessages();
                    if (CurrentSubscription != null) CurrentSubscription.MessageCount = 0;
                }
                else
                {
                    CurrentSubscription?.ClearDlqMessages();
                    if (CurrentSubscription != null) CurrentSubscription.DlqCount = 0;
                }
            }

            if (CurrentQueue != null)
            {
                ServiceBusConnectionString? connectionString = CurrentQueue.ServiceBus?.ConnectionString;
                if (connectionString != null)
                {
                    purgedCount = await _queueHelper.PurgeMessages(connectionString, CurrentQueue.Name, isDlq);
                }

                if (!isDlq)
                {
                    CurrentQueue?.ClearMessages();
                    if (CurrentQueue != null) CurrentQueue.MessageCount = 0;
                }
                else
                {
                    CurrentQueue?.ClearDlqMessages();
                    if (CurrentQueue != null) CurrentQueue.DlqCount = 0;
                }
            }

            LoggingService.Log($"Purged {purgedCount} messages in {purgingPath}");

            // Refreshing messages
            await FetchMessages();
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void FilterTree()
    {
        FilteredConnectedServiceBuses.Clear();

        foreach (ServiceBusResource serviceBusResource in ConnectedServiceBuses)
        {
            bool resourceMatches = serviceBusResource.Name != null && Matches(serviceBusResource.Name);
            var filteredResource = new ServiceBusResource
            {
                Name = serviceBusResource.Name,
                CreatedTime = serviceBusResource.CreatedTime,
                ConnectionString = serviceBusResource.ConnectionString
            };

            // Filter Topics and Subscriptions
            foreach (ServiceBusTopic topic in serviceBusResource.Topics)
            {
                bool topicMatches = resourceMatches || Matches(topic.Name);
                ServiceBusTopic filteredTopic = new()
                {
                    Name = topic.Name,
                    ServiceBus = filteredResource
                };

                foreach (ServiceBusSubscription subscription in topic.Subscriptions)
                    if (topicMatches || Matches(subscription.Name))
                        filteredTopic.AddSubscriptions(subscription);

                if ((filteredTopic.Subscriptions.Count > 0) || topicMatches)
                    filteredResource.AddTopics(filteredTopic);
            }

            // Filter Queues
            foreach (ServiceBusQueue queue in serviceBusResource.Queues)
                if (resourceMatches || Matches(queue.Name))
                    filteredResource.AddQueues(queue);

            if ((filteredResource.Topics.Count > 0) ||
                (filteredResource.Queues.Count > 0) ||
                resourceMatches)
                FilteredConnectedServiceBuses.Add(filteredResource);
        }
    }

    private bool Matches(string text)
    {
        if (string.IsNullOrEmpty(SearchText)) return true;

        if (IsRegex)
            try
            {
                return Regex.IsMatch(text, SearchText, RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }

        return text.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    private async Task Refresh()
    {
        try
        {
            IsBusy = true;
            await RefreshConnectedServiceBuses();
            RefreshTabHeaders();
            await FetchMessages();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task FetchMessages()
    {
        try
        {
            IsBusy = true;
            LoggingService.Log("Fetching messages...");

            await Task.WhenAll(
                FetchSubscriptionMessages(),
                FetchSubscriptionDlqMessages(),
                FetchQueueMessages(),
                FetchQueueDlqMessages()
            );

            LoggingService.Log("Fetched messages");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void SetSelectedSubscription(ServiceBusSubscription subscription)
    {
        CurrentSubscription = subscription;
        CurrentTopic = subscription.Topic;
        LoggingService.Log("Subscription selected: " + subscription.Name);
    }

    public void SetSelectedTopic(ServiceBusTopic selectedTopic)
    {
        CurrentTopic = selectedTopic;
        LoggingService.Log("Topic selected: " + selectedTopic.Name);
    }

    public void SetSelectedMessage(Message message)
    {
        CurrentMessage = message;
        LoggingService.Log("Message selected: " + message.MessageId);
    }

    public void SetSelectedQueue(ServiceBusQueue selectedQueue)
    {
        CurrentQueue = selectedQueue;
        LoggingService.Log("Queue selected: " + selectedQueue.Name);
    }

    public async Task ShowSettings()
        => await ModalWindowHelper.ShowModalWindow<AppSettingsWindow>((AppState)_appState);

    public void ClearAllSelections()
    {
        CurrentSubscription = null;
        CurrentTopic = null;
        CurrentQueue = null;
        CurrentMessage = null;
        Messages.Clear();
        DlqMessages.Clear();
        RefreshTabHeaders();
    }
}
