using VectraLang.Ast.AstNodes;
using VectraLang.Core;
using VectraLang.Core.Diagnostics;

namespace VectraLang.Ast;

public static class FileBuilder
{
    public static async Task<Result<VectraFile>> Build(string sourceFile, IVectraLogger logger, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(sourceFile))
            {
                logger.Error("Parse", $"File not found: {sourceFile}");
                return new Result<VectraFile>(false, null);
            }
            var source = await File.ReadAllTextAsync(sourceFile, ct);
            var lexer = new Lexer(source, sourceFile, logger);
            var tokens = lexer.Tokenize();
            logger.Debug("Parse", $"Lexing complete. {tokens.Count} tokens found.");
            var parser = new Parser(tokens, logger);
            var ast = parser.Parse();
            logger.Debug("Parse", "Parsing complete.");
            return new Result<VectraFile>(true, ast);
        }
        catch (Exception ex)
        {
            logger.Error("Parse", ex.Message);
            return new Result<VectraFile>(false, null);
        }
    }
}