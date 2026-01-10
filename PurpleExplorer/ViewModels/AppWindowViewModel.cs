using System.Reactive;
using System.Threading.Tasks;
using PurpleExplorer.Helpers;
using ReactiveUI;

namespace PurpleExplorer.ViewModels;

public class AppWindowViewModel : ViewModelBase
{
    public AppWindowViewModel()
    {
        AboutPageCommand = ReactiveCommand.Create(AboutPage);
    }

    public ReactiveCommand<Unit, Task> AboutPageCommand { get; }

    // TODO: catch exceptions inside the method and log to console
    private async Task AboutPage()
    {
        await MessageBoxHelper.ShowMessage(
            "About Purple Explorer",
            "Purple Explorer - cross-platform Azure Service Bus explorer (Windows, macOS, Linux) \n\n" +
            "Thank you for using Purple Explorer! \n " +
            "For updated information on the functionalities that Purple Explorer supports, please visit: \n " +
            "https://github.com/telstrapurple/PurpleExplorer ");
    }
}
