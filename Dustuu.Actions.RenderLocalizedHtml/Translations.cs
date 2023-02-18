namespace Dustuu.Actions.BunnyCdnDeploy;

public record Translations
{
    public required string DefaultCulture { get; init; }

    // [HTML ID][Language][Translation]
    public required Dictionary<string, Dictionary<string, string>> Ids { get; init; }
}
