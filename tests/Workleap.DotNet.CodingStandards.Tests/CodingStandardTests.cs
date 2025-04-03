using System.IO.Compression;
using System.Reflection.PortableExecutable;
using Workleap.DotNet.CodingStandards.Tests.Helpers;
using Xunit.Abstractions;

namespace Workleap.DotNet.CodingStandards.Tests;

public sealed class CodingStandardTests(PackageFixture fixture, ITestOutputHelper testOutputHelper) : IClassFixture<PackageFixture>
{
    [Fact]
    public async Task BannedSymbolsAreReported()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);
        project.AddCsprojFile();
        project.AddFile("sample.cs", "_ = System.DateTime.Now;");
        var data = await project.BuildAndGetOutput();
        Assert.True(data.HasWarning("RS0030"));
    }

    [Fact]
    public async Task WarningsAsErrorOnGitHubActions()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);
        project.AddCsprojFile();
        project.AddFile("sample.cs", "_ = System.DateTime.Now;");
        var data = await project.BuildAndGetOutput(["/p:GITHUB_ACTIONS=true"]);
        Assert.True(data.HasError("RS0030"));
    }

    [Fact]
    public async Task MSBuildWarningsAsErrorOnDefaultConfiguration()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);
        project.AddCsprojFile(packageReferences: new Dictionary<string, string> { { "Azure.Identity", "1.10.4" } });
        project.AddFile("sample.cs", """
             namespace sample;
             public static class Sample
             {
                 public static void Main(string[] args)
                 {
                 }
             }
             """);
        var data = await project.BuildAndGetOutput();
        Assert.True(data.HasWarning("NU1902"));
    }

    [Fact]
    public async Task MSBuildWarningsAsErrorOnDebugConfiguration()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);
        project.AddCsprojFile(packageReferences: new Dictionary<string, string> { { "Azure.Identity", "1.10.4" } });
        project.AddFile("sample.cs", """
             namespace sample;
             public static class Sample
             {
                 public static void Main(string[] args)
                 {
                 }
             }
             """);
        var data = await project.BuildAndGetOutput(["--configuration", "Debug"]);
        Assert.True(data.HasWarning("NU1902"));
    }

    [Fact]
    public async Task MSBuildWarningsAsErrorOnReleaseConfiguration()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);
        project.AddCsprojFile(packageReferences: new Dictionary<string, string> { { "Azure.Identity", "1.10.4" } });
        project.AddFile("sample.cs", """
             namespace sample;
             public static class Sample
             {
                 public static void Main(string[] args)
                 {
                 }
             }
             """);
        var data = await project.BuildAndGetOutput(["--configuration", "Release"]);
        Assert.True(data.HasError("NU1902"));
    }

    [Fact]
    public async Task NamingConvention_Invalid()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);
        project.AddCsprojFile();
        project.AddFile("sample.cs", """
            _ = "";

            class Sample
            {
                private readonly int field;

                public Sample(int a) => field = a;

                public int A() => field;
            }
            """);
        var data = await project.BuildAndGetOutput(["--configuration", "Release"]);
        Assert.True(data.HasError("IDE1006"));
    }

    [Fact]
    public async Task NamingConvention_Valid()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);
        project.AddCsprojFile();
        project.AddFile("sample.cs", """
            _ = "";

            class Sample
            {
                private int _field;
            }
            """);
        var data = await project.BuildAndGetOutput(["--configuration", "Release"]);
        Assert.False(data.HasError("IDE1006"));
        Assert.False(data.HasWarning("IDE1006"));
    }

    [Fact]
    public async Task ReportVulnerablePackage_Release_ShouldReportError()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);
        project.AddCsprojFile(packageReferences: new Dictionary<string, string> { { "System.Text.Json", "8.0.1" } });
        project.AddFile("sample.cs", """
            Console.WriteLine();
            """);
        var data = await project.BuildAndGetOutput(["--configuration", "Release"]);
        Assert.True(data.HasError("NU1903"));
    }

    [Fact]
    public async Task ReportVulnerablePackage_Debug_ShouldReportWarning()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);
        project.AddCsprojFile(packageReferences: new Dictionary<string, string> { { "System.Text.Json", "8.0.1" } });
        project.AddFile("sample.cs", """
            Console.WriteLine();
            """);
        var data = await project.BuildAndGetOutput(["--configuration", "Debug"]);
        Assert.False(data.HasError("NU1903"));
        Assert.True(data.HasWarning("NU1903"));
    }

    [Fact]
    public async Task ReportVulnerablePackage_DisabledWarningOnPackage()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);
        project.AddFile("test.csproj", $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <OutputType>exe</OutputType>
                    <TargetFramework>net$(NETCoreAppMaximumVersion)</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <ErrorLog>{ProjectBuilder.SarifFileName},version=2.1</ErrorLog>
                  </PropertyGroup>
                  
                  <ItemGroup>
                    <PackageReference Include="Workleap.DotNet.CodingStandards" Version="*" />
                    <PackageReference Include="System.Text.Json" Version="8.0.1" NoWarn="NU1903" />
                  </ItemGroup>
                </Project>
                """);

        project.AddFile("sample.cs", """
            Console.WriteLine();
            """);
        var data = await project.BuildAndGetOutput(["--configuration", "Release"]);
        Assert.False(data.HasError("NU1903"));
        Assert.False(data.HasWarning("NU1903"));
    }

    [Fact]
    public async Task PdbShouldBeEmbedded_Dotnet_Build()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);
        project.AddFile("test.csproj", $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <ErrorLog>{ProjectBuilder.SarifFileName},version=2.1</ErrorLog>
                    <RootNamespace>Foo</RootNamespace>
                  </PropertyGroup>
                  
                  <ItemGroup>
                    <PackageReference Include="Workleap.DotNet.CodingStandards" Version="*" />
                  </ItemGroup>
                </Project>
                """);

        project.AddFile("Sample.cs", """
            namespace Foo;
            public static class Sample { }
            """);
        var data = await project.BuildAndGetOutput(["--configuration", "Release"]);

        var outputFiles = Directory.GetFiles(Path.Combine(project.RootFolder, "bin", "Release", "net8.0"));
        await AssertPdbIsEmbedded(outputFiles);
    }

    [Fact]
    public async Task PdbShouldBeEmbedded_Dotnet_Pack()
    {
        using var project = new ProjectBuilder(fixture, testOutputHelper);
        project.AddFile("test.csproj", $"""
                <Project Sdk="Microsoft.NET.Sdk">
                  <PropertyGroup>
                    <TargetFramework>net8.0</TargetFramework>
                    <ImplicitUsings>enable</ImplicitUsings>
                    <Nullable>enable</Nullable>
                    <ErrorLog>{ProjectBuilder.SarifFileName},version=2.1</ErrorLog>
                    <RootNamespace>Foo</RootNamespace>
                  </PropertyGroup>
                  
                  <ItemGroup>
                    <PackageReference Include="Workleap.DotNet.CodingStandards" Version="*" />
                  </ItemGroup>
                </Project>
                """);

        project.AddFile("Sample.cs", """
            namespace Foo;
            public static class Sample { }
            """);
        var data = await project.PackAndGetOutput(["--configuration", "Release"]);

        var extractedPath = Path.Combine(project.RootFolder, "extracted");
        var files = Directory.GetFiles(Path.Combine(project.RootFolder, "bin", "Release"));
        Assert.Single(files); // Only the .nupkg should be generated
        var nupkg = files.Single(f => f.EndsWith(".nupkg", StringComparison.OrdinalIgnoreCase));
        ZipFile.ExtractToDirectory(nupkg, extractedPath);

        var outputFiles = Directory.GetFiles(extractedPath, "*", SearchOption.AllDirectories);
        await AssertPdbIsEmbedded(outputFiles);
    }

    private static async Task AssertPdbIsEmbedded(string[] outputFiles)
    {
        Assert.DoesNotContain(outputFiles, f => f.EndsWith(".pdb", StringComparison.OrdinalIgnoreCase));
        var dllPath = outputFiles.Single(f => f.EndsWith(".dll", StringComparison.OrdinalIgnoreCase));
        await using var stream = File.OpenRead(dllPath);
        var peReader = new PEReader(stream);
        var debug = peReader.ReadDebugDirectory();
        Assert.Contains(debug, entry => entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb);
    }
}
