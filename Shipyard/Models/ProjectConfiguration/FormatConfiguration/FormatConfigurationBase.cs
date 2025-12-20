using System.Text.Json.Serialization;

namespace Shipyard.Models.ProjectConfiguration.FormatConfiguration;

/// <summary>
/// Base class for format-specific configurations.<br />
///   If this is updated, remember to update the shipyard.schema.json file as well.
/// </summary>
[JsonSerializable(typeof(FormatConfigurationBase), TypeInfoPropertyName = "format")]
[JsonPolymorphic(TypeDiscriminatorPropertyName = "format")]
//[JsonDerivedType(typeof(DebConfig), nameof(PackageFormat.Deb))]
[JsonDerivedType(typeof(RpmConfig), nameof(PackageFormat.rpm))]
//[JsonDerivedType(typeof(TarConfig), nameof(PackageFormat.Tarball))]
public abstract record FormatConfigurationBase
{
    internal abstract bool Validate(TextWriter errorWriter);
}
