using System.Text;
using System.Xml.Linq;
using Xunit.Abstractions;
using System.Text.Json;
using CliWrap;
using Xunit.Sdk;

namespace Workleap.DotNet.CodingStandards.Tests.Helpers;

internal sealed class ProjectBuilder : IDisposable
{
    public const string SarifFileName = "BuildOutput.sarif";

    private readonly TemporaryDirectory _directory;
    private readonly ITestOutputHelper _testOutputHelper;

    public string RootFolder => this._directory.FullPath;

    public ProjectBuilder(PackageFixture fixture, ITestOutputHelper testOutputHelper)
    {
        this._testOutputHelper = testOutputHelper;

        this._directory = TemporaryDirectory.Create();
        this._directory.CreateTextFile("NuGet.config", $"""
                <configuration>
                  <config>
                    <add key="globalPackagesFolder" value="{fixture.PackageDirectory}/packages" />
                  </config>
                  <packageSources>
                    <clear />
                    <add key="nuget.org" value="https://api.nuget.org/v3/index.json" />
                    <add key="TestSource" value="{fixture.PackageDirectory}" />
                  </packageSources>
                  <packageSourceMapping>
                    <packageSource key="nuget.org">
                        <package pattern="*" />
                    </packageSource>
                    <packageSource key="TestSource">
                        <package pattern="Workleap.DotNet.CodingStandards" />
                    </packageSource>
                  </packageSourceMapping>
                </configuration>
                """);

        File.Copy(Path.Combine(PathHelpers.GetRootDirectory(), "global.json"), this._directory.GetPath("global.json"));
    }

    public void AddFile(string relativePath, string content)
    {
        File.WriteAllText(this._directory.GetPath(relativePath), content);
    }

    public void AddCsprojFile(Dictionary<string, string>? properties = null, Dictionary<string, string>? packageReferences = null)
    {
        var propertyElement = new XElement("PropertyGroup");
        if (properties != null)
        {
            foreach (var prop in properties)
            {
                propertyElement.Add(new XElement(prop.Key, prop.Value));
            }
        }

        var referencesElement = new XElement("ItemGroup");
        if (packageReferences != null)
        {
            foreach (var reference in packageReferences)
            {
                var packageReference = new XElement("PackageReference");
                packageReference.SetAttributeValue("Include", reference.Key);
                packageReference.SetAttributeValue("Version", reference.Value);

                referencesElement.Add(packageReference);
            }
        }

        var content = $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>exe</OutputType>
                    <TargetFramework>net$(NETCoreAppMaximumVersion)</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <ErrorLog>{SarifFileName},version=2.1</ErrorLog>
                  </PropertyGroup>
                  {propertyElement}

                  <ItemGroup>
                    <PackageReference Include="Workleap.DotNet.CodingStandards" Version="*" />
                  </ItemGroup>
                  {referencesElement}
                </Project>
                """;

        File.WriteAllText(this._directory.GetPath("test.csproj"), content);
    }

    public Task<SarifFile> BuildAndGetOutput(string[]? buildArguments = null)
    {
        return this.ExecuteDotnetCommandAndGetOutput("build", buildArguments);
    }

    public Task<SarifFile> PackAndGetOutput(string[]? buildArguments = null)
    {
        return this.ExecuteDotnetCommandAndGetOutput("pack", buildArguments);
    }

    private async Task<SarifFile> ExecuteDotnetCommandAndGetOutput(string command, string[]? buildArguments = null)
    {
        var result = await Cli.Wrap("dotnet")
            .WithWorkingDirectory(this._directory.FullPath)
            .WithArguments([command, .. (buildArguments ?? [])])
            .WithEnvironmentVariables(env => env.Set("CI", null).Set("GITHUB_ACTIONS", null))
            .WithStandardOutputPipe(PipeTarget.ToDelegate(this._testOutputHelper.WriteLine))
            .WithStandardErrorPipe(PipeTarget.ToDelegate(this._testOutputHelper.WriteLine))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        this._testOutputHelper.WriteLine("Process exit code: " + result.ExitCode);

        var bytes = await File.ReadAllBytesAsync(this._directory.GetPath(SarifFileName));
        var sarif = JsonSerializer.Deserialize<SarifFile>(bytes) ?? throw new InvalidOperationException("The sarif file is invalid");

        this.AppendAdditionalResult(sarif);

        this._testOutputHelper.WriteLine("Sarif result:\n" + string.Join("\n", sarif.AllResults().Select(r => r.ToString())));
        return sarif;
    }

    public async Task<(int ExitCode, string Output)> ExecuteDotnetCommand(string[] arguments)
    {
        var stdOut = new StringBuilder();
        var result = await Cli.Wrap("dotnet")
            .WithWorkingDirectory(this._directory.FullPath)
            .WithArguments(arguments)
            .WithEnvironmentVariables(env => env.Set("CI", null).Set("GITHUB_ACTIONS", null))
            .WithStandardOutputPipe(PipeTarget.Merge(
                PipeTarget.ToDelegate(this._testOutputHelper.WriteLine),
                PipeTarget.ToStringBuilder(stdOut)))
            .WithStandardErrorPipe(PipeTarget.Merge(
                PipeTarget.ToDelegate(this._testOutputHelper.WriteLine),
                PipeTarget.ToStringBuilder(stdOut)))
            .WithValidation(CommandResultValidation.None)
            .ExecuteAsync();

        this._testOutputHelper.WriteLine("Process exit code: " + result.ExitCode);
        return (result.ExitCode, stdOut.ToString());
    }

    public void Dispose() => this._directory.Dispose();

    private void AppendAdditionalResult(SarifFile sarifFile)
    {
        if (this._testOutputHelper is not TestOutputHelper testOutputHelper || sarifFile.Runs == null)
        {
            return;
        }

        var outputLines = testOutputHelper.Output.Split(Environment.NewLine);
        var customRunResults = new List<SarifFileRunResult>();

        // These rules (for nuget package vulnerability) are not parsed in the sarif file automatically
        // See https://learn.microsoft.com/en-us/nuget/reference/errors-and-warnings/nu1901-nu1904
        var scannedRules = new List<string> { "NU1901", "NU1902", "NU1903", "NU1904" }
            .ToDictionary(x => x, x => $"{x}:");

        foreach (var outputLine in outputLines)
        {
            foreach (var scannedRule in scannedRules)
            {
                var scannedRuleIndex = outputLine.IndexOf(scannedRule.Value, StringComparison.OrdinalIgnoreCase);
                if (scannedRuleIndex == -1)
                {
                    continue;
                }

                var previousColonIndex = outputLine.LastIndexOf(':', scannedRuleIndex);
                var ruleLevel = outputLine.Substring(previousColonIndex + 1, scannedRuleIndex - previousColonIndex - 1).Trim();

                var message = outputLine[(scannedRuleIndex + scannedRule.Value.Length + 1)..];
                customRunResults.Add(new SarifFileRunResult { Level = ruleLevel, RuleId = scannedRule.Key, Message = new SarifFileRunResultMessage { Text = message } });
            }
        }

        var distinctRules = customRunResults
            .DistinctBy(x => new { x.RuleId, x.Level })
            .ToArray();

        sarifFile.Runs = sarifFile.Runs.Append(new SarifFileRun { Results = distinctRules }).ToArray();
    }
}
