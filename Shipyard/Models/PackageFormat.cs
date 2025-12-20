namespace Shipyard.Models;

/// <summary>
///   The supported package formats.
/// </summary>
public enum PackageFormat
{
    /// <summary>
    ///   The DEB format.
    /// </summary>
    deb,

    /// <summary>
    ///   The RPM format.
    /// </summary>
    rpm,

    /// <summary>
    ///   .tar.gz format.
    /// </summary>
    tarball
}
