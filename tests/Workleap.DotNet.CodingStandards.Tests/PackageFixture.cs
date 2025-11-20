using System.Text;
using CliWrap;
using Workleap.DotNet.CodingStandards.Tests.Helpers;

namespace Workleap.DotNet.CodingStandards.Tests;

public sealed class PackageFixture : IAsyncLifetime
{
    private readonly TemporaryDirectory _packageDirectory = TemporaryDirectory.Create();

    public string PackageDirectory => this._packageDirectory.FullPath;

    public async Task InitializeAsync()
    {
        var projectPath = Path.Combine(PathHelpers.GetRootDirectory(), "Workleap.DotNet.CodingStandards.csproj");
        string[] args = ["pack", projectPath, "-p:NuspecProperties=version=999.9.9", "--output", this._packageDirectory.FullPath];
        var output = new StringBuilder();
        var result = await Cli.Wrap("dotnet")
             .WithArguments(args)
             .WithStandardOutputPipe(PipeTarget.ToStringBuilder(output))
             .WithStandardErrorPipe(PipeTarget.ToStringBuilder(output))
             .WithValidation(CommandResultValidation.None)
             .ExecuteAsync();

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException("Error while creating the NuGet package:\n" + output);
        }
    }

    public Task DisposeAsync()
    {
        this._packageDirectory.Dispose();
        return Task.CompletedTask;
    }
}
