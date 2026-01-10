using System;
using System.Text;
using ReactiveUI;

namespace PurpleExplorer.Services;

public class LoggingService : ReactiveObject, ILoggingService
{
    private readonly StringBuilder _log = new();

    public string Logs => _log.ToString();

    public void Log(string message)
    {
        _log.Insert(0, "[" + DateTime.Now.ToString("dd/MM/yyyy hh:mm:ss tt") + "] " + message + "\n");
        this.RaisePropertyChanged(nameof(Logs));
    }
}
