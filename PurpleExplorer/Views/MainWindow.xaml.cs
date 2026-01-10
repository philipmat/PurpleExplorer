using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using PurpleExplorer.Helpers;
using PurpleExplorer.Models;
using PurpleExplorer.ViewModels;

namespace PurpleExplorer.Views;

public class MainWindow : Window
{
    private bool _isClearingSelection;

    public MainWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        Opened += MainWindow_Opened;
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        var mainWindowViewModel = DataContext as MainWindowViewModel;
        mainWindowViewModel?.ConnectionBtnPopupCommand();
    }

    // TODO: catch exceptions inside the method and log to console
    private async void MessagesGrid_DoubleTapped(object? sender, TappedEventArgs e)
    {
        if (sender is not DataGrid grid) return;

        if (DataContext is not MainWindowViewModel mainWindowViewModel) return;

        if (grid.SelectedItem == null) return;

        MessageDetailsWindowViewModel viewModal = new()
        {
            CurrentMessage = grid.SelectedItem as Message,
            ConnectionString = mainWindowViewModel.ConnectionString,
            Subscription = mainWindowViewModel.CurrentSubscription,
            Queue = mainWindowViewModel.CurrentQueue
        };

        await ModalWindowHelper.ShowModalWindow<MessageDetailsWindow, MessageDetailsWindowViewModel>(viewModal);
    }

    private void MessagesGrid_Tapped(object sender, TappedEventArgs e)
    {
        if (sender is not DataGrid grid) return;
        if (DataContext is not MainWindowViewModel mainWindowViewModel) return;

        if (grid.SelectedItem is Message message) mainWindowViewModel.SetSelectedMessage(message);
    }

    // TODO: catch exceptions inside the method and log to console
    private async void TreeView_SelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (_isClearingSelection) return;

        if (DataContext is not MainWindowViewModel mainWindowViewModel) return;
        if (sender is not TreeView treeView) return;

        _isClearingSelection = true;
        ClearOtherSelections(treeView);
        mainWindowViewModel.ClearAllSelections();
        _isClearingSelection = false;

        object? selectedItem = treeView.SelectedItems.Count > 0 ? treeView.SelectedItems[0] : null;
        if (selectedItem is ServiceBusSubscription selectedSubscription)
        {
            mainWindowViewModel.SetSelectedSubscription(selectedSubscription);
            await mainWindowViewModel.FetchMessages();
            mainWindowViewModel.RefreshTabHeaders();
        }

        if (selectedItem is ServiceBusTopic selectedTopic) mainWindowViewModel.SetSelectedTopic(selectedTopic);

        if (selectedItem is ServiceBusQueue selectedQueue)
        {
            mainWindowViewModel.SetSelectedQueue(selectedQueue);
            await mainWindowViewModel.FetchMessages();
            mainWindowViewModel.RefreshTabHeaders();
        }
    }

    private void ClearOtherSelections(TreeView currentTreeView)
    {
        var tvQueues = this.FindControl<TreeView>("QueuesTreeView");
        var tvTopics = this.FindControl<TreeView>("TopicsTreeView");
        if (currentTreeView == tvQueues) tvTopics?.UnselectAll();

        if (currentTreeView == tvTopics) tvQueues?.UnselectAll();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
