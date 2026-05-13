namespace VectraLang.ModuleLoader.Models;

public sealed record VectraPackage(
    string Name,
    string Version,
    List<VectraModule> Modules);
    
public sealed record PackageLoadResult(
    VectraPackage? Package,
    List<string> Warnings,
    List<string> Errors)
{
    public bool IsSuccess => Errors.Count == 0;
}    