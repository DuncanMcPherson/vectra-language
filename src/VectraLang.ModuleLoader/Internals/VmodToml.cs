
using Tomlyn.Serialization;
#if DEBUG
using JetBrains.Annotations;
#endif

// ReSharper disable CollectionNeverUpdated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.

namespace VectraLang.ModuleLoader.Internals;

#if DEBUG
[UsedImplicitly]
#endif
[TomlSerializable(typeof(VmodToml))]
internal sealed class VmodToml
{
    public ModuleToml Module { get; set; }
    public SourcesToml Sources { get; set; }
    public Dictionary<string, string> Dependencies { get; set; } = new();
}

#if DEBUG
[UsedImplicitly]
#endif
internal sealed class ModuleToml
{
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
}

#if DEBUG
[UsedImplicitly]
#endif
internal sealed class SourcesToml
{
    public List<string>? Files { get; set; } = [];
    public List<string>? Globs { get; set; } = [];
}