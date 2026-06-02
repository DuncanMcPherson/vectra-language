using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using VectraLang.Ast;
using VectraLang.Core;

namespace VectraLang.Commands;

public class BuildCommand : AsyncCommand<BuildCommand.Settings>
{
    public sealed class Settings : CommandSettings
    {
        [CommandArgument(0, "<file>")]
        [Description("The .vec, .vmod, or .vpkg file to build.")]
        public string? File { get; init; }
    }

    protected override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(settings.File) || !File.Exists(settings.File))
        {
            AnsiConsole.MarkupLine($"[red]Error:[/] File not found: {settings.File}");
            return 1;
        }

        var extension = Path.GetExtension(settings.File).ToLowerInvariant();
        return extension switch
        {
            ".vec" => await BuildSingleFile(settings, ct),
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

    private static async Task<int> BuildSingleFile(Settings settings, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings.File); // Should never throw, but doing this to prevent warnings and convince the compiler that `File` is not null
        try
        {
            var res = await FileBuilder.Build(settings.File, ct);
            if (!res.Success)
            {
                AnsiConsole.MarkupLine($"[red]Error: Failed to build '{settings.File}'");
                return 1;
            }

            var file = res.Value!;
            var binder = new Binder();
            var program = binder.Bind(file);
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