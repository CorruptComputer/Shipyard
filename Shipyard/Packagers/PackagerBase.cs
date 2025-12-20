using Shipyard.Models;
using Shipyard.Models.ProjectConfiguration;
using Shipyard.Models.ProjectConfiguration.FormatConfiguration;
using Shipyard.Templates;

namespace Shipyard.Packagers;

/// <summary>
///   The base class for all packagers.
/// </summary>
/// <param name="sourceDir"></param>
/// <param name="outputDir"></param>
/// <param name="projectConfig"></param>
/// <param name="templateResults"></param>
/// <param name="consoleWriter"></param>
/// <param name="errorWriter"></param>
public abstract class PackagerBase(DirectoryInfo sourceDir, DirectoryInfo outputDir, ProjectConfig projectConfig, IEnumerable<TemplateResult> templateResults, TextWriter consoleWriter, TextWriter errorWriter)
{
    /// <summary>
    ///   The source directory containing the source code to be packaged.
    /// </summary>
    protected DirectoryInfo SourceDir { get; } = sourceDir;

    /// <summary>
    ///   The output directory for generated packages, also contains published output.
    /// </summary>
    protected DirectoryInfo OutputDir { get; } = outputDir;

    /// <summary>
    ///   The project configuration associated with this run.
    /// </summary>
    protected ProjectConfig ProjectConfig { get; } = projectConfig;

    /// <summary>
    ///   The built template results.
    /// </summary>
    protected IEnumerable<TemplateResult> TemplateResults { get; } = templateResults;

    /// <summary>
    ///   The console output writer.
    /// </summary>
    protected TextWriter ConsoleWriter { get; } = consoleWriter;

    /// <summary>
    ///   The error output writer.
    /// </summary>
    protected TextWriter ErrorWriter { get; } = errorWriter;

    /// <summary>
    ///   Creates the package(s) according to the configurations.
    /// </summary>
    public abstract Task<IEnumerable<PackageResult>> CreatePackagesAsync(CancellationToken cancellationToken = default);
}

/// <inheritdoc />
public abstract class PackagerBase<TFormatConfig>(DirectoryInfo sourceDir, DirectoryInfo outputDir, ProjectConfig projectConfig, IEnumerable<TemplateResult> templateResults, TextWriter consoleWriter, TextWriter errorWriter)
    : PackagerBase(sourceDir, outputDir, projectConfig, templateResults, consoleWriter, errorWriter)
    where TFormatConfig : FormatConfigurationBase
{
    /// <summary>
    ///   Creates a package for the specified format configuration.
    /// </summary>
    /// <param name="formatConfig">Format-specific configuration</param>
    /// <param name="buildTemplate">The built template result, if applicable.</param>
    protected abstract Task<PackageResult> CreatePackageForFormatAsync(TFormatConfig formatConfig, TemplateResult? buildTemplate = null);

    /// <inheritdoc />
    public override async Task<IEnumerable<PackageResult>> CreatePackagesAsync(CancellationToken cancellationToken = default)
    {
        TFormatConfig? formatConfig = ProjectConfig.FormatConfigs?.OfType<TFormatConfig>().SingleOrDefault();

        if (formatConfig is null)
        {
            await ErrorWriter.WriteLineAsync($"No format configuration of type {typeof(TFormatConfig).Name} found.");
            return [];
        }

        ConsoleWriter.WriteLine($"Creating packages for format config {formatConfig.GetType().Name}, {TemplateResults.Count()} template results...");

        IEnumerable<PackageResult> results = [];
        foreach (TemplateResult tr in TemplateResults)
        {
            ConsoleWriter.WriteLine($" - Template for runtime {tr.Runtime}, output file: {tr.OutputFile.FullName}");
            results = results.Append(await CreatePackageForFormatAsync(formatConfig, tr));
        }

        return results;
    }
}
