using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using VectraLang.Ast;
using VectraLang.Core;
using VectraLang.Core.Diagnostics;

namespace VectraLang.Commands;

public class BuildCommand : AsyncCommand<BuildCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("The .vec, .vmod, or .vpkg file to build.")]
        public string? File { get; init; }
        
        [CommandOption("-v|--verbose")]
        [Description("Enable verbose logging.")]
        public bool Verbose { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(settings.File) || !File.Exists(settings.File))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {settings.File}");
            return 1;
        }

        var logger = new SpectreLogger(settings.Verbose ? DiagnosticSeverity.Debug : DiagnosticSeverity.Info);

        var extension = Path.GetExtension(settings.File).ToLowerInvariant();
        return extension switch
        {
            ".vec" => await BuildSingleFile(settings, logger, ct),
            // ".vmod" => await BuildModule(settings),
            // ".vpkg" => await BuildPackage(settings),
            _ => UnknownExtension(settings.File)
        };
    }
    
    private static int UnknownExtension(string fileName)
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] Unknown file extension: {fileName}");
        return 1;
    }

    private static async Task<int> BuildSingleFile(Settings settings, IVectraLogger logger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings.File); // Should never throw, but doing this to prevent warnings and convince the compiler that `File` is not null
        try
        {
            var res = await FileBuilder.Build(settings.File, logger, ct);
            if (!res.Success)
            {
                logger.Error("Parse", $"Failed to build '{settings.File}'");
                return 1;
            }
            logger.Info("Parse", $"Successfully parsed '{settings.File}'");

            var file = res.Value!;
            var binder = new Binder(logger);
            var program = binder.Bind(file);
            logger.Info("Bind", "Binding complete.");
            if (!program.IsSuccess)
            {
                foreach (var error in program.Errors)
                {
                    AnsiConsole.MarkupLine($"[red]Error: [/] {error}");
                }
                return 1;
            }
            return 0;
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine($"[red]Error: [/] {e.Message}");
            return 1;
        }
    }
}