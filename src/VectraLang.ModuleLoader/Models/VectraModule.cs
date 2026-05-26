namespace VectraLang.ModuleLoader.Models;

/// <summary>
/// Represents a module within the VectraLang ecosystem, providing details
/// such as the module's name, its designated type, resolved source files
/// for the module, and the directory where the module is located.
/// </summary>
public sealed record VectraModule(
    string Name,
    ModuleType Type,
    List<string> ResolvedSourceFiles,
    string ModuleDirectory,
    List<string> Dependencies);

public enum ModuleType
{
    Executable,
    Library,
    Tests
}

public sealed record ModuleLoadResult(
    VectraModule? Module,
    List<string> Warnings,
    List<string> Errors)
{
    public bool IsSuccess => Errors.Count == 0;
}