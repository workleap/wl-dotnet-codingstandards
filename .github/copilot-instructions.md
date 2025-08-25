# Workleap.DotNet.CodingStandards

**ALWAYS follow these instructions first and only fallback to additional search and context gathering if the information in the instructions is incomplete or found to be in error.**

Workleap.DotNet.CodingStandards is a NuGet package that provides coding standards, analyzers, and .editorconfig files for .NET projects. It packages MSBuild properties/targets files and generated analyzer configuration files.

## Working Effectively

### Bootstrap, Build, and Test the Repository
**CRITICAL TIMING:** Build takes 60+ seconds. Tests take 32+ seconds. ConfigurationFilesGenerator takes 15+ seconds. NEVER CANCEL any of these operations.

1. **Install required dependencies:**
   ```bash
   # Install .NET 9.0 SDK (required by global.json)
   wget https://dotnetcli.azureedge.net/dotnet/Sdk/9.0.304/dotnet-sdk-9.0.304-linux-x64.tar.gz
   sudo mkdir -p /usr/share/dotnet9
   sudo tar zxf dotnet-sdk-9.0.304-linux-x64.tar.gz -C /usr/share/dotnet9
   export PATH="/usr/share/dotnet9:$PATH"
   export DOTNET_ROOT="/usr/share/dotnet9"
   
   # Install .NET 8.0 runtime (required for tests)
   wget https://dotnetcli.azureedge.net/dotnet/Runtime/8.0.12/dotnet-runtime-8.0.12-linux-x64.tar.gz
   sudo tar zxf dotnet-runtime-8.0.12-linux-x64.tar.gz -C /usr/share/dotnet9
   
   # Install Mono and NuGet.exe (required for packaging and tests)
   sudo apt-get install -y mono-complete
   wget https://dist.nuget.org/win-x86-commandline/latest/nuget.exe
   sudo mv nuget.exe /usr/local/bin/
   echo '#!/bin/bash
   exec mono /usr/local/bin/nuget.exe "$@"' | sudo tee /usr/local/bin/nuget
   sudo chmod +x /usr/local/bin/nuget
   
   # Install PowerShell (if not available)
   # PowerShell is already available in GitHub Actions
   ```

2. **Restore tools and build components:**
   ```bash
   dotnet tool restore  # Installs GitVersion - takes 5 seconds
   
   # Generate analyzer configuration files - takes 15 seconds. NEVER CANCEL.
   dotnet run --project=tools/ConfigurationFilesGenerator/ConfigurationFilesGenerator.csproj --configuration Release --verbosity normal
   ```

3. **Run tests - takes 35 seconds. NEVER CANCEL. Set timeout to 60+ minutes:**
   ```bash
   dotnet test --configuration Release --logger "console;verbosity=normal"
   ```

4. **Package the NuGet package - takes 2 seconds:**
   ```bash
   # Get version (may fail on feature branches - use manual version instead)
   VERSION=$(dotnet dotnet-gitversion /output json /showvariable SemVer 2>/dev/null || echo "1.1.25-dev")
   
   # Pack the package
   nuget pack Workleap.DotNet.CodingStandards.nuspec -OutputDirectory .output -Version $VERSION -ForceEnglishOutput
   ```

5. **Complete build script (mimics Build.ps1):**
   ```bash
   # NEVER CANCEL: Full build takes 60+ seconds
   export PATH="/usr/share/dotnet9:/usr/local/bin:$PATH"
   export DOTNET_ROOT="/usr/share/dotnet9"
   
   dotnet tool restore
   dotnet run --project=tools/ConfigurationFilesGenerator/ConfigurationFilesGenerator.csproj --configuration Release
   VERSION=$(dotnet dotnet-gitversion /output json /showvariable SemVer 2>/dev/null || echo "1.1.25-dev")
   nuget pack Workleap.DotNet.CodingStandards.nuspec -OutputDirectory .output -Version $VERSION -ForceEnglishOutput
   dotnet test --configuration Release --logger "console;verbosity=normal"
   ```

### Environment Setup Commands
These are the exact commands for setting up the development environment:

```bash
# Set environment variables for all operations
export PATH="/usr/share/dotnet9:/usr/local/bin:$PATH"
export DOTNET_ROOT="/usr/share/dotnet9"

# Verify installation
dotnet --version  # Should show 9.0.304
nuget  # Should show NuGet command line help
pwsh --version  # Should show PowerShell 7.x
```

