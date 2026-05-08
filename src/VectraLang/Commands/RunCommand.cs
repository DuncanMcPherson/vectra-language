using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using VectraLang.Ast;
using VectraLang.Formatters;
// ReSharper disable ClassNeverInstantiated.Global
// ReSharper disable UnusedAutoPropertyAccessor.Global

namespace VectraLang.Commands;

public class RunCommand : AsyncCommand<RunCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("The .vec, .vmod, or .vpkg file to run.")]
        public string? File { get; init; }

        [CommandOption("--ast")]
        [Description("Print the AST before running.")]
        public bool PrintAst { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings,
        CancellationToken ct)
    {
        if (string.IsNullOrEmpty(settings.File) || !File.Exists(settings.File))
        {
            AnsiConsole.MarkupLine($"[red]Error: [/]File not found: {settings.File}");
            return 1;
        }

        var extension = Path.GetExtension(settings.File).ToLowerInvariant();

        return extension switch
        {
            ".vec" => await RunSingleFile(settings, ct),
            ".vmod" => await RunModule(settings),
            ".vpkg" => await RunPackage(settings),
            _ => UnknownExtension(settings.File)
        };
    }

    private static async Task<int> RunSingleFile(Settings settings, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(settings.File);
        try
        {
            var source = await File.ReadAllTextAsync(settings.File!, ct);
            var lexer = new Lexer(source, settings.File);
            var tokens = lexer.Tokenize();
            var parser = new Parser(tokens);
            var program = parser.Parse();

            if (settings.PrintAst)
            {
                var printer = new AstPrinter();
                printer.Print(program);
            }

            var interpreter = new Interpreter.Interpreter();
            interpreter.Interpret(program);
            return 0;
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine($"[red]Error: [/] {e.Message}");
            return 1;
        }
    }

    private static async Task<int> RunModule(Settings _)
    {
        AnsiConsole.MarkupLine("[yellow]Warning:[/] Module support coming soon.");
        return 0;
    }

    private static async Task<int> RunPackage(Settings _)
    {
        AnsiConsole.MarkupLine("[yellow]Warning:[/] Package support coming soon.");
        return 0;
    }
    
    private static int UnknownExtension(string? file)
    {
        AnsiConsole.MarkupLine($"[red]Error: [/]Unknown file extension: {file}");
        return 1;
    }
}