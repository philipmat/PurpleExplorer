using System.Runtime.Serialization;
using ReactiveUI;

namespace PurpleExplorer.Models;

public class ApplicationProperty : ReactiveObject
{
    private string _key = string.Empty;
    private string _value = string.Empty;

    [DataMember]
    public string Key
    {
        get => _key;
        set => this.RaiseAndSetIfChanged(ref _key, value);
    }

    [DataMember]
    public string Value
    {
        get => _value;
        set => this.RaiseAndSetIfChanged(ref _value, value);
    }
}
