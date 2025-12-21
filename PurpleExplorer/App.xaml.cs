using System.IO;
using Avalonia;
using Avalonia.Markup.Xaml;
using Avalonia.ReactiveUI;
using PurpleExplorer.Helpers;
using PurpleExplorer.Models;
using PurpleExplorer.Services;
using PurpleExplorer.ViewModels;
using PurpleExplorer.Views;
using ReactiveUI;
using Splat;

namespace PurpleExplorer;

public class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var appStatePath = "appstate.json";
        /* probably fixed with d009705, but leaving this here as a workaround if it shows up again
        if (!File.Exists(appStatePath))
        {
            if (File.Exists("appstate.json.backup"))
            {
               File.Copy("appstate.json.backup", "appstate.json"); 
            }
            else
            {
                File.Create(appStatePath).Close();
            }
        }
        //*/
            
        var suspension = new AutoSuspendHelper(ApplicationLifetime);
        RxApp.SuspensionHost.CreateNewAppState = () => new AppState();
        RxApp.SuspensionHost.SetupDefaultSuspendResume(new NewtonsoftJsonSuspensionDriver(appStatePath));
        suspension.OnFrameworkInitializationCompleted();
        var state = RxApp.SuspensionHost.GetAppState<AppState>();

        Locator.CurrentMutable.RegisterLazySingleton(() => state, typeof(IAppState));
        Locator.CurrentMutable.RegisterLazySingleton(() => new LoggingService(), typeof(ILoggingService));
        Locator.CurrentMutable.Register(() => new TopicHelper(state.AppSettings), typeof(ITopicHelper));
        Locator.CurrentMutable.Register(() => new QueueHelper(state.AppSettings), typeof(IQueueHelper));

        new MainWindow { DataContext = new MainWindowViewModel() }.Show();
        base.OnFrameworkInitializationCompleted();
    }
}