namespace VectraLang.ModuleLoader.Internals;


internal sealed class VmodToml
{
    public ModuleToml Module { get; set; }
    public SourcesToml Sources { get; set; }
    public Dictionary<string, string> Dependencies { get; set; } = new();
}

internal sealed class ModuleToml
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

internal sealed class SourcesToml
{
    public List<string> Files { get; set; } = [];
    public string? Glob { get; set; }
}