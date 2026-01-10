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
    private readonly Message? _currentMessage;
    private readonly ILoggingService _loggingService;
    private readonly IQueueHelper _queueHelper;
    private readonly ITopicHelper _topicHelper;

    public MessageDetailsWindowViewModel(
        ITopicHelper? topicHelper = null,
        ILoggingService? loggingService = null,
        IQueueHelper? queueHelper = null)
    {
        _loggingService = loggingService ?? Locator.Current.GetService<ILoggingService>()!;
        _topicHelper = topicHelper ?? Locator.Current.GetService<ITopicHelper>()!;
        _queueHelper = queueHelper ?? Locator.Current.GetService<IQueueHelper>()!;

        DeleteMessageCommand = ReactiveCommand.Create<Window>(DeleteMessage);
        CloseWindowCommand = ReactiveCommand.Create<Window>(CloseWindow);
        ResubmitMessageCommand = ReactiveCommand.CreateFromTask(ResubmitMessage);
        DeadLetterMessageCommand = ReactiveCommand.CreateFromTask(DeadLetterMessage);
    }

    public required ServiceBusSubscription? Subscription { get; init; }
    public required ServiceBusQueue? Queue { get; init; }
    public required ServiceBusConnectionString? ConnectionString { get; init; }

    public ICommand DeleteMessageCommand { get; }
    public ICommand CloseWindowCommand { get; }
    public ICommand ResubmitMessageCommand { get; }
    public ICommand DeadLetterMessageCommand { get; }

    public required Message? CurrentMessage
    {
        get => _currentMessage;
        init => this.RaiseAndSetIfChanged(ref _currentMessage, value);
    }

    private async void DeleteMessage(Window window)
    {
        if (CurrentMessage is null) return;
        if (ConnectionString is null) return;

        _loggingService.Log(
            "DANGER NOTE: Deleting requires receiving all the messages up to the selected message to perform this action and this increases the DeliveryCount of the messages");
        string? deletingPath = null;
        if (Subscription is { Topic: not null })
            deletingPath = CurrentMessage.IsDlq
                ? $"{Subscription.Topic.Name}/{Subscription.Name}/$DeadLetterQueue"
                : $"{Subscription.Topic.Name}/{Subscription.Name}";

        if (Queue != null)
            deletingPath = CurrentMessage.IsDlq
                ? $"{Queue.Name}/$DeadLetterQueue"
                : $"{Queue.Name}";
        if (string.IsNullOrEmpty(deletingPath)) return;

        ButtonResult buttonResult = await MessageBoxHelper.ShowConfirmation(
            $"Deleting message from {deletingPath}",
            $"DANGER!!! READ CAREFULLY \n" +
            $"Deleting requires receiving all the messages up to the selected message to perform this action and this increases the DeliveryCount of the messages. \n" +
            $"There can be consequences to other messages in this subscription, Are you sure? \n \n" +
            $"Are you sure you would like to delete the message with ID: {CurrentMessage.MessageId} AND increase the delivery count of ALL the messages before it?");

        // Because buttonResult can be None or No
        if (buttonResult != ButtonResult.Yes) return;

        _loggingService.Log(
            $"User accepted to receive messages in order to delete message {CurrentMessage.MessageId}. This is going to increases the DeliveryCount of the messages before it.");
        _loggingService.Log($"Deleting message {CurrentMessage.MessageId}... (might take some seconds)");

        if (Subscription != null)
        {
            ServiceBusConnectionString? connectionString = Subscription.Topic?.ServiceBus?.ConnectionString;
            if (connectionString != null)
            {
                await _topicHelper.DeleteMessage(
                    connectionString,
                    Subscription.Topic!.Name,
                    Subscription.Name,
                    CurrentMessage,
                    CurrentMessage.IsDlq);

                if (!CurrentMessage.IsDlq)
                    Subscription.RemoveMessage(CurrentMessage.MessageId);
                else
                    Subscription.RemoveDlqMessage(CurrentMessage.MessageId);
            }
        }

        if (Queue != null)
        {
            ServiceBusConnectionString? connectionString = Queue.ServiceBus?.ConnectionString;

            if (connectionString != null)
            {
                await _queueHelper.DeleteMessage(connectionString, Queue.Name, CurrentMessage, CurrentMessage.IsDlq);

                if (!CurrentMessage.IsDlq)
                    Queue.RemoveMessage(CurrentMessage.MessageId);
                else
                    Queue.RemoveDlqMessage(CurrentMessage.MessageId);
            }
        }

        _loggingService.Log($"Message deleted, MessageId: {CurrentMessage.MessageId}");
        window.Close();
    }

    private static void CloseWindow(Window window)
    {
        // _loggingService.Log("Closing window");
        window.Close();
    }

    private async Task ResubmitMessage()
    {
        if (CurrentMessage is null) return;
        if (ConnectionString is null) return;
        _loggingService.Log($"Resending DLQ message: {CurrentMessage!.MessageId}");

        if (Subscription is { Topic: not null })
            await _topicHelper.ResubmitDlqMessage(
                ConnectionString,
                Subscription.Topic.Name,
                Subscription.Name,
                CurrentMessage!);

        if (Queue != null) await _queueHelper.ResubmitDlqMessage(ConnectionString, Queue.Name, CurrentMessage!);

        _loggingService.Log($"Resent DLQ message: {CurrentMessage.MessageId}");
    }

    private async Task DeadLetterMessage()
    {
        if (CurrentMessage is null) return;
        if (ConnectionString is null) return;

        _loggingService.Log(
            "DANGER NOTE: Sending to dead-letter requires receiving all the messages up to the selected message to perform this action and this increases the DeliveryCount of the messages");
        ButtonResult buttonResult = await MessageBoxHelper.ShowConfirmation(
            "Sending message to dead-letter",
            $"DANGER!!! READ CAREFULLY \n" +
            $"Sending to dead-letter requires receiving all the messages up to the selected message to perform this action and this increases the DeliveryCount of the messages. \n" +
            $"There can be consequences to other messages in this subscription, Are you sure? \n \n" +
            $"Are you sure you would like to send the message {CurrentMessage.MessageId} AND increase the delivery count of ALL the messages before it?");

        // Because buttonResult can be None or No
        if (buttonResult != ButtonResult.Yes) return;

        _loggingService.Log(
            $"User accepted to receive messages in order to send message {CurrentMessage.MessageId} to dead-letter. This is going to increases the DeliveryCount of the messages before it.");
        _loggingService.Log($"Sending message: {CurrentMessage.MessageId} to dead-letter");

        if (Subscription is { Topic: not null })
            await _topicHelper.DeadLetterMessage(
                ConnectionString,
                Subscription.Topic!.Name,
                Subscription.Name,
                CurrentMessage);

        if (Queue != null) await _queueHelper.DeadletterMessage(ConnectionString, Queue.Name, CurrentMessage);

        _loggingService.Log($"Sent message: {CurrentMessage.MessageId} to dead-letter");
    }
}
