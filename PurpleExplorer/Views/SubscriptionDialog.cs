using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Layout;
using Avalonia.Media;

namespace PurpleExplorer.Views;

internal sealed class SubscriptionDialog : Window
{
    private SubscriptionDialog(string title, string message, string action, Control? input = null)
    {
        Title = title;
        Width = 440;
        SizeToContent = SizeToContent.Height;
        CanResize = false;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;

        CancelButton = new Button { Content = "Cancel", IsCancel = true };
        CancelButton.Click += (_, _) => Close();
        ActionButton = new Button { Content = action };

        var content = new StackPanel { Margin = new Thickness(16), Spacing = 12 };
        content.Children.Add(new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap });
        if (input != null)
            content.Children.Add(input);
        content.Children.Add(new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { CancelButton, ActionButton }
        });
        Content = content;
    }

    private Button CancelButton { get; }
    private Button ActionButton { get; }

    public static Task<string?> PromptForName(string topicName, string prefix, string suffix)
    {
        var name = new TextBox { Text = prefix + topicName + suffix };
        AutomationProperties.SetName(name, "Subscription name");
        var dialog = new SubscriptionDialog(
            "Create new subscription", $"Enter a name for the new subscription on '{topicName}'.", "Create", name);

        dialog.ActionButton.IsDefault = true;
        dialog.ActionButton.IsEnabled = !string.IsNullOrWhiteSpace(name.Text);
        name.TextChanged += (_, _) => dialog.ActionButton.IsEnabled = !string.IsNullOrWhiteSpace(name.Text);
        dialog.ActionButton.Click += (_, _) =>
        {
            if (!string.IsNullOrWhiteSpace(name.Text))
                dialog.Close(name.Text.Trim());
        };
        dialog.Opened += (_, _) =>
        {
            name.Focus();
            name.SelectionStart = prefix.Length;
            name.SelectionEnd = prefix.Length + topicName.Length;
        };
        return dialog.ShowForMainWindow();
    }

    public static Task<string?> ChooseSubscription(string topicName, IReadOnlyList<string> subscriptionNames)
    {
        var subscriptions = new ComboBox
        {
            ItemsSource = subscriptionNames,
            PlaceholderText = "Select a subscription",
            HorizontalAlignment = HorizontalAlignment.Stretch
        };
        AutomationProperties.SetName(subscriptions, "Subscription");
        var dialog = new SubscriptionDialog(
            "Delete subscription", $"Choose the subscription to delete from '{topicName}'.", "Delete", subscriptions);

        dialog.ActionButton.IsEnabled = false;
        subscriptions.SelectionChanged += (_, _) => dialog.ActionButton.IsEnabled = subscriptions.SelectedItem != null;
        dialog.ActionButton.Click += (_, _) => dialog.Close(subscriptions.SelectedItem);
        dialog.Opened += (_, _) => subscriptions.Focus();
        return dialog.ShowForMainWindow();
    }

    public static async Task<bool> ConfirmDeletion(string namespaceName, string topicName, string subscriptionName)
    {
        var dialog = new SubscriptionDialog("Delete subscription",
            $"Delete subscription '{subscriptionName}' from topic '{topicName}' on '{namespaceName}'? This also deletes its messages.",
            "Delete");

        dialog.CancelButton.IsDefault = true;
        dialog.Opened += (_, _) => dialog.CancelButton.Focus();
        dialog.ActionButton.Click += (_, _) => dialog.Close(subscriptionName);
        return await dialog.ShowForMainWindow() != null;
    }

    private Task<string?> ShowForMainWindow()
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } owner })
            return ShowDialog<string?>(owner);

        return Task.FromResult<string?>(null);
    }
}
