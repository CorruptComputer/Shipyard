namespace Shipyard.Models;

/// <summary>
///   Result of a package creation operation.
/// </summary>
public record PackageResult(
    string Format,
    string PackagePath,
    bool Success,
    Exception? Error = null);
