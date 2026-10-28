using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Data;

namespace PurpleExplorer.Controls;

public class Icon : Projektanker.Icons.Avalonia.Icon
{
    public Icon()
    {
        // The package's Avalonia 11 template calls a binding API removed in Avalonia 12.
        // Replace only its template, retaining the provider, color and image handling.
        Template = new FuncControlTemplate<Icon>((icon, _) =>
        {
            var image = new Image();
            image.Bind(WidthProperty, new Binding(nameof(FontSize)) { Source = icon });
            image.Bind(HeightProperty, new Binding(nameof(FontSize)) { Source = icon });
            image.Bind(Avalonia.Controls.Image.SourceProperty, new Binding(nameof(Image)) { Source = icon });
            return image;
        });
    }
}
