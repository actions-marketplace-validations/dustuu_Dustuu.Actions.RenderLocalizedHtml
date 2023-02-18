﻿using CommandLine;
using HtmlAgilityPack;
using System.Globalization;
using System.Runtime.CompilerServices;
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
        FileInfo indexHtml = new(Path.Join(directory.FullName, "index.html"));
        // TODO: Throw exceptions if files not found

        // Parse Translations
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

        // Create directories
        IDictionary<CultureInfo, DirectoryInfo> cultureDirectories =
            cultures.ToDictionary
            (
                ci => ci,
                ci => directory.CreateSubdirectory(ci.Name)
            );
        LogJoin(nameof(cultureDirectories), cultureDirectories.Values.Select(di => di.FullName));

        // Create files
        IDictionary<CultureInfo, FileInfo> cultureFiles =
            cultures.ToDictionary
            (
                ci => ci,
                ci => new FileInfo(Path.Join(cultureDirectories[ci].FullName, "index.html"))
            );
        LogJoin(nameof(cultureFiles), cultureFiles.Values.Select(fi => fi.FullName));

        // Copy HTML for localizations
        // From File
        HtmlDocument doc = new();
        doc.Load(indexHtml.FullName);
        IDictionary<CultureInfo, HtmlDocument> cultureHtmlDocs =
            cultures.ToDictionary
            (
                ci => ci,
                ci =>
                {
                    doc.Save(cultureFiles[ci].FullName);
                    HtmlDocument cultureHtmlDoc = new();
                    cultureHtmlDoc.Load(cultureFiles[ci].FullName);
                    return cultureHtmlDoc;
                }
            );

        // Replace
        foreach (CultureInfo culture in cultureHtmlDocs.Keys)
        {
            HtmlDocument cultureHtmlDoc = cultureHtmlDocs[culture];

            Dictionary<string, string> idToTranslation =
                translations.Ids
                .Where(id => id.Value.ContainsKey(culture.Name))
                .ToDictionary
                (
                    d => d.Key,
                    d => d.Value[culture.Name]
                );

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

            cultureHtmlDoc.Save(cultureFiles[culture].FullName);
        }

        Log("Done!");
        Environment.Exit(0);
    }
}