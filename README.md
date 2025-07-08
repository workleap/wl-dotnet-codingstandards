# Workleap.DotNet.CodingStandards

[![nuget](https://img.shields.io/nuget/v/Workleap.DotNet.CodingStandards.svg?logo=nuget)](https://www.nuget.org/packages/Workleap.DotNet.CodingStandards/)
[![build](https://img.shields.io/github/actions/workflow/status/workleap/wl-dotnet-codingstandards/publish.yml?logo=github&branch=main)](https://github.com/workleap/wl-dotnet-codingstandards/actions/workflows/publish.yml)

This package provides a set of **programming standards for .NET projects**, including rules for **style**, **quality**, **security**, and **performance**. It relies on [built-in .NET analyzers](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview) that are shipped with the .NET SDK.

During development, the package will provide warnings :warning: when standards are not met. For a production build (`Release` configuration), these warnings will be treated as errors :x: and will make the build fail.

> [!WARNING]
> This package only works with [SDK-style projects](https://learn.microsoft.com/en-us/dotnet/core/project-sdk/overview). Older ASP.NET projects (non-Core) are not supported. The same applies to older desktop applications unless they have migrated to the new `Microsoft.NET.Sdk.WindowsDesktop` SDK.

## Usage

Install the NuGet package in your project:

```
dotnet add package Workleap.DotNet.CodingStandards
```

If you are using [StyleCop.Analyzers](https://www.nuget.org/packages/StyleCop.Analyzers), you can safely remove it from your project. You can also remove the relevant parts of your `.editorconfig` files which contain analysis rules configuration.

If you also have a `Directory.Build.props` file in your solution, you can remove properties that are [already set by this package](src/buildTransitive/Workleap.DotNet.CodingStandards.props).

## What's included

- Code style and formatting options, including indentation, line wrapping, encoding, new lines preferences, and more.
- .NET and C# coding conventions, including naming, preferences for types, modifiers, code blocks, expressions, pattern matching, using directives, parentheses, and much more.
- Project properties, including deterministic build, strict mode, continuous integration build detection, [faster package restoration](https://learn.microsoft.com/en-us/nuget/reference/msbuild-targets#restoring-with-msbuild-static-graph-evaluation), and [faster builds on Visual Studio](https://devblogs.microsoft.com/visualstudio/vs-toolbox-accelerate-your-builds-of-sdk-style-net-projects/), and more.
- .NET analysis rules configuration, including style rules (`IDExxxx`) and code analysis rules (`CAxxxx`). These rules have been manually configured to provide a good balance between quality, performance, security, and build time.
- Banned APIs, such as `DateTime.Now` and `DateTimeOffset.Now` (use their UTC counterparts instead).
- Opt-in banning `Newtonsoft.Json`:

    ```xml
    <PropertyGroup>
        <BanNewtonsoftJsonSymbols>true</BanNewtonsoftJsonSymbols>
    </PropertyGroup>
    ```

## What's NOT included

- Enabling a specific or latest C# language version.
- Enabling nullable reference types by default.
- Enabling implicit using directives by default.
- Enforcing a specific target framework.
- Code styles for files that are not C# source files (e.g. `.csproj`, `.json`, etc.).

We believe that most of these settings should be set on a per-project basis, and decided by the project's maintainers.

For files other than C# source files, it is technically impossible for this package to enforce code styles, so you will have to configure their style using your own `.editorconfig` files.

## How it works

This package comes with a set of `.props` and `.targets` files that are automatically imported into your project when you install the package. These files contain MSBuild properties and targets that configure the build process and the code analysis rules.

It also includes multiple `.editorconfig` files that are automatically imported by Roslyn during the build process.

## Facilitating package adoption

From experience, installing the package will result in numerous warnings, the majority of which are related to code formatting. We recommend addressing these in several steps to minimize the impact on code review, using the [dotnet format](https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-format) utility, which is included in the .NET SDK.

Firstly, warnings related to code formatting **can be automatically corrected**. This includes: indentation, unnecessary spaces, empty lines, braces, file-scoped namespaces, unnecessary using directives, etc. Here is the command to execute:

```
dotnet format <path_to_your_solution_or_project> --diagnostics IDE0001 IDE0004 IDE0005 IDE0007 IDE0009 IDE0011 IDE0055 IDE0161 IDE2000 IDE2001 IDE2002 IDE2003 IDE2004 IDE2005 IDE2006 --verbosity diagnostic
```

Once the pull request is merged, one can then attempt to automatically correct the remaining warnings. Without the noise of formatting warnings, it will be easier to focus on them:

```
dotnet format <path_to_your_solution_or_project> --severity warn --verbosity diagnostic
```

You can also modify the command to specify the IDs of the analysis rules you wish to automatically correct (if a fix is available). In this way, you avoid manual work, and breaking the correction into several pull requests will increase developers' confidence in the process of adopting new standards.

All rules included in this package **can be disabled or modified** in an `.editorconfig` file that you can add to your project. You can also **disable a rule for a specific line or block of code** using `#pragma` directives or `[SuppressMessage]` attributes. Learn more about [configuring code analysis rules](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/suppress-warnings). Remember to always justify why a rule is disabled or modified. Here're some examples:

- Disable specific diagnostics in the code

    ````c#
    #pragma warning disable CA2200 // Rethrow to preserve stack details
    throw e;
    #pragma warning restore CA2200
    ````

- Disable specific diagnostics for a method

    ````c#
    [System.Diagnostics.CodeAnalysis.SuppressMessage("Usage", "CA2200:Rethrow to preserve stack details", Justification = "Not production code.")]
    void Sample()
    {
    }
    ````

- Disable a rule using an .editorconfig

    ````editorconfig
    # Disable CA2200 globally
    [*.cs]
    dotnet_diagnostic.CA2200.severity = none

    # Disable CA2200 for specific files
    [tests/**/*.cs]
    dotnet_diagnostic.CA2200.severity = none
    ````

- Disable [NuGet auditing](https://learn.microsoft.com/en-us/nuget/concepts/auditing-packages?WT.mc_id=DT-MVP-5003978) for a specific package

    ````xml
    <PackageReference Include="System.Formats.Asn1" Version="8.0.0" NoWarn="NU1903" />
    ````

> [!WARNING]
> Remember that this should be a temporary solution to help adopting the package

Finally, the warnings breaks the CI because they are treated as errors when building the `Release` configuration. You can disable this behavior by setting the following MSBuild property in the `.csproj` file or in the `Directory.Build.props` file

    ````xml
    <Project>
        <PropertyGroup>
            <TreatWarningsAsErrors>false</TreatWarningsAsErrors>
            <MSBuildTreatWarningsAsErrors>false</MSBuildTreatWarningsAsErrors>
        </PropertyGroup>
    </Project>
    ````

## References

- [Configuration files for code analysis rules: Global analyzer options](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/configuration-files#global-analyzerconfig) (Microsoft documentation).
- [Sharing coding style and Roslyn analyzers across projects](https://www.meziantou.net/sharing-coding-style-and-roslyn-analyzers-across-projects.htm) (blog post written by [Gérald Barré](https://github.com/meziantou)).
- [Microsoft.CodeAnalysis.NetAnalyzers package content](https://nuget.info/packages/Microsoft.CodeAnalysis.NetAnalyzers/8.0.0) (NuGet.info).

## Build and release process

This project uses [GitVersion](https://learn.microsoft.com/en-us/dotnet/fundamentals/code-analysis/overview) as a .NET tool during the build process to automatically compute the version number for preview packages that are automatically published on the main branch and pull requests.

To publish a new public (non-preview) version, simply create a new release on GitHub with a `x.y.z` tag.

## License

Copyright © 2024, Workleap. This code is licensed under the Apache License, Version 2.0. You may obtain a copy of this license at https://github.com/workleap/gsoft-license/blob/master/LICENSE.
