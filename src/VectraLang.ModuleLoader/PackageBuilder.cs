using VectraLang.Core.Diagnostics;
using VectraLang.ModuleLoader.Models;

namespace VectraLang.ModuleLoader;

public static class PackageBuilder
{
    public static async Task<MergedPackage> Build(VectraPackage package, IVectraLogger logger)
    {
        var sorted = ModuleSorter.TopoSort(package.Modules);
        var mergedModules = new List<MergedModule>();
        foreach (var module in sorted)
        {
            mergedModules.Add(await ModuleBuilder.Build(module, logger));
        }
        return new MergedPackage(package.Name, package.Version, mergedModules);
    }
}