## Validation

### Manual Testing Scenarios
**ALWAYS run these validation steps after making changes:**

1. **Verify ConfigurationFilesGenerator works:**
   ```bash
   # Should complete in ~15 seconds and show analyzer packages being processed
   dotnet run --project=tools/ConfigurationFilesGenerator/ConfigurationFilesGenerator.csproj --configuration Release
   
   # Verify generated files exist
   ls -la src/files/analyzers/
   # Should show: Analyzer.*.editorconfig files and manual_rules.editorconfig
   ```

2. **Verify package contents:**
   ```bash
   # After packaging, check package contents
   unzip -l .output/Workleap.DotNet.CodingStandards.*.nupkg
   # Should contain: build/, buildTransitive/, buildMultiTargeting/, files/ directories
   ```

3. **Test package installation (end-to-end test):**
   ```bash
   # Create test project
   mkdir test-project && cd test-project
   dotnet new console
   dotnet add package Workleap.DotNet.CodingStandards --source ../../../.output --prerelease
   dotnet build
   # Should build successfully with coding standards applied
   ```

### CI Validation Commands
Always run these before committing changes:

```bash
# These commands mirror what CI does and must pass
dotnet format --verify-no-changes  # Verify code formatting
dotnet build --configuration Release  # Verify build succeeds
dotnet test --configuration Release   # Verify all tests pass
```

## Common Tasks and Troubleshooting

### Working with the ConfigurationFilesGenerator
- **Purpose:** Downloads NuGet analyzer packages and generates corresponding .editorconfig files
- **Input:** Hardcoded list of analyzer packages in Program.cs
- **Output:** src/files/analyzers/Analyzer.*.editorconfig files
- **Timing:** Takes 15+ seconds to download and process all analyzer packages

### Key Repository Locations
```
src/
├── build/                              # MSBuild .props/.targets files
├── buildTransitive/                    # Transitive MSBuild files
├── buildMultiTargeting/                # Multi-targeting MSBuild files  
└── files/
    ├── *.editorconfig                  # Static configuration files
    ├── BannedSymbols.txt               # Banned API definitions
    └── analyzers/                      # Generated analyzer configs
tools/
└── ConfigurationFilesGenerator/        # Tool to generate analyzer configs
tests/
└── Workleap.DotNet.CodingStandards.Tests/  # xUnit test project
```

### Dependencies and Requirements
- **.NET 9.0 SDK:** Required for building (specified in global.json)
- **.NET 8.0 Runtime:** Required for running tests (test project targets net8.0)
- **PowerShell:** Required for Build.ps1 script
- **Mono + NuGet.exe:** Required for packaging and test fixtures
- **GitVersion:** .NET tool for automatic versioning

### Timing Expectations
**CRITICAL: Never cancel these operations. Set timeouts appropriately:**
- Tool restore: ~5 seconds
- ConfigurationFilesGenerator: ~15 seconds
- Full build: ~20 seconds  
- Tests: ~32 seconds (14 test cases)
- Full CI pipeline: ~60 seconds total

### Build Failures and Solutions
- **"SDK not found" error:** Ensure .NET 9.0 SDK is installed and in PATH
- **"nuget command not found":** Install mono and create nuget wrapper script
- **Test failures with runtime errors:** Ensure .NET 8.0 runtime is available
- **GitVersion errors on feature branches:** Use manual version instead

### Package Structure
The NuGet package contains:
- **MSBuild integration:** Automatic import of .props/.targets files
- **EditorConfig files:** Code style and analyzer rules configuration
- **Banned symbols:** API usage restrictions
- **Analyzer dependencies:** Meziantou.Analyzer, StyleCop.Analyzers, Microsoft analyzers

### Testing Strategy
Tests use ProjectBuilder helper to:
1. Create temporary test projects with NuGet.config
2. Add the coding standards package
3. Build projects and capture SARIF output
4. Validate expected warnings/errors are present

## References
- Build script: Build.ps1 (PowerShell)
- CI workflow: .github/workflows/ci.yml  
- Package definition: Workleap.DotNet.CodingStandards.nuspec
- Test project: tests/Workleap.DotNet.CodingStandards.Tests/