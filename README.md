# Shipyard [![Build & Tests](https://github.com/CorruptComputer/Shipyard/actions/workflows/build.yml/badge.svg?branch=develop)](https://github.com/CorruptComputer/Shipyard/actions/workflows/build.yml) [![NuGet Version](https://img.shields.io/nuget/vpre/shipyard.svg)](https://www.nuget.org/packages/shipyard)

Project to build and package .NET applications on Linux.

Currently implemented:
- RPM Packages
- DEB Packages

Future Goals:
- tarball
- AppImage
- Flatpaks
- Snaps

## Installing

Right now, the easiest way to install and run this tool is using `dotnet tool install --global shipyard`.
You'll also need to install `rpmbuild` and `dpkg` from your systems package manager, depending on which package type you'd like to build.

## Running

You'll need to create a json configuration file, below is a sample based on Shipyards own configuration:
```json
{
  "$schema": "https://raw.githubusercontent.com/CorruptComputer/Shipyard/refs/heads/release/shipyard.schema.json",
  "executable": "shipyard",
  "version": "0.0.4",
  "author": "Nickolas Gupton",
  "license": "MIT",
  "repositoryUrl": "https://github.com/CorruptComputer/Shipyard",
  "dotnet": {
    "projectFile": "./Shipyard.csproj",
    "framework": "net10.0",
    "configuration": "Release",
    "runtimes": [
      "linux-x64"
    ],
    "publish": []
  },
  "formats": [
    {
      "format": "rpm",
      "packageName": "Shipyard",
      "release": 1,
      "provides": [],
      "dependsOn": [
        "dotnet-runtime-10.0",
        "dotnet-sdk-10.0",
        "rpm-build",
        "dpkg"
      ]
    },
    {
      "format": "deb",
      "packageName": "Shipyard",
      "section": "utils",
      "priority": "optional",
      "maintainer": "Nickolas Gupton <email@example.com>",
      "depends": [
        "dotnet-runtime-10.0",
        "dotnet-sdk-10.0",
        "rpm",
        "dpkg"
      ]
    }
  ]
}
```

Once this is created and shipyard is installed, you should be able to just run `shipyard --config path/to/shipyard.json --output path/to/output/dir`.

## Contributing

Since this is still very early in this project, I will probably not accept any contributions.
Once things have stabilized and I'm able to achieve what I want from this, contributions will be more than welcome.