namespace VectraLang.ModuleLoader;

public record MergedPackage(
    string PackageName,
    string Version,
    List<MergedModule> Modules);