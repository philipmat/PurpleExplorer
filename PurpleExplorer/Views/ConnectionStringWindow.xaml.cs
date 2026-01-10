using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PurpleExplorer.Helpers;
using PurpleExplorer.Models;
using PurpleExplorer.ViewModels;

namespace PurpleExplorer.Views;

public class ConnectionStringWindow : Window
{
    public ConnectionStringWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    public async void SendButtonClick(object? sender, RoutedEventArgs e)
    {
        var dataContext = DataContext as ConnectionStringWindowViewModel;
        if (string.IsNullOrEmpty(dataContext?.ConnectionString))
        {
            await MessageBoxHelper.ShowError("Please enter a service bus connection string.");
        }
        else
        {
            dataContext.Cancel = false;
            Close();
        }
    }

    public async void SendButtonClick2(object? sender, TappedEventArgs e) => SendButtonClick(sender, e);

    public async void SaveConnectionStringButtonClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ConnectionStringWindowViewModel dataContext) return;

        if (dataContext.SavedConnectionStrings.FirstOrDefault(x
                => x.ConnectionString == dataContext.ConnectionString &&
                   x.UseManagedIdentity == dataContext.UseManagedIdentity) != null)
            await MessageBoxHelper.ShowMessage(
                "Duplicate connection string",
                "This connection string is already saved.");
        else
            dataContext.SavedConnectionStrings.Add(
                new ServiceBusConnectionString
                {
                    ConnectionString = dataContext.ConnectionString,
                    UseManagedIdentity = dataContext.UseManagedIdentity
                });
    }

    private void ConnectionStringListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        // `sender` can be Button when we arrive here from the remove button
        if (sender is ListBox { SelectedItem: ServiceBusConnectionString serviceBusConnectionString })
        {
            if (DataContext is not ConnectionStringWindowViewModel dataContext) return;
            dataContext.ConnectionString = serviceBusConnectionString.ConnectionString;
            dataContext.UseManagedIdentity = serviceBusConnectionString.UseManagedIdentity;
        }
    }

    public void DeleteConnectionStringButtonClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not ConnectionStringWindowViewModel dataContext) return;

        ListBox? listBox = this.FindControl<ListBox>("SavedConnectionStringListBox");
        if (listBox?.SelectedItem is not ServiceBusConnectionString connectionString) return;
        dataContext.SavedConnectionStrings.Remove(connectionString);
    }
}
