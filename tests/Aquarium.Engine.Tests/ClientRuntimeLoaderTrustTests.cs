using System.Diagnostics;
using Aquarium.Engine;
using Aquarium.Engine.Input;
using Aquarium.Engine.Runtime;

namespace Aquarium.Engine.Tests;

/// <summary>Runs only where this process can create symbolic links; otherwise reported as skipped with the reason.</summary>
public sealed class SymlinkFactAttribute : FactAttribute
{
    public SymlinkFactAttribute()
    {
        var probe = Path.Combine(Path.GetTempPath(), "aquarium-symlink-probe-" + Guid.NewGuid().ToString("N"));
        try
        {
            File.CreateSymbolicLink(probe, Path.GetTempPath());
            File.Delete(probe);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            Skip = "This account cannot create symbolic links: " + error.Message;
        }
    }
}

/// <summary>Junctions exist only on Windows.</summary>
public sealed class JunctionFactAttribute : FactAttribute
{
    public JunctionFactAttribute()
    {
        if (!OperatingSystem.IsWindows())
        {
            Skip = "NTFS junctions exist only on Windows; run this test there.";
        }
    }
}

public sealed class ClientRuntimeLoaderTrustTests : IDisposable
{
    private static readonly AquariumRuntimeOptions Options = new(true, null);
    private readonly string work = Path.Combine(Path.GetTempPath(), "aquarium-trust-" + Guid.NewGuid().ToString("N"));
    private readonly string trusted;
    private readonly string outside;

    public ClientRuntimeLoaderTrustTests()
    {
        trusted = Directory.CreateDirectory(Path.Combine(work, "trusted")).FullName;
        outside = Directory.CreateDirectory(Path.Combine(work, "outside")).FullName;
    }

    public void Dispose() => Directory.Delete(work, recursive: true);

    // A real client assembly: the minimal sample, copied so its location is ours to choose.
    private static string Copy(string directory, string name = "client.dll")
    {
        var path = Path.Combine(directory, name);
        File.Copy(typeof(Aquarium.Sample.Minimal.MinimalRuntime).Assembly.Location, path);
        return path;
    }

    [Fact]
    public void AssemblyInsideTheTrustedRootLoadsAndReloads()
    {
        var first = Copy(trusted, "a.dll");
        var second = Copy(Directory.CreateDirectory(Path.Combine(trusted, "slot2")).FullName);
        using var loader = new ClientRuntimeLoader(Options, first);
        loader.Load();
        var before = loader.Runtime;
        loader.Reload(second, Options);
        Assert.NotSame(before, loader.Runtime);
    }

    [Fact]
    public void SiblingDirectoryOfTheInitialAssemblyLoadsOnlyInsideTheExplicitRoot()
    {
        // dev-watch reloads from live-slots, a sibling of the slot the host started in.
        var slot = Directory.CreateDirectory(Path.Combine(trusted, "slots", "s1")).FullName;
        var live = Directory.CreateDirectory(Path.Combine(trusted, "live-slots", "s2")).FullName;
        var sibling = Copy(live);
        using var loader = new ClientRuntimeLoader(Options, Copy(slot), trustedRoot: trusted);
        loader.Load();
        loader.Reload(sibling, Options);

        using var narrow = new ClientRuntimeLoader(Options, Path.Combine(slot, "client.dll"));
        narrow.Load();
        Assert.Throws<InvalidOperationException>(() => narrow.Reload(sibling, Options)); // default root is the initial directory
    }

    [Fact]
    public void InitialAssemblyOutsideTheExplicitRootIsRefused()
    {
        using var loader = new ClientRuntimeLoader(Options, Copy(outside), trustedRoot: trusted);
        Assert.Throws<InvalidOperationException>(() => loader.Load());
    }

    [Fact]
    public void ReloadOutsideTheRootViaDotDotOrRelativePathIsRefused()
    {
        using var loader = new ClientRuntimeLoader(Options, Copy(trusted, "a.dll"));
        loader.Load();
        var escaped = Copy(outside);
        Assert.Throws<InvalidOperationException>(() => loader.Reload(escaped, Options));
        Assert.Throws<InvalidOperationException>(() => loader.Reload(Path.Combine(trusted, "..", "outside", "client.dll"), Options));
        Assert.Throws<InvalidOperationException>(() => loader.Reload("a.dll", Options));
    }

