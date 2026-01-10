using System;
using System.Reactive;
using System.Reactive.Linq;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using DynamicData;
using PurpleExplorer.Helpers;
using PurpleExplorer.Models;
using PurpleExplorer.Views;
using ReactiveUI;
using System.Threading.Tasks;
using MsBox.Avalonia.Enums;
using Microsoft.Azure.ServiceBus;
using PurpleExplorer.Services;
using Splat;
using Message = PurpleExplorer.Models.Message;

namespace PurpleExplorer.ViewModels;

public class MainWindowViewModel : ViewModelBase
{
    private readonly ITopicHelper _topicHelper;
    private readonly IQueueHelper _queueHelper;
    private readonly ILoggingService _loggingService;
    private string _messageTabHeader;
    private string _dlqTabHeader;
    private string _topicTabHeader;
    private string _queueTabHeader;
    private string _searchText;
    private bool _isRegex;

    private ServiceBusSubscription _currentSubscription;
    private ServiceBusTopic _currentTopic;
    private ServiceBusQueue _currentQueue;
    private Message _currentMessage;
    private MessageCollection _currentMessageCollection;
    private IAppState _appState;
    private bool _isBusy;

    public bool IsBusy
    {
        get => _isBusy;
        set => this.RaiseAndSetIfChanged(ref _isBusy, value);
    }

    public ObservableCollection<Message> Messages { get; }
    public ObservableCollection<Message> DlqMessages { get; }
    public ObservableCollection<ServiceBusResource> ConnectedServiceBuses { get; }
    public ObservableCollection<ServiceBusResource> FilteredConnectedServiceBuses { get; }

    public ServiceBusConnectionString ConnectionString { get; set; }

    public string MessagesTabHeader
    {
        get => _messageTabHeader;
        set => this.RaiseAndSetIfChanged(ref _messageTabHeader, value);
    }

    public string DlqTabHeader
    {
        get => _dlqTabHeader;
        set => this.RaiseAndSetIfChanged(ref _dlqTabHeader, value);
    }

    public string TopicTabHeader
    {
        get => _topicTabHeader;
        set => this.RaiseAndSetIfChanged(ref _topicTabHeader, value);
    }
        
    public string QueueTabHeader
    {
        get => _queueTabHeader;
        set => this.RaiseAndSetIfChanged(ref _queueTabHeader, value);
    }

    public string SearchText
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

    public ServiceBusSubscription CurrentSubscription
    {
        get => _currentSubscription;
        set => this.RaiseAndSetIfChanged(ref _currentSubscription, value);
    }

    public ServiceBusTopic CurrentTopic
    {
        get => _currentTopic;
        set => this.RaiseAndSetIfChanged(ref _currentTopic, value);
    }

    public Message CurrentMessage
    {
        get => _currentMessage;
        set => this.RaiseAndSetIfChanged(ref _currentMessage, value);
    }

    public ServiceBusQueue CurrentQueue
    {
        get => _currentQueue;
        set => this.RaiseAndSetIfChanged(ref _currentQueue, value);
    }

    public double TopicListWidth
    {
        get => _appState.AppSettings.TopicListWidth;
        set
        {
            _appState.AppSettings.TopicListWidth = value;
            this.RaisePropertyChanged(nameof(TopicListWidth));
        }
    }
        
    public MessageCollection CurrentMessageCollection
    {
        get
        {
            if (CurrentSubscription != null)
                return CurrentSubscription;
            if (CurrentQueue != null)
                return CurrentQueue;
            return null;
        }
    }

    public ILoggingService LoggingService => _loggingService;
    public Version AppVersion => Assembly.GetExecutingAssembly().GetName().Version;
    public string AppVersionText { get; set; }

    public ReactiveCommand<Unit, Unit> RefreshCommand { get; }
    public ReactiveCommand<Unit, Unit> AddMessageCommand { get; }
    public ReactiveCommand<string, Unit> PurgeMessagesCommand { get; }
    public ReactiveCommand<Unit, Unit> TransferDeadletterMessagesCommand { get; }

