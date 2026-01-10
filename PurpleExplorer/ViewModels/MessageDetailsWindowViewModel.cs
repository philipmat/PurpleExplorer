using System.Reactive;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia.Controls;
using MsBox.Avalonia.Enums;
using PurpleExplorer.Helpers;
using PurpleExplorer.Models;
using PurpleExplorer.Services;
using ReactiveUI;
using Splat;

namespace PurpleExplorer.ViewModels;

public class MessageDetailsWindowViewModel : ViewModelBase
{
    private readonly ITopicHelper _topicHelper;
    private readonly IQueueHelper _queueHelper;
    private readonly ILoggingService _loggingService;

    private Message _currentMessage;

    public Message CurrentMessage
    {
        get => _currentMessage;
        set => this.RaiseAndSetIfChanged(ref _currentMessage, value);
    }

    public ServiceBusSubscription Subscription { get; set; }
    public ServiceBusQueue Queue { get; set; }
    public ServiceBusConnectionString ConnectionString { get; set; }

    public ICommand DeleteMessageCommand { get; }
    public ICommand CloseWindowCommand { get; }
    public ICommand ResubmitMessageCommand { get; }
    public ICommand DeadletterMessageCommand { get; }

    public MessageDetailsWindowViewModel(ITopicHelper topicHelper = null,
        ILoggingService loggingService = null,
        IQueueHelper queueHelper = null)
    {
        _loggingService = loggingService ?? Locator.Current.GetService<ILoggingService>();
        _topicHelper = topicHelper ?? Locator.Current.GetService<ITopicHelper>();
        _queueHelper = queueHelper ?? Locator.Current.GetService<IQueueHelper>();

        DeleteMessageCommand = ReactiveCommand.Create<Window>(DeleteMessage);
        CloseWindowCommand = ReactiveCommand.Create<Window>(CloseWindow);
        ResubmitMessageCommand = ReactiveCommand.CreateFromTask(ResubmitMessage);
        DeadletterMessageCommand = ReactiveCommand.CreateFromTask(DeadletterMessage);
    }

    public async void DeleteMessage(Window window)
    {
        _loggingService.Log(
            "DANGER NOTE: Deleting requires receiving all the messages up to the selected message to perform this action and this increases the DeliveryCount of the messages");
        string deletingPath = null;
        if (Subscription != null)
        {
            deletingPath = CurrentMessage.IsDlq
                ? $"{Subscription.Topic.Name}/{Subscription.Name}/$DeadLetterQueue"
                : $"{Subscription.Topic.Name}/{Subscription.Name}";
        }

        if (Queue != null)
        {
            deletingPath = CurrentMessage.IsDlq
                ? $"{Queue.Name}/$DeadLetterQueue"
                : $"{Queue.Name}";
        }

        var buttonResult = await MessageBoxHelper.ShowConfirmation(
            $"Deleting message from {deletingPath}",
            $"DANGER!!! READ CAREFULLY \n" +
            $"Deleting requires receiving all the messages up to the selected message to perform this action and this increases the DeliveryCount of the messages. \n" +
            $"There can be consequences to other messages in this subscription, Are you sure? \n \n" +
            $"Are you sure you would like to delete the message with ID: {CurrentMessage.MessageId} AND increase the delivery count of ALL the messages before it?");

        // Because buttonResult can be None or No
        if (buttonResult != ButtonResult.Yes)
        {
            return;
        }

        _loggingService.Log(
            $"User accepted to receive messages in order to delete message {CurrentMessage.MessageId}. This is going to increases the DeliveryCount of the messages before it.");
        _loggingService.Log($"Deleting message {CurrentMessage.MessageId}... (might take some seconds)");

        if (Subscription != null)
        {
            var connectionString = Subscription.Topic.ServiceBus.ConnectionString;
            await _topicHelper.DeleteMessage(connectionString, Subscription.Topic.Name, Subscription.Name,
                CurrentMessage, CurrentMessage.IsDlq);

            if (!CurrentMessage.IsDlq)
                Subscription.RemoveMessage(CurrentMessage.MessageId);
            else
                Subscription.RemoveDlqMessage(CurrentMessage.MessageId);
        }

        if (Queue != null)
        {
            var connectionString = Queue.ServiceBus.ConnectionString;
            await _queueHelper.DeleteMessage(connectionString, Queue.Name, CurrentMessage, CurrentMessage.IsDlq);

            if (!CurrentMessage.IsDlq)
                Queue.RemoveMessage(CurrentMessage.MessageId);
            else
                Queue.RemoveDlqMessage(CurrentMessage.MessageId);
        }

        _loggingService.Log($"Message deleted, MessageId: {CurrentMessage.MessageId}");
        window.Close();
    }

    public void CloseWindow(Window window)
    {
        // _loggingService.Log("Closing window");
        window.Close();
    }

    public async Task ResubmitMessage()
    {
        _loggingService.Log($"Resending DLQ message: {CurrentMessage.MessageId}");

        if (Subscription != null)
        {
            await _topicHelper.ResubmitDlqMessage(ConnectionString, Subscription.Topic.Name, Subscription.Name,
                CurrentMessage);
        }

        if (Queue != null)
        {
            await _queueHelper.ResubmitDlqMessage(ConnectionString, Queue.Name, CurrentMessage);
        }

        _loggingService.Log($"Resent DLQ message: {CurrentMessage.MessageId}");
    }

    public async Task DeadletterMessage()
    {
        _loggingService.Log(
            "DANGER NOTE: Sending to dead-letter requires receiving all the messages up to the selected message to perform this action and this increases the DeliveryCount of the messages");
        var buttonResult = await MessageBoxHelper.ShowConfirmation(
            $"Sending message to dead-letter",
            $"DANGER!!! READ CAREFULLY \n" +
            $"Sending to dead-letter requires receiving all the messages up to the selected message to perform this action and this increases the DeliveryCount of the messages. \n" +
            $"There can be consequences to other messages in this subscription, Are you sure? \n \n" +
            $"Are you sure you would like to send the message {CurrentMessage.MessageId} AND increase the delivery count of ALL the messages before it?");

        // Because buttonResult can be None or No
        if (buttonResult != ButtonResult.Yes)
        {
            return;
        }

        _loggingService.Log(
            $"User accepted to receive messages in order to send message {CurrentMessage.MessageId} to dead-letter. This is going to increases the DeliveryCount of the messages before it.");
        _loggingService.Log($"Sending message: {CurrentMessage.MessageId} to dead-letter");

        if (Subscription != null)
        {
            await _topicHelper.DeadletterMessage(ConnectionString, Subscription.Topic.Name, Subscription.Name,
                CurrentMessage);
        }

        if (Queue != null)
        {
            await _queueHelper.DeadletterMessage(ConnectionString, Queue.Name, CurrentMessage);
        }

        _loggingService.Log($"Sent message: {CurrentMessage.MessageId} to dead-letter");
    }
}