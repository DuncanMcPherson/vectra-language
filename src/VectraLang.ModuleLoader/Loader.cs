using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.FileSystemGlobbing.Abstractions;
using Tomlyn;
using VectraLang.ModuleLoader.Internals;
using VectraLang.ModuleLoader.Models;

namespace VectraLang.ModuleLoader;

public static class Loader
{
    public static async Task<PackageLoadResult> LoadPackage(string filePath)
    {
        List<string> warnings = [];
        List<string> errors = [];
        filePath = Path.GetFullPath(filePath);
        try
        {
            var moduleSource = File.OpenRead(filePath);
            var options = new TomlSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            var toml = TomlSerializer.Deserialize<VpkgToml>(moduleSource, options);

            if (toml is null)
            {
                throw new Exception("Failed to parse TOML");
            }
            
            var localPath = Path.GetDirectoryName(filePath) ?? string.Empty;

            var moduleTasks = toml.Workspace.Modules.Select(mPath => Load(Path.Combine(localPath, mPath)));
            var moduleResults = await Task.WhenAll(moduleTasks);
            
            if (moduleResults.Any(m => m.Module is null))
                throw new Exception("Failed to load one or more modules");
            return new PackageLoadResult(new VectraPackage
            (toml.Package.Name, toml.Package.Version, moduleResults.Select(m => m.Module!).ToList()), warnings, errors);
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to load package from '{filePath}': {ex.Message}");
            return new PackageLoadResult(null, warnings, errors);
        }
    }

    public static Task<ModuleLoadResult> Load(string filePath)
    {
        if (!filePath.EndsWith(".vmod"))
            filePath += ".vmod";
        List<string> warnings = [];
        List<string> errors = [];
        filePath = Path.GetFullPath(filePath);
        try
        {
            var moduleSource = File.OpenRead(filePath);
            var options = new TomlSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            };
            var toml = TomlSerializer.Deserialize<VmodToml>(moduleSource, options);
            if (toml?.Module is null || toml.Sources is null)
                throw new Exception("Failed to parse TOML");
            var resolvedFilesToBuild =
                ResolveFileGlobs(toml.Sources, warnings, errors, Path.GetDirectoryName(filePath)!);
            return Task.FromResult(new ModuleLoadResult(
                new VectraModule(toml.Module.Name, ResolveModuleType(toml.Module.Type), resolvedFilesToBuild,
                    Path.GetDirectoryName(filePath) ?? string.Empty), warnings, errors));
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to load module from '{filePath}': {ex.Message}");
            return Task.FromResult(new ModuleLoadResult(null, warnings, errors));
        }
    }

    private static List<string> ResolveFileGlobs(SourcesToml globs, List<string> warnings, List<string> errors,
        string basePath)
    {
        var matcher = new Matcher(StringComparison.OrdinalIgnoreCase);

        errors.AddRange(from file in globs.Files
            let absolute = Path.GetFullPath(Path.Combine(basePath, file))
            where !File.Exists(absolute)
            select $"File '{absolute}' does not exist");
        foreach (var pattern in globs.Globs!)
        {
            matcher.AddInclude(pattern);
        }

        var directoryInfo = new DirectoryInfoWrapper(new DirectoryInfo(basePath));

        var foundFiles = from file in globs.Files
            let absolute = Path.GetFullPath(Path.Combine(basePath, file))
            where File.Exists(absolute)
            select absolute;

        var matches = matcher.Execute(directoryInfo);
        var files = matches.Files
            .Select(f => Path.GetFullPath(Path.Combine(basePath, f.Path)))
            .Where(p => p.EndsWith(".vec", StringComparison.Ordinal))
            .Concat(foundFiles)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();
        if (files.Count == 0)
        {
            warnings.Add($"No .vec files found in '{basePath}' matching globs: {string.Join(", ", globs.Globs)}");
        }

        return files;
    }

    private static ModuleType ResolveModuleType(string type)
    {
        return type switch
        {
            "library" => ModuleType.Library,
            "executable" => ModuleType.Executable,
            "tests" => ModuleType.Tests,
            _ => throw new ArgumentException($"Unknown module type '{type}'")
        };
    }
}