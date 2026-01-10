using System.Collections.ObjectModel;
using PurpleExplorer.Models;
using ReactiveUI;
using Splat;

namespace PurpleExplorer.ViewModels;

public class AddMessageWindowViewModal : DialogViewModelBase
{
    private string? _message;
    private string? _title;

    public AddMessageWindowViewModal(IAppState? appState = null)
    {
        IAppState appState1 = appState ?? Locator.Current.GetService<IAppState>()!;
        SavedMessages = appState1.SavedMessages;
    }

    public string? Message
    {
        get => _message;
        set => this.RaiseAndSetIfChanged(ref _message, value);
    }

    public string? Title
    {
        get => _title;
        set => this.RaiseAndSetIfChanged(ref _title, value);
    }

    public ObservableCollection<SavedMessage> SavedMessages { get; set; }

    public void SaveMessage()
    {
        // TODO: log to console
        if (string.IsNullOrWhiteSpace(Message) || string.IsNullOrWhiteSpace(Title)) return;

        SavedMessage newMessage = new SavedMessage
        {
            Message = Message,
            Title = Title
        };
        SavedMessages.Add(newMessage);
    }
}
