using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DynamicData;
using ReactiveUI;

namespace PurpleExplorer.Models;

/// <summary>
///     Represents either a subscription or a queue
/// </summary>
public abstract class MessageCollection : ReactiveObject
{
    private long _dlqCount;

    // These are needed to be set before fetching messages, in the second constructor
    private long _messageCount;

    protected MessageCollection()
    {
        Messages = new ObservableCollection<Message>();
        DlqMessages = new ObservableCollection<Message>();
    }

    protected MessageCollection(long messageCount, long dlqCount) : this()
    {
        _messageCount = messageCount;
        _dlqCount = dlqCount;
    }

    private ObservableCollection<Message> Messages { get; }
    private ObservableCollection<Message> DlqMessages { get; }

    public long MessageCount
    {
        get => _messageCount;
        set => this.RaiseAndSetIfChanged(ref _messageCount, value);
    }

    public long DlqCount
    {
        get => _dlqCount;
        set => this.RaiseAndSetIfChanged(ref _dlqCount, value);
    }

    public void AddMessages(IEnumerable<Message> messages) => Messages.AddRange(messages);

    public void RemoveMessage(long sequenceNumber)
    {
        Message? message = Messages.FirstOrDefault(msg => msg.SequenceNumber == sequenceNumber);
        if (message != null && Messages.Remove(message) && MessageCount > 0)
            MessageCount--;
    }

    public void ClearMessages() => Messages.Clear();

    public void AddDlqMessages(IEnumerable<Message> dlqMessages) => DlqMessages.AddRange(dlqMessages);

    public void RemoveDlqMessage(long sequenceNumber)
    {
        Message? message = DlqMessages.FirstOrDefault(msg => msg.SequenceNumber == sequenceNumber);
        if (message != null && DlqMessages.Remove(message) && DlqCount > 0)
            DlqCount--;
    }

    public void ClearDlqMessages() => DlqMessages.Clear();
}
