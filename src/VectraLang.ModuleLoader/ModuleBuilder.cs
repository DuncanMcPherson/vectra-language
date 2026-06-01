using VectraLang.Ast;
using VectraLang.Ast.AstNodes;
using VectraLang.ModuleLoader.Models;

namespace VectraLang.ModuleLoader;

public static class ModuleBuilder
{
    public static async Task<MergedModule> Build(VectraModule module)
    {
        var files = new List<VectraFile>();
        foreach (var file in module.ResolvedSourceFiles)
        {
            try
            {
                var res = await FileBuilder.Build(file);
                if (!res.Success)
                    throw new InvalidOperationException($"Failed to parse '{file}'");
                files.Add(res.Value!);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse '{file}'", ex);
            }
        }
        return Merger.Merge(module.Name, files, module.Type == ModuleType.Executable);
    }
}