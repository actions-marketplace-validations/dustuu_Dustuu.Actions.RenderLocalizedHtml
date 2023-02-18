using CommandLine;
using HtmlAgilityPack;
using System.Globalization;
using System.Text.Json;
using static System.Text.Json.JsonSerializer;

namespace Dustuu.Actions.BunnyCdnDeploy;

internal partial class Program
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private static void Log(string message) => Console.WriteLine(message);

    private static void LogLabel(string label, string message) =>
        Log($"{label}: {message}");

    private static void LogJoin(string label, IEnumerable<string> parts) =>
        LogLabel(label, $"[{string.Join(',', parts)}]");

    private static void LogJson(string label, object obj) =>
        LogLabel(label, Serialize(obj, _opts));

    private static async Task Main(string[] args)
    {
        // Configure Cancellation
        using CancellationTokenSource tokenSource = new();
        Console.CancelKeyPress += delegate { tokenSource.Cancel(); };

        // Configure Inputs
        ParserResult<ActionInputs> parser = Parser.Default.ParseArguments<ActionInputs>(args);
        if (parser.Errors.ToArray() is { Length: > 0 } errors)
        {
            foreach (Error error in errors) { LogLabel(nameof(error), error.Tag.ToString()); }
            Environment.Exit(2);
            return;
        }
        ActionInputs inputs = parser.Value;

        // Find Local Files
        DirectoryInfo workspace = new(inputs.Workspace);
        DirectoryInfo directory = workspace.CreateSubdirectory(inputs.Directory);
        FileInfo translationJson = new(Path.Join(directory.FullName, "translations.json"));
        // TODO: Throw exceptions if files not found

        // Parse Translations JSON
        string translationJsonText = await translationJson.OpenText().ReadToEndAsync();
        Translations translations = Deserialize<Translations>(translationJsonText, _opts)!;
        LogJson(nameof(translations), translations);

        // Find all Culture strings represented in the translation JSON
        IEnumerable<string> cultureStrings = translations.Ids.Values
            .SelectMany(v => v.Keys)
            .Distinct();
        LogJoin(nameof(cultureStrings), cultureStrings);

        // Validate Cultures
        IEnumerable<CultureInfo> cultures = CultureInfo.GetCultures(CultureTypes.AllCultures)
            .Where
            (
                ci =>
                cultureStrings.Any
                (
                    s =>
                    string.Equals(ci.Name, s, StringComparison.OrdinalIgnoreCase)
                )
            );
        LogJoin(nameof(cultures), cultures.Select(ci => ci.Name));

        // Get default culture
        CultureInfo cultureDefault = cultures.Single
            (ci => string.Equals(ci.Name, translations.DefaultCulture, StringComparison.OrdinalIgnoreCase));

        // Load the source index HTML
        FileInfo indexHtml = new(Path.Join(directory.FullName, "index.html"));
        HtmlDocument indexHtmlDoc = new();
        indexHtmlDoc.Load(indexHtml.FullName);

        // Create a root directory to store the localized output
        DirectoryInfo localizedRoot = directory.CreateSubdirectory("localized");
        FileInfo indexHtmlDefault = new(Path.Join(localizedRoot.FullName, indexHtml.Name));
        indexHtmlDoc.Save(indexHtmlDefault.FullName);
        LocalizeFile(cultureDefault, indexHtmlDefault, translations);

        // Replace
        foreach (CultureInfo culture in cultures)
        {
            DirectoryInfo cultureDirectory = localizedRoot.CreateSubdirectory(culture.Name);
            FileInfo cultureFile = new(Path.Join(cultureDirectory.FullName, indexHtml.Name));
            // Copy the source
            indexHtmlDoc.Save(cultureFile.FullName);
            LocalizeFile(culture, cultureFile, translations);
        }

        Log("Done!");
        Environment.Exit(0);
    }

    private static void LocalizeFile(CultureInfo culture, FileInfo cultureHtml, Translations translations)
    {
        Dictionary<string, string> idToTranslation =
            translations.Ids
            .Where(id => id.Value.ContainsKey(culture.Name))
            .ToDictionary
            (
                d => d.Key,
                d => d.Value[culture.Name]
            );

        HtmlDocument cultureHtmlDoc = new();
        cultureHtmlDoc.Load(cultureHtml.FullName);

        foreach (string id in idToTranslation.Keys)
        {
            string translation = idToTranslation[id];

            Log($"Replacing {culture.Name}: '{id}' => {translation}...");

            string xPath = $"//*[@id='{id}']/text()";
            HtmlNodeCollection textNodes = cultureHtmlDoc.DocumentNode.SelectNodes(xPath);
            if (textNodes is null)
            {
                Log($"No nodes found with id: '{id}'");
                continue;
            }

            foreach (HtmlTextNode node in textNodes.Cast<HtmlTextNode>())
            {
                node.Text = translation;
            }
        }

        cultureHtmlDoc.Save(cultureHtml.FullName);
    }
}