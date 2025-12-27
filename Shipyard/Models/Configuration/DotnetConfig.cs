using System;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using FluentValidation;

namespace Shipyard.Models.Configuration;

/// <summary>
///   Configurations for .NET publishing in Shipyard.<br />
///   If this is updated, remember to update the shipyard.schema.json file as well.
/// </summary>
[JsonSerializable(typeof(DotnetConfig))]
public record DotnetConfig
{
    /// <summary>
    ///   The path to the .NET project file (.csproj, .fsproj, or .vbproj).<br />
    ///   If relative, interpreted relative to the configuration file location.
    /// </summary>
    [JsonPropertyName("projectFile")]
    public string? ProjectFile { get; init; }

    /// <summary>
    ///   The build configuration (e.g., "Release" or "Debug").
    /// </summary>
    [JsonPropertyName("configuration")]
    public string? Configuration { get; init; }

    /// <summary>
    ///   The target framework (e.g., "net8.0", "net9.0").
    /// </summary>
    [JsonPropertyName("framework")]
    public string? Framework { get; init; }

    /// <summary>
    ///   The publish options.
    /// </summary>
    [JsonPropertyName("publish")]
    public List<DotnetPublishOptions>? Publish { get; init; }

    /// <summary>
    ///   The runtime identifiers (RIDs) for target platforms (e.g., "linux-x64", "linux-arm64").
    /// </summary>
    [JsonPropertyName("runtimes")]
    public List<DotnetRuntimes>? Runtimes { get; init; }

        // Quick validation to get rid of nullability warnings elsewhere
    // Should NOT be used for full validation of config correctness
    [MemberNotNullWhen(true, nameof(ProjectFile))]
    [MemberNotNullWhen(true, nameof(Configuration))]
    [MemberNotNullWhen(true, nameof(Framework))]
    [MemberNotNullWhen(true, nameof(Runtimes))]
    internal bool HasNonNullRequiredValues
        => !string.IsNullOrWhiteSpace(ProjectFile)
            && !string.IsNullOrWhiteSpace(Configuration)
            && !string.IsNullOrWhiteSpace(Framework)
            && Runtimes is not null;

    internal string? GetResolvedProjectFile(DirectoryInfo configDirectory)
    {
        if (string.IsNullOrWhiteSpace(ProjectFile))
        {
            return null;
        }

        // If ProjectFile is an absolute path, use it as-is; otherwise, resolve relative to config directory
        return Path.IsPathRooted(ProjectFile)
            ? ProjectFile
            : Path.GetFullPath(Path.Combine(configDirectory.FullName, ProjectFile));
    }

    internal sealed class Validator : AbstractValidator<DotnetConfig>
    {
        public Validator(DirectoryInfo configDirectory)
        {
            RuleFor(config => config.ProjectFile)
                .NotEmpty().WithMessage("'projectFile' in 'dotnet' configuration is required.")
                .Must(path => (path?.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase) ?? false)
                           || (path?.EndsWith(".fsproj", StringComparison.OrdinalIgnoreCase) ?? false)
                           || (path?.EndsWith(".vbproj", StringComparison.OrdinalIgnoreCase) ?? false))
                           .WithMessage("'projectFile' in 'dotnet' configuration must point to a valid .NET project file (.csproj, .fsproj, or .vbproj).");

            RuleFor(config => config.GetResolvedProjectFile(configDirectory))
                .NotEmpty().WithMessage("'projectFile' in 'dotnet' configuration: project does not exist.");

            RuleFor(config => config.Configuration)
                .NotEmpty().WithMessage("'configuration' in 'dotnet' configuration  is required.")
                .Must(value => value == "Debug" || value == "Release").WithMessage("'configuration' in 'dotnet' configuration must be either 'Debug' or 'Release'.");

            RuleFor(config => config.Framework)
                .NotEmpty().WithMessage("'framework' in 'dotnet' configuration is required.")
                .Must(value => value!.StartsWith("net", StringComparison.InvariantCulture)).WithMessage("'framework' in 'dotnet' configuration must be a valid .NET target framework (e.g., 'net8.0').");

            RuleFor(config => config.Publish)
                .Must(po =>
                {
                    if (po is null || po.Count == 0)
                    {
                        return true;
                    }

                    foreach (DotnetPublishOptions option in po)
                    {
                        if (!Enum.IsDefined(option))
                        {
                            return false;
                        }
                    }

                    return true;
                }).WithMessage("'publish' in 'dotnet' configuration contains invalid publish options.");

            RuleFor(config => config.Runtimes)
                .NotEmpty().WithMessage("'runtimes' in 'dotnet' configuration is required.")
                .Must(runtimes =>
                {
                    if (runtimes is null || runtimes.Count == 0)
                    {
                        return true;
                    }

                    foreach (DotnetRuntimes runtime in runtimes)
                    {
                        if (!Enum.IsDefined(runtime))
                        {
                            return false;
                        }
                    }

                    return true;
                }).WithMessage("'runtimes' in 'dotnet' configuration contains invalid runtime identifiers.")
                .Must(runtimes =>
                {
                    if (runtimes is null)
                    {
                        return true;
                    }

                    return runtimes.Distinct().Count() == runtimes.Count;
                }).WithMessage("'runtimes' in 'dotnet' configuration contains duplicate runtime identifiers.");
        }
    }
}
