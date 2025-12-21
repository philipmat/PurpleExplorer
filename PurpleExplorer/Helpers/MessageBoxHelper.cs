using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using MsBox.Avalonia;
using MsBox.Avalonia.Dto;
using MsBox.Avalonia.Enums;
using System.Threading.Tasks;

namespace PurpleExplorer.Helpers;

public static class MessageBoxHelper
{
    public static async Task<ButtonResult> ShowConfirmation(string title, string message)
    {
        return await ShowMessageBox(ButtonEnum.YesNo, Icon.Warning, title, message);
    }
        
    // ReSharper disable once UnusedMethodReturnValue.Global
    public static async Task<ButtonResult> ShowError(string message)
    {
        return await ShowMessageBox(ButtonEnum.Ok, Icon.Error, "Error", message);
    }

    // ReSharper disable once UnusedMethodReturnValue.Global
    public static async Task<ButtonResult> ShowMessage(string title, string message)
    {
        return await ShowMessageBox(ButtonEnum.Ok, Icon.Info, title, message);
    }

    private static async Task<ButtonResult> ShowMessageBox(ButtonEnum buttons, Icon icon, string title, string message)
    {
        var box = MessageBoxManager.GetMessageBoxStandard(new MessageBoxStandardParams
        {
            ButtonDefinitions = buttons,
            ContentTitle = title,
            ContentMessage = message,
            Icon = icon,
            CanResize = true,
            WindowStartupLocation = Avalonia.Controls.WindowStartupLocation.CenterOwner
        });

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            return await box.ShowWindowDialogAsync(desktop.MainWindow!);
        }
        
        return ButtonResult.None;
    }
}