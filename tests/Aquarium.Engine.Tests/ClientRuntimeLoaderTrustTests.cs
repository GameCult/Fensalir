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

/// <summary>Windows file names cannot contain a newline, so this setup cannot be built there.</summary>
public sealed class NewlineInFileNameFactAttribute : FactAttribute
{
    public NewlineInFileNameFactAttribute()
    {
        if (OperatingSystem.IsWindows())
        {
            Skip = "Windows file names cannot contain a newline; the escaping is covered by the Printable tests.";
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
        // Windows refuses an untrusted (non-administrator) mount point itself; the loader reports it as a refusal too.
        Assert.ThrowsAny<InvalidOperationException>(() => loader.Reload(Path.Combine(junction, "client.dll"), Options));
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

    [NewlineInFileNameFact]
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
    public void TheTrustedRootEnvironmentVariableIsTakenOnceOnEveryPathAndCached()
    {
        const string Name = "AQUARIUM_CLIENT_TRUSTED_ROOT";
        Environment.SetEnvironmentVariable(Name, "/from/env");
        var setting = TrustedRootSetting.FromProcessEnvironment();
        Assert.Equal("/from/flag", setting.Resolve(["--client-trusted-root", "/from/flag"]));
        Assert.Null(Environment.GetEnvironmentVariable(Name)); // cleared although the flag won
        Assert.Equal("/from/env", setting.Resolve([])); // cached: a second resolution does not fall to the default root
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ABlankEnvironmentValueIsRefusedLikeABlankFlag(string value)
    {
        Assert.Throws<ArgumentException>(() => new TrustedRootSetting(() => value).Resolve([]));
        Assert.Throws<ArgumentException>(() => new TrustedRootSetting(() => null).Resolve(["--client-trusted-root", value]));
        Assert.Throws<ArgumentException>(() => new TrustedRootSetting(() => null).Resolve(["--client-trusted-root"]));
    }

    [Fact]
    public void APathOutsideTheRootIsRefusedBeforeAnyLinkOnItIsFollowed()
    {
        // A link loop outside the root: following it would throw "Too many links"; the string check must refuse first.
        var a = Path.Combine(outside, "a");
        var b = Path.Combine(outside, "b");
        try
        {
            Directory.CreateSymbolicLink(a, b);
            Directory.CreateSymbolicLink(b, a);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            // No links here: the check is still exercised by the nonexistent outside path below.
        }

        var error2 = Assert.Throws<InvalidOperationException>(
            () => ClientRuntimeLoader.ResolveTrusted(ClientRuntimeLoader.ResolveLinks(trusted), Path.Combine(a, "client.dll")));
        Assert.Contains("outside the trusted root", error2.Message);
    }

    [Fact]
    public void ANonexistentPointerTargetOutsideTheRootIsRefusedAndReportedNotSilentlyIgnored()
    {
        var pointer = Path.Combine(work, "pointer.txt");
        File.WriteAllText(pointer, Path.Combine(outside, "not-there.dll"));
        using var loader = new ClientRuntimeLoader(Options, Copy(trusted, "a.dll"), pointer);
        loader.Load();

        var errors = CaptureError(() =>
        {
            Thread.Sleep(600);
            loader.Update(0.01f, new InputState());
        });

        Assert.Contains("outside the trusted root", errors);
    }

    [SymlinkFact]
    public void ARootReachedThroughALinkAsGivenStillAcceptsPathsUnderIt()
    {
        var real = Directory.CreateDirectory(Path.Combine(work, "real")).FullName;
        var rootLink = Path.Combine(work, "rootlink");
        Directory.CreateSymbolicLink(rootLink, real);
        Copy(real, "a.dll");
        var viaLink = Path.Combine(rootLink, "b.dll");
        File.Copy(typeof(Aquarium.Sample.Minimal.MinimalRuntime).Assembly.Location, viaLink);
        using var loader = new ClientRuntimeLoader(Options, Path.Combine(rootLink, "a.dll"), trustedRoot: rootLink);
        loader.Load();
        loader.Reload(viaLink, Options);
    }

    [Fact]
    public void ABackslashInAPointerTargetIsEscapedExactlyOnceInTheLog()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // a backslash is a separator in a Windows file name; the escaping itself is covered by the Printable tests
        }

        var hostile = Path.Combine(outside, "x\\y.dll");
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

        Assert.Contains("x\\\\y.dll", errors); // one literal backslash is shown as two characters
        Assert.DoesNotContain("x\\\\\\\\y.dll", errors); // not four: the message is not escaped a second time
    }

    [Fact]
    public void AStartupRefusalIsLoggedByTheHostAndExitsNonzero()
    {
        var volumeRoot = Path.GetPathRoot(Path.GetFullPath(work))!;
        var exit = 0;
        var errors = CaptureError(() => exit = AquariumHost.Run(["--headless", "--client-trusted-root", volumeRoot]));

        Assert.Equal(1, exit);
        Assert.Contains("Client runtime failed to start.", errors);
        Assert.Contains("cannot be the client runtime trusted root", errors);
    }

    [Fact]
    public void LoggedPathsEscapeAstralFormatCharactersAndLoneSurrogatesByCodePoint()
    {
        Assert.Equal("a\\U000e0001b", ClientRuntimeLoader.Printable("a\U000E0001b")); // language tag
        Assert.Equal("a\\U000e0041b", ClientRuntimeLoader.Printable("a\U000E0041b")); // tag letter
        Assert.Equal("a\\U0001d173b", ClientRuntimeLoader.Printable("a\U0001D173b")); // musical begin beam
        Assert.Equal("a\\ud800b", ClientRuntimeLoader.Printable("a\ud800b")); // lone high surrogate
        Assert.Equal("a\\udc00b", ClientRuntimeLoader.Printable("a\udc00b")); // lone low surrogate
        Assert.Equal("a\\ud800\\ud800b", ClientRuntimeLoader.Printable("a\ud800\ud800b")); // two highs in a row
        Assert.Equal("a\U0001F600b", ClientRuntimeLoader.Printable("a\U0001F600b")); // an ordinary astral character is kept
    }

    [Fact]
    public void LoggedPathsEscapeBackslashesLineSeparatorsAndBidiAndZeroWidthControls()
    {
        Assert.Equal("a\\\\b", ClientRuntimeLoader.Printable("a\\b"));
        Assert.Equal("a\\u2028b", ClientRuntimeLoader.Printable("a\u2028b"));
        Assert.Equal("a\\u2029b", ClientRuntimeLoader.Printable("a\u2029b"));
        Assert.Equal("a\\u202eb", ClientRuntimeLoader.Printable("a\u202eb"));
        Assert.Equal("a\\u200bb", ClientRuntimeLoader.Printable("a\u200bb"));
        Assert.Equal("plain-name.dll", ClientRuntimeLoader.Printable("plain-name.dll"));
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

    [Fact]
    public void DevScriptsMatchTheHostAckByItsUniqueSlotLeafPruneLiveSlotsAndRejectStaleReopenState()
    {
        var scripts = Path.Combine(RepositoryRoot(), "scripts");
        var reload = File.ReadAllText(Path.Combine(scripts, "dev-reload.ps1"));
        var watch = File.ReadAllText(Path.Combine(scripts, "dev-watch.ps1"));

        Assert.Contains(".Replace(\"\\\", \"\\\\\")", watch); // the host logs backslashes doubled
        Assert.DoesNotContain("Contains($appliedLine)", watch);
        Assert.Contains("@($slotRoot, $liveSlotRoot)", reload);
        Assert.Contains("outside the assemblies root", reload);
        Assert.Contains("no longer exists (pruned slot)", reload);
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
