namespace VectraLang.ModuleLoader.Models;

public sealed record VectraModule(
    string Name,
    ModuleType Type,
    List<string> ResolvedSourceFiles,
    string ModuleDirectory);

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