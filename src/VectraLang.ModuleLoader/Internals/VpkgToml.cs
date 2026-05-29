namespace VectraLang.ModuleLoader.Internals;

internal sealed class VpkgToml
{
    public PackageToml Package { get; set; } = new();
    public WorkspaceToml Workspace { get; set; } = new();
    public Dictionary<string, string> Dependencies { get; set; } = new();
}

internal sealed class PackageToml
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
}

internal sealed class WorkspaceToml
{
    public List<string> Modules { get; set; } = [];
}