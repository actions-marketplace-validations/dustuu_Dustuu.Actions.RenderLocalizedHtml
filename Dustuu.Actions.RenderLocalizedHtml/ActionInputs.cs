using CommandLine;

namespace Dustuu.Actions.BunnyCdnDeploy;

public class ActionInputs
{
    [Option('w', "workspace", Required = true)]
    public string Workspace { get; set; } = null!;

    [Option('t', "translation", Required = true)]
    public string Translation { get; set; } = null!;

    [Option('i', "input", Required = true)]
    public string Input { get; set; } = null!;

    [Option('o', "output", Required = true)]
    public string Output { get; set; } = null!;
}
