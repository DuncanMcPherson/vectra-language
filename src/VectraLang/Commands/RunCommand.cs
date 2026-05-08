using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using VectraLang.Ast;
using VectraLang.Ast.AstNodes;
using VectraLang.Formatters;
using VectraLang.ModuleLoader;
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

    private static async Task<int> RunModule(Settings settings)
    {
        var result = await Loader.Load(settings.File!);
        foreach (var warning in result.Warnings)
            AnsiConsole.MarkupLine($"[yellow]Warning:[/] {warning}");
        if (!result.IsSuccess)
        {
            foreach (var error in result.Errors)
                AnsiConsole.MarkupLine($"[red]Error:[/] {error}");
            return 1;
        }

        var module = result.Module!;
        AnsiConsole.MarkupLine($"[green]Module:[/] {module.Name} ([grey]{module.Type}[/])");
        AnsiConsole.MarkupLine($"[green]Files:[/] {module.ResolvedSourceFiles.Count} source file(s) resolved");
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

                if (settings.PrintAst)
                {
                    AnsiConsole.MarkupLine($"\n[grey]AST: {file}[/]");
                    var printer = new AstPrinter();
                    printer.Print(files[^1]);
                }
            }
            catch (Exception ex)
            {
                AnsiConsole.MarkupLine($"[red]Error:[/] Failed to parse '{file}': {ex.Message}");
                return 1;
            }
        }

        // var interpreter = new Interpreter.Interpreter();
        // TODO: Figure out merging
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