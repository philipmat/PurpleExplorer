using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using PurpleExplorer.ViewModels;

namespace PurpleExplorer.Helpers;

public static class ModalWindowHelper
{
    public static async Task ShowModalWindow<T>(ViewModelBase viewModel) where T : Window, new()
    {
        Window? mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?
            .Windows[0];

        if (mainWindow == null) return;
        var window = new T
        {
            DataContext = viewModel
        };

        await window.ShowDialog(mainWindow);
    }

    public static async Task<TVm?> ShowModalWindow<TW, TVm>(ViewModelBase viewModel)
        where TW : Window, new() where TVm : ViewModelBase
    {
        Window? mainWindow = (Application.Current?.ApplicationLifetime as IClassicDesktopStyleApplicationLifetime)?
            .Windows[0];

        if (mainWindow == null) return null;

        var window = new TW
        {
            DataContext = viewModel
        };

        await window.ShowDialog(mainWindow);
        // focus the window so the key bindings of that window work
        window.Focus();
        return window.DataContext as TVm;
    }
}
