using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using DynamicData;
using ReactiveUI;

namespace PurpleExplorer.Models;

/// <summary>
/// Represents either a subscription or a queue
/// </summary>
public abstract class MessageCollection : ReactiveObject
{
    // These are needed to be set before fetching messages, in the second constructor
    private long _messageCount;
    private long _dlqCount;
       
    public ObservableCollection<Message> Messages { get; }
    public ObservableCollection<Message> DlqMessages { get; }
        
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

    public void AddMessages(IEnumerable<Message> messages)
    {
        Messages.AddRange(messages);
    }

    public void RemoveMessage(string messageId)
    {
        Messages.Remove(Messages.Single(msg => msg.MessageId.Equals(messageId)));
        MessageCount--;
    }
        
    public void ClearMessages()
    {
        Messages.Clear();
    }
        
    public void AddDlqMessages(IEnumerable<Message> dlqMessages)
    {
        DlqMessages.AddRange(dlqMessages);
    }
        
    public void RemoveDlqMessage(string messageId)
    {
        DlqMessages.Remove(DlqMessages.Single(msg => msg.MessageId.Equals(messageId)));
        DlqCount--;
    }
        
    public void ClearDlqMessages()
    {
        DlqMessages.Clear();
    }
}