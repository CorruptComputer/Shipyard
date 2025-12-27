using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using FluentValidation;

namespace Shipyard.Models.Configuration.OutputFormats;

/// <summary>
///   Configuration specific to RPM (RedHat/CentOS/Fedora) package format.<br />
///   If this is updated, remember to update the shipyard.schema.json file as well.
/// </summary>
[JsonSerializable(typeof(RpmConfig))]
public record RpmConfig : FormatConfigurationBase
{
    /// <summary>
    ///   The name of the package.
    /// </summary>
    [JsonPropertyName("packageName")]
    public string? PackageName { get; set; }

    /// <summary>
    ///   The release number (incremented for rebuilds of the same version).
    /// </summary>
    [JsonPropertyName("release")]
    public int? Release { get; set; }

    /// <summary>
    ///   Optionally, the dependencies which this package provides.
    /// </summary>
    [JsonPropertyName("provides")]
    public List<string>? Provides { get; set; }

    /// <summary>
    ///   Optionally, the dependencies which this package requires.
    /// </summary>
    [JsonPropertyName("dependsOn")]
    public List<string>? DependsOn { get; set; }

    /// <summary>
    ///   Optionally, a flag indicating whether to install a systemd service unit.
    /// </summary>
    [JsonPropertyName("installSystemdService")]
    public bool? InstallSystemdService { get; set; }

    /// <summary>
    ///   Optionally, the name of the systemd service.
    /// </summary>
    [JsonPropertyName("systemdServiceName")]
    public string? SystemdServiceName { get; set; }

    /// <summary>
    ///   Optionally, a script to run before installation.
    /// </summary>
    [JsonPropertyName("preInstallScript")]
    public string? PreInstallScript { get; set; }

    /// <summary>
    ///   Optionally, a script to run after installation.
    /// </summary>
    [JsonPropertyName("postInstallScript")]
    public string? PostInstallScript { get; set; }

    /// <summary>
    ///   Optionally, a script to run before uninstallation.
    /// </summary>
    [JsonPropertyName("preUninstallScript")]
    public string? PreUninstallScript { get; set; }

    /// <summary>
    ///   Optionally, a script to run after uninstallation.
    /// </summary>
    [JsonPropertyName("postUninstallScript")]
    public string? PostUninstallScript { get; set; }

    // Quick validation to get rid of nullability warnings elsewhere
    // Should NOT be used for full validation of config correctness
    [MemberNotNullWhen(true, nameof(PackageName))]
    [MemberNotNullWhen(true, nameof(Release))]
    internal bool HasNonNullRequiredValues
        => !string.IsNullOrWhiteSpace(PackageName)
            && Release.HasValue;

    internal sealed class Validator : AbstractValidator<RpmConfig>
    {
        public Validator()
        {
            RuleFor(config => config.PackageName)
                .NotEmpty().WithMessage("'packageName' in 'rpm' configuration is required.");

            RuleFor(config => config.Release)
                .NotNull().WithMessage("'release' in 'rpm' configuration is required.")
                .GreaterThan(0).WithMessage("'release' in 'rpm' configuration must be a positive integer.");

            When(config => config.InstallSystemdService == true, () =>
            {
                RuleFor(config => config.SystemdServiceName)
                    .NotEmpty().WithMessage("'systemdServiceName' in 'rpm' configuration is required when 'installSystemdService' is true.");
            });
        }
    }
}