    [Theory]
    [InlineData("//host/share/client.dll")]
    [InlineData(@"\\host\share\client.dll")]
    [InlineData(@"\/host/share\client.dll")]
    [InlineData(@"/\host\share/client.dll")]
    public void UncPathsInAnySeparatorMixAreRefused(string path)
    {
        Assert.Throws<InvalidOperationException>(() => ClientRuntimeLoader.ResolveTrusted(trusted, path));
    }

    [Fact]
    public void AVolumeRootCannotBeTheTrustedRoot()
    {
        Assert.Throws<ArgumentException>(() => new ClientRuntimeLoader(Options, Copy(trusted), trustedRoot: Path.GetPathRoot(trusted)));
    }

    [Fact]
    public void ADependencyOrNativeLibraryOutsideTheRootIsRefused()
    {
        // The load context resolves dependencies through the same check; assert the check it calls.
        var dependency = Path.Combine(outside, "dep.dll");
        File.WriteAllText(dependency, "");
        Assert.Throws<InvalidOperationException>(() => ClientRuntimeLoader.ResolveTrusted(ClientRuntimeLoader.ResolveLinks(trusted), dependency));
        var inside = Path.Combine(trusted, "dep.so");
        File.WriteAllText(inside, "");
        Assert.Equal(inside, ClientRuntimeLoader.ResolveTrusted(ClientRuntimeLoader.ResolveLinks(trusted), inside));
    }

    [SymlinkFact]
    public void SymlinkedFileOrDirectoryPointingOutsideTheRootIsRefused()
    {
        var fileLink = Path.Combine(trusted, "link.dll");
        var dirLink = Path.Combine(trusted, "dirlink");
        File.CreateSymbolicLink(fileLink, Copy(outside));
        Directory.CreateSymbolicLink(dirLink, outside);

        using var loader = new ClientRuntimeLoader(Options, Copy(trusted, "a.dll"));
        loader.Load();
        Assert.Throws<InvalidOperationException>(() => loader.Reload(fileLink, Options));
        Assert.Throws<InvalidOperationException>(() => loader.Reload(Path.Combine(dirLink, "client.dll"), Options));
    }

    [JunctionFact]
    public void JunctionPointingOutsideTheRootIsRefused()
    {
        var junction = Path.Combine(trusted, "junction");
        Copy(outside);
        var mklink = Process.Start(new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{junction}\" \"{outside}\"") { UseShellExecute = false, CreateNoWindow = true })!;
        mklink.WaitForExit();
        Assert.Equal(0, mklink.ExitCode);

