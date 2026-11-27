namespace PurpleExplorer.Models;

public class AppSettings
{
    public int QueueListFetchCount { get; set; } = 100;
    public int QueueMessageFetchCount { get; set; } = 100;

    public int TopicListFetchCount { get; set; } = 100;
    public int TopicMessageFetchCount { get; set; } = 100;
    public double TopicListWidth { get; set; } = 400;
    public string NewSubscriptionPrefix { get; set; } = "test-";
    public string NewSubscriptionSuffix { get; set; } = string.Empty;
    public int TreeFilterHistoryLimit { get; set; } = 10;
}
