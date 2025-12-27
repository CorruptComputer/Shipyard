using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using FluentValidation;

namespace Shipyard.Models.Configuration.OutputFormats;

/// <summary>
///   Configuration for DEB output format.<br />
///   If this is updated, remember to update the shipyard.schema.json file as well.
/// </summary>
public record DebConfig : FormatConfigurationBase
{
    /// <summary>
    ///   The name of the package.
    /// </summary>
    [JsonPropertyName("packageName")]
    public string? PackageName { get; init; }

    /// <summary>
    ///   The section of the package (e.g., "utils", "net", "admin").
    /// </summary>
    [JsonPropertyName("section")]
    public string? Section { get; init; }

    /// <summary>
    ///   The priority of the package (e.g., "optional", "standard", "required").
    /// </summary>
    [JsonPropertyName("priority")]
    public string? Priority { get; init; }

    /// <summary>
    ///   The maintainer of the package, must be in the format "Name &lt;email&gt;".
    /// </summary>
    [JsonPropertyName("maintainer")]
    public string? Maintainer { get; init; }

    /// <summary>
    ///   Optionally, the dependencies which this package requires.
    /// </summary>
    [JsonPropertyName("depends")]
    public List<string>? Depends { get; init; }

    /// <summary>
    ///   Optionally, a flag indicating whether to install a systemd service unit.
    /// </summary>
    [JsonPropertyName("installSystemdService")]
    public bool? InstallSystemdService { get; init; }

    /// <summary>
    ///   Optionally, the name of the systemd service.
    /// </summary>
    [JsonPropertyName("systemdServiceName")]
    public string? SystemdServiceName { get; init; }

    // Quick validation to get rid of nullability warnings elsewhere
    // Should NOT be used for full validation of config correctness
    [MemberNotNullWhen(true, nameof(PackageName))]
    [MemberNotNullWhen(true, nameof(Section))]
    [MemberNotNullWhen(true, nameof(Priority))]
    [MemberNotNullWhen(true, nameof(Maintainer))]
    internal bool HasNonNullRequiredValues
        => !string.IsNullOrWhiteSpace(PackageName)
            && !string.IsNullOrWhiteSpace(Section)
            && !string.IsNullOrWhiteSpace(Priority)
            && !string.IsNullOrWhiteSpace(Maintainer);

    internal sealed class Validator : AbstractValidator<DebConfig>
    {
        public Validator()
        {
            RuleFor(config => config.PackageName)
                .NotEmpty().WithMessage("'packageName' in 'deb' configuration is required.");

            RuleFor(config => config.Section)
                .NotEmpty().WithMessage("'section' in 'deb' configuration is required.");

            RuleFor(config => config.Priority)
                .NotEmpty().WithMessage("'priority' in 'deb' configuration is required.");

            RuleFor(config => config.Maintainer)
                .NotEmpty().WithMessage("'maintainer' in 'deb' configuration is required.")
                .Matches(@"^.+\s<.+@.+\..+>$").WithMessage("'maintainer' must be in the format 'Name <email>'.");

            When(config => config.InstallSystemdService == true, () =>
            {
                RuleFor(config => config.SystemdServiceName)
                    .NotEmpty().WithMessage("'systemdServiceName' in 'deb' configuration is required when 'installSystemdService' is true.");
            });
        }
    }
}
