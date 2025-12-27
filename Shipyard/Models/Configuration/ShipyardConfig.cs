using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;
using FluentValidation;
using FluentValidation.Results;
using Shipyard.Models.Configuration.OutputFormats;

namespace Shipyard.Models.Configuration;

/// <summary>
///   Root configuration for a Shipyard package project.<br />
///   If this is updated, remember to update the shipyard.schema.json file as well.
/// </summary>
[JsonSerializable(typeof(ShipyardConfig))]
public record ShipyardConfig
{
    /// <summary>
    ///   The name of the executable to be built.
    /// </summary>
    [JsonPropertyName("executable")]
    public string? Executable { get; init; }

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
    ///   The .NET publishing configuration.
    /// </summary>
    [JsonPropertyName("dotnet")]
    public DotnetConfig? Dotnet { get; init; }

    /// <summary>
    ///   The format-specific configurations.
    /// </summary>
    [JsonPropertyName("formats")]
    public List<FormatConfigurationBase>? FormatConfigs { get; init; }

    // Quick validation to get rid of nullability warnings elsewhere
    // Should NOT be used for full validation of config correctness

    [MemberNotNullWhen(true, nameof(Executable))]
    [MemberNotNullWhen(true, nameof(Version))]
    [MemberNotNullWhen(true, nameof(Author))]
    [MemberNotNullWhen(true, nameof(License))]
    [MemberNotNullWhen(true, nameof(RepositoryUrl))]
    [MemberNotNullWhen(true, nameof(Dotnet))]
    [MemberNotNullWhen(true, nameof(FormatConfigs))]
    internal bool HasNonNullRequiredValues
        => !string.IsNullOrWhiteSpace(Executable)
            && !string.IsNullOrWhiteSpace(Version)
            && !string.IsNullOrWhiteSpace(Author)
            && !string.IsNullOrWhiteSpace(License)
            && !string.IsNullOrWhiteSpace(RepositoryUrl)
            && Dotnet is not null
            && FormatConfigs is not null;

    internal sealed class Validator : AbstractValidator<ShipyardConfig>
    {
        public Validator(DirectoryInfo configDirectory)
        {
            RuleFor(config => config.Executable)
                .NotEmpty().WithMessage("'executable' is required.");
            RuleFor(config => config.Version)
                .NotEmpty().WithMessage("'version' is required.");
            RuleFor(config => config.Author)
                .NotEmpty().WithMessage("'author' is required.");
            RuleFor(config => config.License)
                .NotEmpty().WithMessage("'license' is required.");
            RuleFor(config => config.RepositoryUrl)
                .NotEmpty().WithMessage("'repositoryUrl' is required.")
                .Must(uri => Uri.IsWellFormedUriString(uri, UriKind.Absolute)).WithMessage("RepositoryUrl must be a valid absolute URI.");

            RuleFor(config => config.Dotnet)
                .NotNull().WithMessage("'dotnet' section of configuration is missing.")
                .Custom((dc, ctx) =>
                {
                    if (dc is null)
                    {
                        return;
                    }

                    DotnetConfig.Validator dotnetValidator = new(configDirectory);
                    ValidationResult result = dotnetValidator.Validate(dc);
                    foreach (ValidationFailure failure in result.Errors)
                    {
                        ctx.AddFailure(failure);
                    }
                });

            RuleFor(config => config.Dotnet!)
                .SetValidator(new DotnetConfig.Validator(configDirectory)).When(c => c is not null);

            RuleFor(config => config.FormatConfigs)
                .NotEmpty().WithMessage("'formats' must contain at least one format configuration.")
                .Custom((fc, ctx) =>
                {
                    if (fc is null)
                    {
                        return;
                    }

                    foreach (FormatConfigurationBase formatConfig in fc)
                    {
                        switch (formatConfig)
                        {
                            case RpmConfig rc:
                                RpmConfig.Validator formatValidator = new();
                                ValidationResult result = formatValidator.Validate(rc);
                                foreach (ValidationFailure failure in result.Errors)
                                {
                                    ctx.AddFailure(failure);
                                }
                                break;
                        }

                    }
                })
                .Must(fc =>
                {
                    if (fc is null)
                    {
                        return true;
                    }

                    List<Type> types = [.. fc.Select(f => f.GetType())];

                    return types.Distinct().Count() == types.Count;
                }).WithMessage("'formats' contains duplicate format.");
        }
    }

}
