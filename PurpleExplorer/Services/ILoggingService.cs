namespace PurpleExplorer.Services;

public interface ILoggingService
{
    string Logs { get; }
    void Log(string message);
}
