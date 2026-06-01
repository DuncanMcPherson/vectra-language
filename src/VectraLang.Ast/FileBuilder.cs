using Spectre.Console;
using VectraLang.Ast.AstNodes;
using VectraLang.Core;

namespace VectraLang.Ast;

public static class FileBuilder
{
    public static async Task<Result<VectraFile>> Build(string sourceFile, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(sourceFile))
            {
                AnsiConsole.MarkupLine($"[red]Error: [/]File not found: {sourceFile}");
                return new Result<VectraFile>(false, null);
            }
            var source = await File.ReadAllTextAsync(sourceFile, ct);
            var lexer = new Lexer(source, sourceFile);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            var ast = parser.Parse();
            return new Result<VectraFile>(true, ast);
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"[red]Error: [/]{ex.Message}");
            return new Result<VectraFile>(false, null);
        }
    }
}