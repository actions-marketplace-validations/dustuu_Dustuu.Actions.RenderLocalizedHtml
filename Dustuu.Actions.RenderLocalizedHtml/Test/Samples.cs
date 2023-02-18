namespace Dustuu.Actions.BunnyCdnDeploy.Test;

internal class Samples
{
    private static readonly Translations _defaultTranslations = new()
    {
        DefaultCulture = "en-US",
        Ids = new Dictionary<string, Dictionary<string, string>>()
        {
            {
                "test",
                new Dictionary<string, string>()
                {
                    { "en-US", "This is a test" },
                    { "ja-JP", "これはテストです" }
                }
            }
        }
    };

    public static Translations DefaultTranslations => _defaultTranslations;
}
