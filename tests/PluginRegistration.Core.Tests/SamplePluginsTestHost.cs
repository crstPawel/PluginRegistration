using System.Diagnostics;
using Xunit;

namespace PluginRegistration.Core.Tests;

/// <summary>
/// Builds samples/Sample.Plugins once for reflection-based tests.
/// Disables NuGet pack (sample is packable for local demo) to avoid
/// file-lock races when xUnit runs tests in parallel.
/// </summary>
internal static class SamplePluginsTestHost
{
    private static readonly object Gate = new();
    private static bool _built;
    private static Exception? _buildFailure;

    public static string ProjectDirectory { get; } = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "samples", "Sample.Plugins"));

    public static string AssemblyPath { get; } = Path.Combine(
        ProjectDirectory, "bin", "Debug", "net462", "Sample.Plugins.dll");

    public static void EnsureBuilt()
    {
        lock (Gate)
        {
            if (_built)
            {
                return;
            }

            if (_buildFailure is not null)
            {
                throw _buildFailure;
            }

            try
            {
                BuildOnce();
                _built = true;
            }
            catch (Exception ex)
            {
                _buildFailure = ex;
                throw;
            }
        }
    }

    private static void BuildOnce()
    {
        var projectFile = Path.Combine(ProjectDirectory, "Sample.Plugins.csproj");
        Assert.True(File.Exists(projectFile), $"Sample project not found: {projectFile}");

        // Tests only need the plugin DLL. Sample.Plugins is IsPackable for local demos;
        // packing during parallel test builds races on Sample.Plugins.*.nupkg (file in use → 403-style IO error).
        var psi = new ProcessStartInfo
        {
            FileName = "dotnet",
            Arguments =
                $"build \"{projectFile}\" -c Debug --no-restore --nologo --verbosity quiet " +
                "/p:GeneratePackageOnBuild=false /p:IsPackable=false",
            WorkingDirectory = ProjectDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(120_000))
        {
            try { process.Kill(entireProcessTree: true); } catch { /* ignore */ }
            throw new Xunit.Sdk.XunitException(
                "Timed out building sample plugins for test (120s).");
        }

        if (process.ExitCode != 0)
        {
            throw new Xunit.Sdk.XunitException(
                $"Failed to build sample plugins for test.\nSTDOUT:\n{stdout}\nSTDERR:\n{stderr}");
        }

        Assert.True(File.Exists(AssemblyPath), $"Expected sample plugin assembly at {AssemblyPath}");
    }
}
