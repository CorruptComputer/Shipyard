# Shipyard [![Build & Tests](https://github.com/CorruptComputer/Shipyard/actions/workflows/build.yml/badge.svg?branch=develop)](https://github.com/CorruptComputer/Shipyard/actions/workflows/build.yml) [![NuGet Version](https://img.shields.io/nuget/vpre/shipyard.svg)](https://www.nuget.org/packages/shipyard)

Project to build and package .NET applications on Linux.

Current focus:
- RPM Packages

Future Goals:
- DEB Packages
- Flatpaks
- Snaps
- AppImage
- tarball

## Architecture
- Project build configuration should live in a single file, ideally one that can easily be checked into source control right along side the source code.
  - Going with a JSON config for now, yaml might be nice to add support for later for folks that prefer it.
  - Each project should only need 1 build configuration, even if they want to build for multiple platforms (x86_64, aarch64).
  - Each package that is output should have its own package config, these package configs should only contain that package types specific configuration. (eg. if building both DEB and RPM packages for the same platform they shouldn't both need to specify the platform).
- When possible, the Shipyard should use standard packaging tools and fit neatly into the ecosystem, without overstepping where it doesn't need to. (eg. Use rpmbuild to build RPM packages instead of reinventing the wheel).
- The main application flow I forsee with this is:
  1. User runs `shipyard --config path/to/shipyard.json --output path/to/output/dir`
  2. Shipyard reads and deserializes the configuration file into ProjectConfig.
  3. Shipyard validates the configuration.
    - If invalid, it outputs errors and exits.
    - This should include validating that required fields for the selected package formats are present.
    - This should also include verifying that the target project exists and can be built with the specified settings.
  4. Shipyard does a dotnet publish of the target project to the $"{output}/publish-{arch}{(selfContained ? string.Empty : "-sc")}" directory.
    -- `dotnet publish ./Example/Example.csproj --output ./publish-x86_64 --configuration Release --framework net10.0 --runtime linux-x64 --no-self-contained --verbosity minimal` or similar
  5. Shipyard uses the Packagers to create the packages for each of the package type configs in the ProjectConfig.
    - This allows, for example, building both x86_64 and aarch64 RPMs and DEBs (4 packages total in this example) at the same time.
  6. Shipyard outputs the generated packages to the the $"{output}" directory.
    - Hopefully ending up with a filesystem that looks something like this with the example above:
      - $"{output}/publish-x64"
      - $"{output}/publish-arm64"
      - $"{output}/Project-1.0.0-1.arm64.deb"
      - $"{output}/Project-1.0.0-1.arm64.rpm"
      - $"{output}/Project-1.0.0-1.x64.deb"
      - $"{output}/Project-1.0.0-1.x64.rpm"

