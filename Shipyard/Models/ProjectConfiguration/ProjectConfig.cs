using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using Shipyard.Models.ProjectConfiguration.FormatConfiguration;

namespace Shipyard.Models.ProjectConfiguration;

/// <summary>
///   Root configuration for a Shipyard package project.<br />
///   If this is updated, remember to update the shipyard.schema.json file as well.
/// </summary>
[JsonSerializable(typeof(ProjectConfig))]
public record ProjectConfig
{
    /// <summary>
    ///   The path to the .NET project file (.csproj, .fsproj, or .vbproj).<br />
    ///   If relative, interpreted relative to the configuration file location.
    /// </summary>
    [JsonPropertyName("projectFile")]
    public string? ProjectFile { get; init; }

    /// <summary>
    ///   The version of .NET to target (e.g., "net9.0", "net10.0").<br />
    ///   See: https://learn.microsoft.com/en-us/dotnet/standard/frameworks#supported-target-frameworks
    /// </summary>
    [JsonPropertyName("framework")]
    public string? Framework { get; init; }

    /// <summary>
    ///   The runtime identifier (RID) for the target platform (e.g., "linux-x64", "linux-arm64").<br />
    ///   See: https://learn.microsoft.com/en-us/dotnet/core/rid-catalog#linux-rids
    /// </summary>
    [JsonPropertyName("runtimes")]
    public List<DotnetRuntimes>? Runtimes { get; init; }

    /// <summary>
    ///   The version of the project.
    /// </summary>
    [JsonPropertyName("version")]
    public string? Version { get; init; }

    /// <summary>
    ///   The developer or author of the project.
    /// </summary>
    [JsonPropertyName("author")]
    public string? Author { get; init; }

    /// <summary>
    ///   The license of the project (SPDX format recommended).
    /// </summary>
    [JsonPropertyName("license")]
    public string? License { get; init; }

    /// <summary>
    ///   The repository URL for the project source.
    /// </summary>
    [JsonPropertyName("repositoryUrl")]
    public string? RepositoryUrl { get; init; }

    /// <summary>
    ///   The format-specific configurations.
    /// </summary>
    [JsonPropertyName("formatConfigs")]
    public List<FormatConfigurationBase>? FormatConfigs { get; init; }

    // Quick validation to get rid of nullability warnings elsewhere
    // Should NOT be used for full validation of config correctness
    [MemberNotNullWhen(true, nameof(ProjectFile))]
    [MemberNotNullWhen(true, nameof(Framework))]
    [MemberNotNullWhen(true, nameof(Runtimes))]
    [MemberNotNullWhen(true, nameof(Version))]
    [MemberNotNullWhen(true, nameof(Author))]
    [MemberNotNullWhen(true, nameof(License))]
    [MemberNotNullWhen(true, nameof(RepositoryUrl))]
    [MemberNotNullWhen(true, nameof(FormatConfigs))]
    internal bool HasNonNullRequiredValues
        => !string.IsNullOrWhiteSpace(ProjectFile)
            && !string.IsNullOrWhiteSpace(Framework)
            && Runtimes is not null
            && !string.IsNullOrWhiteSpace(Version)
            && !string.IsNullOrWhiteSpace(Author)
            && !string.IsNullOrWhiteSpace(License)
            && !string.IsNullOrWhiteSpace(RepositoryUrl)
            && FormatConfigs is not null;

    internal bool Validate(TextWriter errorWriter, DirectoryInfo configDirectory)
    {
        bool isValid = true;

        if (!HasNonNullRequiredValues)
        {
            errorWriter.WriteLine("Project configuration is missing required values.");
            isValid = false;
        }

        if (ProjectFile is not null)
        {
            // If ProjectFile is an absolute path, use it as-is; otherwise, resolve relative to config directory
            string projectFilePath = Path.IsPathRooted(ProjectFile)
                ? ProjectFile
                : Path.GetFullPath(Path.Combine(configDirectory.FullName, ProjectFile));

            if (!File.Exists(projectFilePath))
            {
                errorWriter.WriteLine($"Project file '{projectFilePath}' does not exist.");
                isValid = false;
            }
        }

        if (Runtimes is not null)
        {
            if (Runtimes.Count == 0)
            {
                errorWriter.WriteLine("At least one runtime identifier must be specified.");
                isValid = false;
            }

            // Ensure no duplicate runtimes
            Dictionary<DotnetRuntimes, int> runtimeCounts = Runtimes
                .GroupBy(rt => rt)
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (KeyValuePair<DotnetRuntimes, int> kvp in runtimeCounts.Where(kvp => kvp.Value > 1))
            {
                errorWriter.WriteLine($"Multiple entries found for runtime '{kvp.Key}'. Only one entry per runtime is allowed.");
                isValid = false;
            }
        }

        if (FormatConfigs is not null)
        {
            if (FormatConfigs.Count == 0)
            {
                errorWriter.WriteLine("At least one format configuration must be specified.");
                isValid = false;
            }

            // There should only be one config per format
            Dictionary<PackageFormat, int> formatCounts = FormatConfigs
                .GroupBy(fc => fc switch
                {
                    RpmConfig => PackageFormat.rpm,
                    _ => throw new InvalidDataException($"Unsupported format configuration type: {fc.GetType().Name}")
                })
                .ToDictionary(g => g.Key, g => g.Count());

            foreach (KeyValuePair<PackageFormat, int> kvp in formatCounts.Where(kvp => kvp.Value > 1))
            {
                isValid = false;
                errorWriter.WriteLine($"Multiple configurations found for format '{kvp.Key}'. Only one configuration per format is allowed.");
            }

            foreach (FormatConfigurationBase formatConfig in FormatConfigs)
            {
                bool formatIsValid = formatConfig.Validate(errorWriter);
                if (!formatIsValid)
                {
                    isValid = false;
                }
            }
        }

        return isValid;
    }
}
