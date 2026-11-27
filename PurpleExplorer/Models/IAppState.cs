using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace PurpleExplorer.Models;

public interface IAppState
{
    public ObservableCollection<ServiceBusConnectionString> SavedConnectionStrings { get; set; }
    public ObservableCollection<SavedMessage> SavedMessages { get; set; }
    public AppSettings AppSettings { get; set; }
    public List<string> TreeFilterHistory { get; set; }
}
