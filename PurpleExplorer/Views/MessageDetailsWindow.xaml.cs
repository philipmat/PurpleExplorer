using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace PurpleExplorer.Views;

public class MessageDetailsWindow : Window
{
    public MessageDetailsWindow()
    {
        InitializeComponent();
#if DEBUG
        this.AttachDevTools();
#endif
        /*
        Opened += (sender, e) =>
        {
            var firstField = this.FindControl<Control>("MessageId");
            // firstField?.Focus();
        };
        */
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
