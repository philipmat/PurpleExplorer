using System;
using System.Collections.ObjectModel;
using System.Reactive.Linq;
using System.Threading.Tasks;
using PurpleExplorer.Helpers;
using PurpleExplorer.Models;
using ReactiveUI;
using Splat;

namespace PurpleExplorer.ViewModels;

public class AddMessageWindowViewModal : DialogViewModelBase
{
    private readonly IAppState _appState;
    private readonly ISuspensionDriver _suspensionDriver;

    private string? _message;
    private string? _title;

    public AddMessageWindowViewModal(IAppState? appState = null, ISuspensionDriver? suspensionDriver = null)
    {
        _appState = appState ?? Locator.Current.GetService<IAppState>()!;
        _suspensionDriver = suspensionDriver ?? Locator.Current.GetService<ISuspensionDriver>()!;
        SavedMessages = _appState.SavedMessages;
        ApplicationProperties = new ObservableCollection<ApplicationProperty>();
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

    public ObservableCollection<ApplicationProperty> ApplicationProperties { get; set; }

    public async Task SaveMessage()
    {
        if (string.IsNullOrWhiteSpace(Message) || string.IsNullOrWhiteSpace(Title)) return;

        SavedMessage newMessage = new SavedMessage
        {
            Message = Message,
            Title = Title,
            ApplicationProperties = [..ApplicationProperties]
        };
        SavedMessages.Add(newMessage);

        try
        {
            await _suspensionDriver.SaveState(_appState);
        }
        catch (Exception ex)
        {
            SavedMessages.Remove(newMessage);
            await MessageBoxHelper.ShowError($"Could not save the message: {ex.Message}");
        }
    }
}
