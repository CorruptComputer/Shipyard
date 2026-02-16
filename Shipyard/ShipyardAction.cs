using System.CommandLine;
using System.CommandLine.Invocation;
using System.Text.Json;
using FluentValidation.Results;
using Shipyard.Models;
using Shipyard.Models.Configuration;
using Shipyard.Models.Configuration.OutputFormats;
using Shipyard.Packagers;
using Shipyard.Templates;
using Shipyard.Wrappers;

namespace Shipyard;

/// <summary>
///   The Shipyard command action.
/// </summary>
public sealed class ShipyardAction : AsynchronousCommandLineAction
{
    private const string ConfigOptionName = "--config";
    private const string OutputOptionName = "--output";
    private const string CleanOptionName = "--clean";
    /// <summary>
    ///   The command arguments.
    /// </summary>
    public static readonly IReadOnlyList<Option> Options =
    [
        new Option<FileInfo>(ConfigOptionName)
        {
            Description = "Path to shipyard.json config file",
            Required = true
        },
        new Option<DirectoryInfo>(OutputOptionName)
        {
            Description = "Path to the output directory for generated packages",
            Required = true
        },
        new Option<bool>(CleanOptionName)
        {
            Description = "Clean the output directory before building, if it already exists",
            DefaultValueFactory = (_) => false
        },
    ];

    /// <inheritdoc/>
    public override async Task<int> InvokeAsync(ParseResult parseResult, CancellationToken cancellationToken = default)
    {
        FileInfo configFile = parseResult.GetRequiredValue<FileInfo>(ConfigOptionName);
        DirectoryInfo output = parseResult.GetRequiredValue<DirectoryInfo>(OutputOptionName);
        bool clean = parseResult.GetValue<bool>(CleanOptionName);

        DirectoryInfo workingDir = new($"/tmp/shipyard/run-{Environment.ProcessId}");

        if (!workingDir.Exists)
        {
            if (!workingDir.Parent!.Exists)
            {
                workingDir.Parent.Create();
            }

            workingDir.Create();
        }

        TextWriter consoleWriter = parseResult.InvocationConfiguration.Output;
        TextWriter errorWriter = parseResult.InvocationConfiguration.Error;

        ShipyardConfig? config = await TryLoadProjectConfigAsync(configFile, errorWriter);
        if (config is null || !config.HasNonNullRequiredValues || !config.Dotnet.HasNonNullRequiredValues)
        {
            await errorWriter.WriteLineAsync("Failed to load configuration file.");
            workingDir.Delete(true);
            return 1;
        }

        // If ProjectFile is an absolute path, use it as-is; otherwise, resolve relative to config directory
        string projectFilePath = config.Dotnet.GetResolvedProjectFile(configFile.Directory!)!;

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

            if (clean)
            {
                consoleWriter.WriteLine($"Cleaning output directory...");
                output.Delete(true);
                output.Create();
            }
        }

        foreach (DotnetRuntimes runtime in config.Dotnet.Runtimes)
        {
            DotnetWrapper dotnetBuilder = new(consoleWriter, errorWriter);
            // Get project file path relative to the config file
            FileInfo projectFile = new(Path.Combine(configFile.Directory!.FullName, config.Dotnet.ProjectFile));
            DirectoryInfo publishDir = workingDir.CreateSubdirectory($"publish-{Enum.GetName(runtime)}");

            string? publishPath = await dotnetBuilder.PublishAsync(
                projectFile.FullName,
                publishDir.FullName,
                config.Dotnet.Configuration,
                config.Dotnet.Framework,
                runtime.ToJsonValue(),
                config.Dotnet.Publish?.Contains(DotnetPublishOptions.SelfContained) == true,
                config.Dotnet.Publish?.Contains(DotnetPublishOptions.Trim) == true,
                cancellationToken);

            if (publishPath is null)
            {
                // Error already reported by DotnetBuilder
                workingDir.Delete(true);
                return 1;
            }
        }

        List<TemplateResult> templateResults = await BuildTemplatesAsync(config, workingDir);

        consoleWriter.WriteLine($"Built {templateResults.Count} template(s).");

        List<PackagerBase> packagersNeeded = [];
        if (config.FormatConfigs?.OfType<RpmConfig>().Any() == true)
        {
            packagersNeeded.Add(
                new RpmPackager(projectDir, output, workingDir, config,
                    templateResults.Where(tr => tr.Format == PackageFormat.rpm),
                    consoleWriter, errorWriter)
            );
        }

        if (config.FormatConfigs?.OfType<DebConfig>().Any() == true)
        {
            packagersNeeded.Add(
                new DebPackager(projectDir, output, workingDir, config,
                    templateResults.Where(tr => tr.Format == PackageFormat.deb),
                    consoleWriter, errorWriter)
            );
        }

        if (packagersNeeded.Count == 0)
        {
            await errorWriter.WriteLineAsync($"No supported package formats found in configuration.");
            workingDir.Delete(true);
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
            }
            else
            {
                errorWriter.WriteLine($"✗ {result.Format}: {result.Error?.Message}");
                allSuccess = false;
            }
        }

        workingDir.Delete(true);

        return allSuccess ? 0 : 1;
    }

    private static async Task<ShipyardConfig?> TryLoadProjectConfigAsync(FileInfo configFile, TextWriter errorWriter)
    {
        ShipyardConfig? config;

        try
        {
            string jsonContent = await File.ReadAllTextAsync(configFile.FullName);
            config = JsonSerializer.Deserialize<ShipyardConfig>(jsonContent);

            if (config is not null)
            {
                ShipyardConfig.Validator configValidator = new(configFile.Directory!);
                ValidationResult validationResult = configValidator.Validate(config);
                if (!validationResult.IsValid)
                {
                    foreach (ValidationFailure failure in validationResult.Errors)
                    {
                        await errorWriter.WriteLineAsync($"{failure.ErrorMessage}");
                    }

                    return null;
                }
            }
        }
        catch (Exception ex)
        {
            await errorWriter.WriteLineAsync($"Failed to load or parse configuration file: {ex.Message}");
            config = null;
        }

        return config;
    }

    private static async Task<List<TemplateResult>> BuildTemplatesAsync(ShipyardConfig projectConfig, DirectoryInfo outputDir)
    {
        TemplateBuilder templateBuilder = new(projectConfig, outputDir);
        List<TemplateResult> templates = await templateBuilder.BuildTemplatesAsync();

        return templates;
    }
}
