using System.Diagnostics;
using CliWrap;
using Workleap.DotNet.CodingStandards.Tests.Helpers;

namespace Workleap.DotNet.CodingStandards.Tests;

public sealed class PackageFixture : IAsyncLifetime
{
    private readonly TemporaryDirectory _packageDirectory = TemporaryDirectory.Create();

    public string PackageDirectory => this._packageDirectory.FullPath;

    public async Task InitializeAsync()
    {
        var nuspecPath = Path.Combine(PathHelpers.GetRootDirectory(), "Workleap.DotNet.CodingStandards.nuspec");
        string[] args = ["pack", nuspecPath, "-ForceEnglishOutput", "-Version", "999.9.9", "-OutputDirectory", this._packageDirectory.FullPath];

        if (OperatingSystem.IsWindows())
        {
            var exe = Path.Combine(Path.GetTempPath(), $"nuget-{Guid.NewGuid()}.exe");
            await DownloadFileAsync("https://dist.nuget.org/win-x86-commandline/latest/nuget.exe", exe);

            _ = await Cli.Wrap(exe)
                .WithArguments(args)
                .ExecuteAsync();
        }
        else
        {
            // CliWrap doesn't support UseShellExecute. On Linux, it's easier to use it as "nuget" is a shell script that use mono to run nuget.exe
            var psi = new ProcessStartInfo("nuget");
            foreach (var arg in args)
            {
                psi.ArgumentList.Add(arg);
            }

            var p = Process.Start(psi)!;
            await p.WaitForExitAsync();
            if (p.ExitCode != 0)
            {
                throw new InvalidOperationException("Error when running creating the NuGet package");
            }
        }
    }

    public Task DisposeAsync()
    {
        this._packageDirectory.Dispose();
        return Task.CompletedTask;
    }

    private static async Task DownloadFileAsync(string url, string path)
    {
        _ = Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        await using var nugetStream = await SharedHttpClient.Instance.GetStreamAsync(url);
        await using var fileStream = File.Create(path);
        await nugetStream.CopyToAsync(fileStream);
    }
}
