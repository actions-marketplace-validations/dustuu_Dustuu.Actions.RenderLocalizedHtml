using CommandLine;
using HtmlAgilityPack;
using System.Globalization;
using System.Text.Json;
using static System.Text.Json.JsonSerializer;

namespace Dustuu.Actions.BunnyCdnDeploy;

internal partial class Program
{
    private const string INDEX_HTML = "index.html";

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

        // Find Local Directories and Files
        // Workspace Directory
        DirectoryInfo workspace = new (inputs.Workspace);
        if (!workspace.Exists) { throw new DirectoryNotFoundException(workspace.FullName); }
        // Translation Json File
        FileInfo translationJson = new (Path.Join(workspace.FullName, inputs.Translation));
        if (!translationJson.Exists) { throw new FileNotFoundException(translationJson.FullName); }
        // Input Directory
        DirectoryInfo input = new (Path.Join(workspace.FullName, inputs.Input));
        if (!input.Exists) { throw new DirectoryNotFoundException(input.FullName); }
        // Input Html File
        FileInfo inputIndexHtml = new (Path.Join(input.FullName, INDEX_HTML));
        if (!inputIndexHtml.Exists) { throw new FileNotFoundException(inputIndexHtml.FullName); }
        // Output Directory (Delete existing and recreate if needed)
        DirectoryInfo output = new (Path.Join(workspace.FullName, inputs.Output));
        if (output.Exists) { output.Delete(true); }
        output.Create();

        // Parse Translations JSON
        string translationJsonText = await translationJson.OpenText().ReadToEndAsync();
        Translation translation = Deserialize<Translation>(translationJsonText, _opts)!;
        LogJson(nameof(translation), translation);

        // Find all Culture strings represented in the translation JSON
        IEnumerable<string> cultureStrings = translation.Ids.Values
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

        void LocalizeFile(CultureInfo culture, DirectoryInfo localizedRoot)
        {
            FileInfo cultureHtml = inputIndexHtml.CopyTo(Path.Join(localizedRoot.FullName, INDEX_HTML), true);
            HtmlDocument cultureHtmlDoc = new();
            cultureHtmlDoc.Load(cultureHtml.FullName);

            Dictionary<string, string> idToTranslation =
                translation.Ids
                .Where(id => id.Value.ContainsKey(culture.Name))
                .ToDictionary
                (d => d.Key, d => d.Value[culture.Name]);

            foreach (string id in idToTranslation.Keys)
            {
                string translation = idToTranslation[id];

                string xPath = $"//*[@id='{id}']/text()";
                HtmlNodeCollection textNodes = cultureHtmlDoc.DocumentNode.SelectNodes(xPath);
                if (textNodes is null)
                {
                    Log($"No nodes found with id: '{id}'");
                    continue;
                }

                foreach (HtmlTextNode node in textNodes.Cast<HtmlTextNode>())
                {
                    Log($"Replacing {culture.Name}: <{node.NodeType} id='{id}'/> => {translation}...");
                    node.Text = translation;
                }
            }

            Log("----------");
            cultureHtmlDoc.Save(Console.OpenStandardOutput());
            Log("----------");
            cultureHtmlDoc.Save(cultureHtml.FullName);
        }

        // Get default culture
        CultureInfo cultureDefault = cultures.Single
            (ci => string.Equals(ci.Name, translation.DefaultCulture, StringComparison.OrdinalIgnoreCase));

        // Create a root directory to store the localized output
        LocalizeFile(cultureDefault, output);

        // Replace
        foreach (CultureInfo culture in cultures)
        {
            DirectoryInfo cultureDirectory = output.CreateSubdirectory(culture.Name);
            LocalizeFile(culture, cultureDirectory);
        }

        Log("Done!");
        Environment.Exit(0);
    }
}