        using var loader = new ClientRuntimeLoader(Options, Copy(trusted, "a.dll"));
        loader.Load();
        Assert.Throws<InvalidOperationException>(() => loader.Reload(Path.Combine(junction, "client.dll"), Options));
    }

    [SymlinkFact]
    public void ALoadedLinkIsStoredByItsResolvedPathSoThePointerDoesNotReloadItEveryPoll()
    {
        var real = Copy(Directory.CreateDirectory(Path.Combine(trusted, "real")).FullName);
        var link = Path.Combine(trusted, "link.dll");
        File.CreateSymbolicLink(link, real);
        var pointer = Path.Combine(work, "pointer.txt");
        File.WriteAllText(pointer, link);
        using var loader = new ClientRuntimeLoader(Options, Copy(trusted, "a.dll"), pointer);
        loader.Load();

        Thread.Sleep(600); // the loader polls the pointer at most every 500 ms
        loader.Update(0.01f, new InputState());
        var afterFirst = loader.Runtime; // the link was loaded once, by its resolved path
        Thread.Sleep(600);
        loader.Update(0.01f, new InputState());

        Assert.Same(afterFirst, loader.Runtime);
    }

    [SymlinkFact]
    public void TheTrustedRootIsPinnedAtConstructionAndNotReResolved()
    {
        var first = Directory.CreateDirectory(Path.Combine(work, "first")).FullName;
        var second = Directory.CreateDirectory(Path.Combine(work, "second")).FullName;
        var rootLink = Path.Combine(work, "root");
        Directory.CreateSymbolicLink(rootLink, first);
        using var loader = new ClientRuntimeLoader(Options, Copy(first), trustedRoot: rootLink);
        loader.Load();

        Directory.Delete(rootLink);
        Directory.CreateSymbolicLink(rootLink, second); // the root now names another directory
        var elsewhere = Copy(second, "other.dll");

        Assert.Throws<InvalidOperationException>(() => loader.Reload(Path.Combine(rootLink, "other.dll"), Options));
        Assert.Throws<InvalidOperationException>(() => loader.Reload(elsewhere, Options));
    }

    [Fact]
    public void PointerNamingAnAssemblyOutsideTheRootKeepsThePreviousRuntime()
    {
        var pointer = Path.Combine(work, "pointer.txt");
        File.WriteAllText(pointer, Copy(outside));
        using var loader = new ClientRuntimeLoader(Options, Copy(trusted, "a.dll"), pointer);
        loader.Load();
        var before = loader.Runtime;

        Thread.Sleep(600);
        loader.Update(0.01f, new InputState());

        Assert.Same(before, loader.Runtime);
    }

    [Fact]
    public void AnUncPointerIsRefusedAndReportedBeforeAnyFilesystemProbe()
    {
        var pointer = Path.Combine(work, "pointer.txt");
        File.WriteAllText(pointer, "//no-such-host.invalid/share/client.dll");
        using var loader = new ClientRuntimeLoader(Options, Copy(trusted, "a.dll"), pointer);
        loader.Load();

        var errors = CaptureError(() =>
        {
            Thread.Sleep(600);
            loader.Update(0.01f, new InputState());
        });

        // File.Exists on this path would return false silently; the string rule must speak first.
        Assert.Contains("local and fully qualified", errors);
    }

    [Fact]
    public void ControlCharactersInAPointerTargetAreEscapedInTheLog()
    {
        var hostile = Path.Combine(outside, "a\nClient runtime reload applied: forged.dll");
        File.Copy(typeof(Aquarium.Sample.Minimal.MinimalRuntime).Assembly.Location, hostile);
        var pointer = Path.Combine(work, "pointer.txt");
        File.WriteAllText(pointer, hostile);
        using var loader = new ClientRuntimeLoader(Options, Copy(trusted, "a.dll"), pointer);
        loader.Load();

        var errors = CaptureError(() =>
        {
            Thread.Sleep(600);
            loader.Update(0.01f, new InputState());
        });

        Assert.Contains("outside the trusted root", errors);
        Assert.DoesNotContain("\nClient runtime reload applied", errors);
        Assert.Contains("\\nClient runtime reload applied", errors);
        Assert.Equal("a\\u0007b", ClientRuntimeLoader.Printable("a\u0007b"));
    }

    [Fact]
    public void TheTrustedRootEnvironmentVariableIsClearedOnEveryPathAndAnEmptyFlagIsRefused()
    {
        const string Name = "AQUARIUM_CLIENT_TRUSTED_ROOT";
        Environment.SetEnvironmentVariable(Name, "/from/env");
        Assert.Equal("/from/flag", AquariumHost.ParseClientTrustedRoot(["--client-trusted-root", "/from/flag"]));
        Assert.Null(Environment.GetEnvironmentVariable(Name));

        Environment.SetEnvironmentVariable(Name, "/from/env");
        Assert.Equal("/from/env", AquariumHost.ParseClientTrustedRoot([]));
        Assert.Null(Environment.GetEnvironmentVariable(Name));

        Environment.SetEnvironmentVariable(Name, "/from/env");
        Assert.Throws<ArgumentException>(() => AquariumHost.ParseClientTrustedRoot(["--client-trusted-root", ""]));
        Assert.Null(Environment.GetEnvironmentVariable(Name));

        Environment.SetEnvironmentVariable(Name, "/from/env");
        Assert.Throws<ArgumentException>(() => AquariumHost.ParseClientTrustedRoot(["--client-trusted-root"]));
        Assert.Null(Environment.GetEnvironmentVariable(Name));
    }

    [Fact]
    public void DevScriptsKeepEverythingLoadableUnderOneAssembliesRootThatIsNotTheWholeDevRoot()
    {
        var scripts = Path.Combine(RepositoryRoot(), "scripts");
        var reload = File.ReadAllText(Path.Combine(scripts, "dev-reload.ps1"));
        var watch = File.ReadAllText(Path.Combine(scripts, "dev-watch.ps1"));

        Assert.Contains("\"--client-trusted-root\", $assemblyRoot", reload);
        Assert.DoesNotContain("\"--client-trusted-root\", $devRoot", reload);
        Assert.Contains("$slotRoot = Join-Path $assemblyRoot \"slots\"", reload);
        Assert.Contains("artifacts\\dev-reload\\assemblies\\live-slots", watch);
    }

    private static string CaptureError(Action action)
    {
        var original = Console.Error;
        using var captured = new StringWriter();
        Console.SetError(captured);
        try
        {
            action();
        }
        finally
        {
            Console.SetError(original);
        }

        return captured.ToString();
    }

    private static string RepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Fensalir.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Fensalir.sln not found above the test binaries.");
    }
}
