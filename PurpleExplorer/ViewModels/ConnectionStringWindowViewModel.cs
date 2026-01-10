using System.Collections.ObjectModel;
using PurpleExplorer.Models;
using ReactiveUI;
using Splat;

namespace PurpleExplorer.ViewModels;

public class ConnectionStringWindowViewModel : DialogViewModelBase
{
    private string? _connectionString;
    private bool _useManagedIdentity;

    public ConnectionStringWindowViewModel(IAppState? appState = null)
    {
        IAppState appState1 = appState ?? Locator.Current.GetService<IAppState>()!;
        SavedConnectionStrings = appState1.SavedConnectionStrings;
    }

    public ObservableCollection<ServiceBusConnectionString> SavedConnectionStrings { get; set; }

    public string? ConnectionString
    {
        get => _connectionString;
        set => this.RaiseAndSetIfChanged(ref _connectionString, value);
    }

    public bool UseManagedIdentity
    {
        get => _useManagedIdentity;
        set => this.RaiseAndSetIfChanged(ref _useManagedIdentity, value);
    }
}
