using System.ComponentModel;
using Spectre.Console;
using Spectre.Console.Cli;
using VectraLang.Analysis;
using VectraLang.Ast;
using VectraLang.Core;
using VectraLang.Core.Diagnostics;
using VectraLang.Lowering;
using VectraLang.ModuleLoader;

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
            ".vmod" => await BuildModule(settings, logger, ct),
            ".vpkg" => await BuildPackage(settings, logger, ct),
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
        ArgumentNullException
            .ThrowIfNull(settings
                .File); // Should never throw, but doing this to prevent warnings and convince the compiler that `File` is not null
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
            logger.Info("Analysis", "Starting analysis...");
            var analyzer = new Analyzer(logger);
            analyzer.Analyze(program);

            var lowerer = new Lowerer(logger);
            var lowered = lowerer.Lower(program);
            return 0;
        }
        catch (Exception e)
        {
            AnsiConsole.MarkupLine($"[red]Error: [/] {e.Message}");
            return 1;
        }
    }

    private static async Task<int> BuildModule(Settings settings, IVectraLogger logger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings.File);
        try
        {
            var loadResult = await Loader.Load(settings.File);
            foreach (var warning in loadResult.Warnings)
            {
                logger.Warning("Load", warning);
            }

            if (!loadResult.IsSuccess)
            {
                foreach (var error in loadResult.Errors)
                    logger.Error("Load", error);
                return 1;
            }

            var module = loadResult.Module!;
            logger.Info("Load", $"Successfully loaded '{module.Name}'");
            
            var mergedModule = await ModuleBuilder.Build(module, logger, ct);
            logger.Info("Parse", $"Successfully parsed '{module.Name}'");
            var binder = new Binder(logger);
            var program = binder.Bind(mergedModule);
            logger.Info("Bind", "Binding complete.");
            logger.Info("Analysis", "Starting analysis...");
            var analyzer = new Analyzer(logger);
            analyzer.Analyze(program);
            return 0;
        }
        catch (Exception e)
        {
            logger.Error("Build", e.Message);
            return 1;
        }
    }

    private static async Task<int> BuildPackage(Settings settings, IVectraLogger logger, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(settings.File);
        try
        {
            var loadResult = await Loader.LoadPackage(settings.File);
            foreach (var warning in loadResult.Warnings)
            {
                logger.Warning("Load", warning);
            }

            if (!loadResult.IsSuccess)
            {
                foreach (var error in loadResult.Errors)
                    logger.Error("Load", error);
                return 1;
            }

            var package = loadResult.Package!;
            logger.Info("Load", $"Successfully loaded '{package.Name}'");
            var mergedPackage = await PackageBuilder.Build(package, logger, ct);
            logger.Info("Build", $"Successfully built '{package.Name}'");
            var binder = new Binder(logger);
            var program = binder.Bind(mergedPackage);
            logger.Info("Bind", "Binding complete.");
            logger.Info("Analysis", "Starting analysis...");
            var analyzer = new Analyzer(logger);
            analyzer.Analyze(program);
            var lowerer = new Lowerer(logger);
            var lowered = lowerer.LowerPackage(program);
            return 0;
        }
        catch (Exception e)
        {
            logger.Error("Build", e.Message);
            return 1;
        }
    }
}