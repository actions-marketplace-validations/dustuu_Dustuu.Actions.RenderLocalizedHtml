using CommandLine;

namespace Dustuu.Actions.BunnyCdnDeploy;

public class ActionInputs
{
    [Option('w', "workspace", Required = true)]
    public string Workspace { get; set; } = null!;

    [Option('d', "directory", Required = true)]
    public string Directory { get; set; } = null!;
}
