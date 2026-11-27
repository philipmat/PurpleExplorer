using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using PurpleExplorer.Helpers;
using PurpleExplorer.Models;
using PurpleExplorer.ViewModels;

namespace PurpleExplorer.Views;

public class MainWindow : Window
{
    private readonly MenuFlyout _topicFilterHistory;
    private readonly MenuFlyout _queueFilterHistory;
    private readonly ObservableCollection<MenuItem> _topicFilterItems = [];
    private readonly ObservableCollection<MenuItem> _queueFilterItems = [];

    private MainWindowViewModel? _viewModel;
    private bool _isClearingSelection;

    public MainWindow()
    {
        InitializeComponent();
        _topicFilterHistory = (MenuFlyout)this.FindControl<DropDownButton>("TopicFilterHistoryButton")!.Flyout!;
        _queueFilterHistory = (MenuFlyout)this.FindControl<DropDownButton>("QueueFilterHistoryButton")!.Flyout!;
        _topicFilterHistory.ItemsSource = _topicFilterItems;
        _queueFilterHistory.ItemsSource = _queueFilterItems;
#if DEBUG
        this.AttachDevTools();
#endif
        Opened += MainWindow_Opened;
        Closing += (_, _) => _viewModel?.RememberTreeFilter();
        DataContextChanged += MainWindow_DataContextChanged;
    }

    private void MainWindow_DataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.TreeSelectionInvalidated -= ClearTreeSelection;
            _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
        }

        _viewModel = DataContext as MainWindowViewModel;
        if (_viewModel != null)
        {
            _viewModel.TreeSelectionInvalidated += ClearTreeSelection;
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        RefreshTreeFilterHistory();
    }

    private void ClearTreeSelection(object? sender, EventArgs e)
    {
        _isClearingSelection = true;
        try
        {
            this.FindControl<TreeView>("TopicsTreeView")?.UnselectAll();
            this.FindControl<TreeView>("QueuesTreeView")?.UnselectAll();
        }
        finally
        {
            _isClearingSelection = false;
        }
    }

    private void MainWindow_Opened(object? sender, EventArgs e)
    {
        var mainWindowViewModel = DataContext as MainWindowViewModel;
        mainWindowViewModel?.ConnectionBtnPopupCommand();
    }

    private void TreeFilter_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender != e.Source) return;

        _viewModel?.RememberTreeFilter();
        e.Handled = true;
    }

    private void TreeFilter_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (sender == e.Source)
            _viewModel?.RememberTreeFilter();
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainWindowViewModel.TreeFilterHistory))
            RefreshTreeFilterHistory();
    }

    private void RefreshTreeFilterHistory()
    {
        PopulateTreeFilterMenu(_topicFilterHistory, _topicFilterItems);
        PopulateTreeFilterMenu(_queueFilterHistory, _queueFilterItems);
    }

    private void PopulateTreeFilterMenu(MenuFlyout menu, ObservableCollection<MenuItem> items)
    {
        items.Clear();
        if (_viewModel is not { TreeFilterHistory.Count: > 0 } viewModel)
        {
            items.Add(new MenuItem { Header = "No recent filters", IsEnabled = false });
            return;
        }

        foreach (var filter in viewModel.TreeFilterHistory)
        {
            var item = new MenuItem { Header = filter.Replace("_", "__"), MaxWidth = 400 };
            item.Click += (_, _) =>
            {
                menu.Hide();
                viewModel.SearchText = filter;
                viewModel.RememberTreeFilter();
            };
            items.Add(item);
        }
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

        if (viewModal.MessageDeleted && viewModal.CurrentMessage is { } deletedMessage)
        {
            if (deletedMessage.IsDlq)
                mainWindowViewModel.DlqMessages.Remove(deletedMessage);
            else
                mainWindowViewModel.Messages.Remove(deletedMessage);
            mainWindowViewModel.RefreshTabHeaders();
        }
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
        if (selectedItem is ServiceBusResource selectedServiceBus)
            mainWindowViewModel.SetSelectedServiceBus(selectedServiceBus);

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
