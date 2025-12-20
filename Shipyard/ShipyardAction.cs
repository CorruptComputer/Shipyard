using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using Shipyard.Models;
using Shipyard.Models.ProjectConfiguration;
using Shipyard.Models.ProjectConfiguration.FormatConfiguration;
using Shipyard.Packagers;
using Shipyard.Templates;
using Shipyard.Wrappers;

namespace Shipyard;

/// <summary>
///   The Shipyard command action.
/// </summary>
public sealed class ShipyardAction : AsynchronousCommandLineAction
{
    /// <summary>
    ///   The command arguments.
    /// </summary>
    public static readonly IReadOnlyList<Option> Options =
    [
        new Option<FileInfo>("--config", "-c")
        {
            Description = "Path to shipyard.json config file",
            Required = true
        },
        new Option<DirectoryInfo>("--output", "-o")
        {
            Description = "Path to the output directory for generated packages",
            Required = true
        },
    ];

    /// <inheritdoc/>
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        FileInfo config = parseResult.GetRequiredValue<FileInfo>("--config");
        DirectoryInfo output = parseResult.GetRequiredValue<DirectoryInfo>("--output");

        TextWriter consoleWriter = parseResult.InvocationConfiguration.Output;
        TextWriter errorWriter = parseResult.InvocationConfiguration.Error;

        ProjectConfig? projectConfig = await TryLoadProjectConfigAsync(config, errorWriter);
        if (projectConfig is null || !projectConfig.HasNonNullRequiredValues)
        {
            await errorWriter.WriteLineAsync("Failed to load configuration file.");
            return 1;
        }

        // If ProjectFile is an absolute path, use it as-is; otherwise, resolve relative to config directory
        string projectFilePath = Path.IsPathRooted(projectConfig.ProjectFile)
            ? projectConfig.ProjectFile
            : Path.GetFullPath(Path.Combine(config.Directory!.FullName, projectConfig.ProjectFile));

        FileInfo projectFileInfo = new(projectFilePath);
        DirectoryInfo projectDir = projectFileInfo.Directory!;

        if (!output.Exists)
        {
            consoleWriter.WriteLine($"Creating output directory at '{output.FullName}'...");
            output.Create();
        }
        else
        {
            consoleWriter.WriteLine($"Output directory '{output.FullName}' already exists.");
            if (output.GetFileSystemInfos().Length > 0)
            {
                consoleWriter.WriteLine("Output directory is not empty, aborting.");
                return 1;
            }
        }

        foreach (DotnetRuntimes runtime in projectConfig.Runtimes)
        {
            DotnetWrapper dotnetBuilder = new(consoleWriter, errorWriter);
            // Get project file path relative to the config file
            FileInfo projectFile = new(Path.Combine(config.Directory!.FullName, projectConfig.ProjectFile));
            DirectoryInfo publishDir = output.CreateSubdirectory($"publish-{Enum.GetName(runtime)}");

            string? publishPath = await dotnetBuilder.PublishAsync(
                projectFile.FullName,
                publishDir.FullName,
                projectConfig.Framework,
                runtime.ToJsonValue(),
                cancellationToken);

            if (publishPath is null)
            {
                // Error already reported by DotnetBuilder
                return 1;
            }
        }

        List<TemplateResult> templateResults = await BuildTemplatesAsync(projectConfig, output);

        consoleWriter.WriteLine($"Built {templateResults.Count} template(s).");

        List<PackagerBase> packagersNeeded = [];
        if (projectConfig.FormatConfigs?.OfType<RpmConfig>().Any() == true)
        {
            packagersNeeded.Add(
                new RpmPackager(projectDir, output, projectConfig,
                    templateResults.Where(tr => tr.Format == PackageFormat.rpm),
                    consoleWriter,
                    errorWriter)
            );
        }

        if (packagersNeeded.Count == 0)
        {
            await errorWriter.WriteLineAsync($"No supported package formats found in configuration.");
            return 1;
        }

        consoleWriter.WriteLine($"Starting package creation for {packagersNeeded.Count} packager(s)...");

        IEnumerable<PackageResult> results = [];

        foreach (PackagerBase packager in packagersNeeded)
        {
            consoleWriter.WriteLine($"Using packager: {packager.GetType().Name}");
            results = results.Concat(await packager.CreatePackagesAsync(cancellationToken));
        }

        bool allSuccess = true;
        foreach (PackageResult result in results)
        {
            if (result.Success)
            {
                consoleWriter.WriteLine($"✓ {result.Format}: {result.PackagePath}");
                File.Copy(result.PackagePath, Path.Combine(output.FullName, Path.GetFileName(result.PackagePath)));
            }
            else
            {
                errorWriter.WriteLine($"✗ {result.Format}: {result.Error?.Message}");
                allSuccess = false;
            }
        }

        return allSuccess ? 0 : 1;
    }

    private static async Task<ProjectConfig?> TryLoadProjectConfigAsync(FileInfo configFile, TextWriter errorWriter)
    {
        ProjectConfig? projectConfig;

        try
        {
            string jsonContent = await File.ReadAllTextAsync(configFile.FullName);
            projectConfig = JsonSerializer.Deserialize<ProjectConfig>(jsonContent);

            if (projectConfig is not null)
            {
                bool valid = projectConfig.Validate(errorWriter, configFile.Directory!);
                if (!valid)
                {
                    projectConfig = null;
                }
            }
        }
        catch (Exception ex)
        {
            await errorWriter.WriteLineAsync($"Failed to load or parse configuration file: {ex.Message}");
            projectConfig = null;
        }

        return projectConfig;
    }

    private static async Task<List<TemplateResult>> BuildTemplatesAsync(ProjectConfig projectConfig, DirectoryInfo outputDir)
    {
        TemplateBuilder templateBuilder = new(projectConfig, outputDir);
        List<TemplateResult> templates = await templateBuilder.BuildTemplatesAsync();

        return templates;
    }
}
