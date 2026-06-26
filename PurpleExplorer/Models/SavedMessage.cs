using System.Collections.Generic;

namespace PurpleExplorer.Models;

public class SavedMessage
{
    public string? Title { get; set; }
    public string? Message { get; init; }
    public List<ApplicationProperty> ApplicationProperties { get; set; } = [];
}