    public MainWindowViewModel()
    {
        _loggingService = Locator.Current.GetService<ILoggingService>();
        _topicHelper = Locator.Current.GetService<ITopicHelper>();
        _queueHelper = Locator.Current.GetService<IQueueHelper>();
        _appState = Locator.Current.GetService<IAppState>();

        Messages = new ObservableCollection<Message>();
        DlqMessages = new ObservableCollection<Message>();
        ConnectedServiceBuses = new ObservableCollection<ServiceBusResource>();
        FilteredConnectedServiceBuses = new ObservableCollection<ServiceBusResource>();
        ConnectedServiceBuses.CollectionChanged += (sender, args) => FilterTree();

        var canExecuteQueueLevelAction = this.WhenAnyValue(
            x => x.CurrentSubscription,
            x => x.CurrentQueue,
            (subscription, queue) => subscription != null || queue != null
        );

        RefreshCommand = ReactiveCommand.CreateFromTask(Refresh, canExecuteQueueLevelAction);
        AddMessageCommand = ReactiveCommand.CreateFromTask(AddMessage, canExecuteQueueLevelAction);
        PurgeMessagesCommand = ReactiveCommand.CreateFromTask<string>(PurgeMessages, canExecuteQueueLevelAction);
        TransferDeadletterMessagesCommand =
            ReactiveCommand.CreateFromTask(TransferDeadletterMessages, canExecuteQueueLevelAction);

        RefreshTabHeaders();

        AppVersionText = AppVersion.ToString(3);
        LoggingService.Log($"PurpleExplorer v{AppVersionText}");

        // Checking for new version asynchronous. no need to await on it
#pragma warning disable 4014
        CheckForNewVersion();
#pragma warning restore 4014
    }

