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
                var source = await File.ReadAllTextAsync(file);
                var lexer = new Lexer(source, file);
                var tokens = lexer.Tokenize();
                var parser = new Parser(tokens);
                files.Add(parser.Parse());
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to parse '{file}'", ex);
            }
        }
        return Merger.Merge(module.Name, files);
    }
}