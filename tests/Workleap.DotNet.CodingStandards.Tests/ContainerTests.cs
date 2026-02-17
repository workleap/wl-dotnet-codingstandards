using System.Formats.Tar;
using System.Text.Json;
using Workleap.DotNet.CodingStandards.Tests.Helpers;
using Xunit.Abstractions;

namespace Workleap.DotNet.CodingStandards.Tests;

public sealed class ContainerTests(PackageFixture fixture, ITestOutputHelper testOutputHelper) : IClassFixture<PackageFixture>
{
    [Fact]
    public async Task PublishContainer_WithContainerUseNativeCommand_EntrypointUsesNativeExecutable()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);

        var archivePath = Path.Combine(project.RootFolder, "container");

        project.AddFile("test.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net$(NETCoreAppMaximumVersion)</TargetFramework>
                <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <ContainerArchiveOutputPath>{archivePath}</ContainerArchiveOutputPath>
                <ErrorLog>{ProjectBuilder.SarifFileName},version=2.1</ErrorLog>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Workleap.DotNet.CodingStandards" Version="*" />
              </ItemGroup>
            </Project>
            """);

        project.AddFile("Program.cs",
            """
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();
            app.MapGet("/health", () => "ok");
            app.Run();
            """ + "\n");

        var (publishExitCode, _) = await project.ExecuteDotnetCommand(["publish", "/t:PublishContainer"]);
        Assert.Equal(0, publishExitCode);

        var entrypoint = await GetEntrypointFromArchive(archivePath);
        testOutputHelper.WriteLine("Entrypoint: " + JsonSerializer.Serialize(entrypoint));

        Assert.Equal(["/app/test"], entrypoint);
    }

    [Fact]
    public async Task PublishContainer_WithContainerUseNativeCommandDisabled_EntrypointUsesDotnet()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);

        var archivePath = Path.Combine(project.RootFolder, "container");

        project.AddFile("test.csproj", $"""
            <Project Sdk="Microsoft.NET.Sdk.Web">
              <PropertyGroup>
                <TargetFramework>net$(NETCoreAppMaximumVersion)</TargetFramework>
                <RuntimeIdentifier>linux-x64</RuntimeIdentifier>
                <ImplicitUsings>enable</ImplicitUsings>
                <Nullable>enable</Nullable>
                <ContainerUseNativeCommand>false</ContainerUseNativeCommand>
                <ContainerArchiveOutputPath>{archivePath}</ContainerArchiveOutputPath>
                <ErrorLog>{ProjectBuilder.SarifFileName},version=2.1</ErrorLog>
              </PropertyGroup>

              <ItemGroup>
                <PackageReference Include="Workleap.DotNet.CodingStandards" Version="*" />
              </ItemGroup>
            </Project>
            """);

        project.AddFile("Program.cs",
            """
            var builder = WebApplication.CreateBuilder(args);
            var app = builder.Build();
            app.MapGet("/health", () => "ok");
            app.Run();
            """ + "\n");

        var (publishExitCode, _) = await project.ExecuteDotnetCommand(["publish", "/t:PublishContainer"]);
        Assert.Equal(0, publishExitCode);

        var entrypoint = await GetEntrypointFromArchive(archivePath);
        testOutputHelper.WriteLine("Entrypoint: " + JsonSerializer.Serialize(entrypoint));

        Assert.Equal(["dotnet", "/app/test.dll"], entrypoint);
    }

    private static async Task<string[]> GetEntrypointFromArchive(string archiveDirectory)
    {
        var tarGzFile = Directory.GetFiles(archiveDirectory, "*.tar.gz").Single();

        // Read only the JSON metadata entries from the archive (stop before layer blobs)
        var jsonFiles = new Dictionary<string, string>(StringComparer.Ordinal);
        await using var fileStream = File.OpenRead(tarGzFile);
        await using var tarReader = new TarReader(fileStream);

        while (await tarReader.GetNextEntryAsync() is { } entry)
        {
            if (entry.DataStream == null || !entry.Name.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            using var reader = new StreamReader(entry.DataStream, leaveOpen: true);
            jsonFiles[entry.Name] = await reader.ReadToEndAsync();
        }

        // Parse Docker manifest -> config
        using var manifest = JsonDocument.Parse(jsonFiles["manifest.json"]);
        var configFileName = manifest.RootElement[0].GetProperty("Config").GetString()!;

        using var config = JsonDocument.Parse(jsonFiles[configFileName]);

        return config.RootElement
            .GetProperty("config")
            .GetProperty("Entrypoint")
            .EnumerateArray()
            .Select(e => e.GetString()!)
            .ToArray();
    }
}