    private async Task CheckForNewVersion()
    {
        var latestRelease = await AppVersionHelper.GetLatestRelease();
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

    public async void ConnectionBtnPopupCommand()
    {
        var viewModel = new ConnectionStringWindowViewModel();

        var returnedViewModel =
            await ModalWindowHelper.ShowModalWindow<ConnectionStringWindow, ConnectionStringWindowViewModel>(
                viewModel);

        if (returnedViewModel.Cancel)
        {
            return;
        }

        ConnectionString = new ServiceBusConnectionString
        {
            ConnectionString = returnedViewModel.ConnectionString,
            UseManagedIdentity = returnedViewModel.UseManagedIdentity
        };

        if (string.IsNullOrEmpty(ConnectionString.ConnectionString))
        {
            return;
        }

        if (ConnectedServiceBuses.Any(x =>
                x.ConnectionString.ConnectionString == ConnectionString.ConnectionString &&
                x.ConnectionString.UseManagedIdentity == ConnectionString.UseManagedIdentity))
        {
            await MessageBoxHelper.ShowMessage("Duplicate connection", "This connection is already open.");
            return;
        }

        try
        {
            IsBusy = true;
            LoggingService.Log("Connecting...");

            var namespaceInfo = await _topicHelper.GetNamespaceInfo(ConnectionString);
            var topics = await _topicHelper.GetTopicsAndSubscriptions(ConnectionString);
            var queues = await _queueHelper.GetQueues(ConnectionString);

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
        catch (UnauthorizedException)
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

    public async Task FetchSubscriptionMessages()
    {
        if (CurrentSubscription == null)
        {
            return;
        }

        var connectionString = CurrentSubscription.Topic.ServiceBus.ConnectionString;
        var topicName = CurrentSubscription.Topic.Name;
        var subscriptionName = CurrentSubscription.Name;

        try
        {
            var subscription = await _topicHelper.GetSubscription(connectionString, topicName, subscriptionName);
            CurrentSubscription.MessageCount = subscription.MessageCount;
            CurrentSubscription.DlqCount = subscription.DlqCount;
        }
        catch (Exception ex)
        {
            LoggingService.Log($"Error fetching subscription runtime info: {ex.Message}");
        }

        Messages.Clear();
        CurrentSubscription.ClearMessages();
        var messages =
            await _topicHelper.GetMessagesBySubscription(connectionString,
                topicName,
                subscriptionName);
        if (messages is { Count: > 0 })
        {
            CurrentSubscription?.AddMessages(messages);
            Messages.AddRange(messages);
        }
    }

    public async Task FetchSubscriptionDlqMessages()
    {
        if (CurrentSubscription == null)
        {
            return;
        }

        var connectionString = CurrentSubscription.Topic.ServiceBus.ConnectionString;
        var topicName = CurrentSubscription.Topic.Name;
        var subscriptionName = CurrentSubscription.Name;

        try
        {
            var subscription = await _topicHelper.GetSubscription(connectionString, topicName, subscriptionName);
            CurrentSubscription.MessageCount = subscription.MessageCount;
            CurrentSubscription.DlqCount = subscription.DlqCount;
        }
        catch (Exception ex)
        {
            LoggingService.Log($"Error fetching subscription runtime info: {ex.Message}");
        }

        DlqMessages.Clear();
        CurrentSubscription?.ClearDlqMessages();
        var dlqMessages =
            await _topicHelper.GetDlqMessages(connectionString,
                topicName, subscriptionName);
        if (dlqMessages is { Count: > 0 })
        {
            CurrentSubscription?.AddDlqMessages(dlqMessages);
            DlqMessages.AddRange(dlqMessages);
        }
    }
        
    public async Task FetchQueueMessages()
    {
        if (CurrentQueue == null)
        {
            return;
        }

        var connectionString = CurrentQueue.ServiceBus.ConnectionString;
        var queuePath = CurrentQueue.Name;

        try
        {
            var queue = await _queueHelper.GetQueue(connectionString, queuePath);
            CurrentQueue.MessageCount = queue.MessageCount;
            CurrentQueue.DlqCount = queue.DlqCount;
        }
        catch (Exception ex)
        {
            LoggingService.Log($"Error fetching queue runtime info: {ex.Message}");
        }

        Messages.Clear();
        CurrentQueue.ClearMessages();
        var messages = await _queueHelper.GetMessages(connectionString, queuePath);
        CurrentQueue.AddMessages(messages);
        Messages.AddRange(messages);
    }

    public async Task FetchQueueDlqMessages()
    {
        if (CurrentQueue == null)
        {
            return;
        }

        var connectionString = CurrentQueue.ServiceBus.ConnectionString;
        var queuePath = CurrentQueue.Name;

        try
        {
            var queue = await _queueHelper.GetQueue(connectionString, queuePath);
            CurrentQueue.MessageCount = queue.MessageCount;
            CurrentQueue.DlqCount = queue.DlqCount;
        }
        catch (Exception ex)
        {
            LoggingService.Log($"Error fetching queue runtime info: {ex.Message}");
        }

        DlqMessages.Clear();
        CurrentQueue.ClearDlqMessages();
        var messages = await _queueHelper.GetDlqMessages(connectionString, queuePath);
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

        var topicCount = ConnectedServiceBuses.Sum(x => x.Topics.Count);
        var queueCount = ConnectedServiceBuses.Sum(x => x.Queues.Count);
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

    public async Task RefreshConnectedServiceBuses()
    {
        try
        {
            IsBusy = true;
            foreach (var serviceBusResource in ConnectedServiceBuses)
            {
                var topicsAndSubscriptions =
                    await _topicHelper.GetTopicsAndSubscriptions(serviceBusResource.ConnectionString);
                var serviceBusQueues = await _queueHelper.GetQueues(serviceBusResource.ConnectionString);

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
        
    public async Task AddMessage()
    {
        var viewModal = new AddMessageWindowViewModal();

        var returnedViewModal =
            await ModalWindowHelper.ShowModalWindow<AddMessageWindow, AddMessageWindowViewModal>(viewModal);

        if (returnedViewModal.Cancel)
        {
            return;
        }

        var messageText = returnedViewModal.Message.Trim();
        if (string.IsNullOrEmpty(messageText))
        {
            return;
        }

        try
        {
            IsBusy = true;
            LoggingService.Log("Sending message...");
            if (CurrentTopic != null)
            {
                var connectionString = CurrentTopic.ServiceBus.ConnectionString;
                await _topicHelper.SendMessage(connectionString, CurrentTopic.Name, messageText);
            }

            if (CurrentQueue != null)
            {
                var connectionString = CurrentQueue.ServiceBus.ConnectionString;
                await _queueHelper.SendMessage(connectionString, CurrentQueue.Name, messageText);
            }

            LoggingService.Log("Message sent");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task TransferDeadletterMessages()
    {
        string dlqPath = null;
        string transferTo = null; 
        if (_currentSubscription != null)
        {
            transferTo = $"{_currentTopic.Name}/{_currentSubscription.Name}";
            dlqPath = $"{transferTo}/$DeadLetterQueue";
        }

        if (_currentQueue != null)
        {
            transferTo = $"{_currentQueue.Name}";
            dlqPath = $"{transferTo}/$DeadLetterQueue";
        }
            
        var buttonResult = await MessageBoxHelper.ShowConfirmation(
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
            if (CurrentSubscription != null)
            {
                var connectionString = CurrentSubscription.Topic.ServiceBus.ConnectionString;
                transferCount = await _topicHelper.TransferDlqMessages(connectionString, _currentTopic.Name,
                    _currentSubscription.Name);
            }

            if (CurrentQueue != null)
            {
                var connectionString = CurrentQueue.ServiceBus.ConnectionString;
                transferCount = await _queueHelper.TransferDlqMessages(connectionString, _currentQueue.Name);
            }

            LoggingService.Log($"Transferred {transferCount} messages in {dlqPath}");
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task PurgeMessages(string isDlqText)
    {
        var isDlq = Convert.ToBoolean(isDlqText);
        string purgingPath = null;
        if (_currentSubscription != null)
        {
            purgingPath = isDlq
                ? $"{_currentTopic.Name}/{_currentSubscription.Name}/$DeadLetterQueue"
                : $"{_currentTopic.Name}/{_currentSubscription.Name}";
        }

        if (_currentQueue != null)
        {
            purgingPath = isDlq
                ? $"{_currentQueue.Name}/$DeadLetterQueue"
                : $"{_currentQueue.Name}";
        }

        var buttonResult = await MessageBoxHelper.ShowConfirmation(
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
            if (CurrentSubscription != null)
            {
                var connectionString = CurrentSubscription.Topic.ServiceBus.ConnectionString;
                purgedCount = await _topicHelper.PurgeMessages(connectionString, _currentTopic.Name,
                    _currentSubscription.Name, isDlq);

                if (!isDlq)
                {
                    CurrentSubscription?.ClearMessages();
                    CurrentSubscription.MessageCount = 0;
                }
                else
                {
                    CurrentSubscription?.ClearDlqMessages();
                    CurrentSubscription.DlqCount = 0;
                }
            }

            if (CurrentQueue != null)
            {
                var connectionString = CurrentQueue.ServiceBus.ConnectionString;
                purgedCount = await _queueHelper.PurgeMessages(connectionString, _currentQueue.Name, isDlq);

                if (!isDlq)
                {
                    CurrentQueue?.ClearMessages();
                    CurrentQueue.MessageCount = 0;
                }
                else
                {
                    CurrentQueue?.ClearDlqMessages();
                    CurrentQueue.DlqCount = 0;
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

        foreach (var serviceBusResource in ConnectedServiceBuses)
        {
            var resourceMatches = Matches(serviceBusResource.Name);
            var filteredResource = new ServiceBusResource
            {
                Name = serviceBusResource.Name,
                CreatedTime = serviceBusResource.CreatedTime,
                ConnectionString = serviceBusResource.ConnectionString
            };

            // Filter Topics and Subscriptions
            foreach (var topic in serviceBusResource.Topics)
            {
                var topicMatches = resourceMatches || Matches(topic.Name);
                var filteredTopic = new ServiceBusTopic
                {
                    Name = topic.Name,
                    ServiceBus = filteredResource
                };

                if (topic.Subscriptions != null)
                {
                    foreach (var subscription in topic.Subscriptions)
                    {
                        if (topicMatches || Matches(subscription.Name))
                        {
                            filteredTopic.AddSubscriptions(subscription);
                        }
                    }
                }

                if (filteredTopic.Subscriptions != null && filteredTopic.Subscriptions.Count > 0 || topicMatches)
                {
                    filteredResource.AddTopics(filteredTopic);
                }
            }

            // Filter Queues
            foreach (var queue in serviceBusResource.Queues)
            {
                if (resourceMatches || Matches(queue.Name))
                {
                    filteredResource.AddQueues(queue);
                }
            }

            if (filteredResource.Topics != null && filteredResource.Topics.Count > 0 ||
                filteredResource.Queues != null && filteredResource.Queues.Count > 0 ||
                resourceMatches)
            {
                FilteredConnectedServiceBuses.Add(filteredResource);
            }
        }
    }

    private bool Matches(string text)
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            return true;
        }

        if (IsRegex)
        {
            try
            {
                return System.Text.RegularExpressions.Regex.IsMatch(text, SearchText, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        return text.Contains(SearchText, StringComparison.OrdinalIgnoreCase);
    }

    public async Task Refresh()
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
    {
        await ModalWindowHelper.ShowModalWindow<AppSettingsWindow>(_appState as AppState);
    }
        
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