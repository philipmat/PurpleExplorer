using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PurpleExplorer.Helpers;
using PurpleExplorer.Models;
using PurpleExplorer.ViewModels;

namespace PurpleExplorer.Views;

public class AddMessageWindow : Window
{
    public AddMessageWindow()
    {
        InitializeComponent();
    }

    // TODO: catch exceptions inside the method and log to console
    public async void BtnAddClick(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not AddMessageWindowViewModal dataContext) return;
        if (string.IsNullOrEmpty(dataContext.Message))
        {
            await MessageBoxHelper.ShowError("Please enter a message to be sent");
        }
        else
        {
            dataContext.Cancel = false;
            Close();
        }
    }

    public void BtnDeleteMessage(object sender, RoutedEventArgs e)
    {
        if (DataContext is not AddMessageWindowViewModal dataContext) return;
        var dataGrid = this.FindControl<DataGrid>("dgSavedMessages");
        if (dataGrid?.SelectedItem == null) return;
        dataContext.SavedMessages.Remove((SavedMessage)dataGrid.SelectedItem);
    }

    public void MessageSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (DataContext is not AddMessageWindowViewModal dataContext) return;
        if (sender is not DataGrid dataGrid) return;

        if (dataGrid.SelectedItem is not SavedMessage selectedMessage) return;

        dataContext.Message = selectedMessage.Message;
        dataContext.Title = selectedMessage.Title;
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
