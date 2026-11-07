using Aquarium.Engine;
using Aquarium.Engine.Input;
using Aquarium.Engine.Runtime;

namespace Aquarium.Engine.Tests;

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
        loader.Reload(second, Options);
        Assert.NotNull(loader.Runtime);
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
        Assert.Throws<InvalidOperationException>(() => loader.Reload(Copy(outside), Options));

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
    public void ReloadOutsideTheRootDotDotRelativeAndUncPathsAreRefused()
    {
        using var loader = new ClientRuntimeLoader(Options, Copy(trusted, "a.dll"));
        loader.Load();
        var escaped = Copy(outside);
        Assert.Throws<InvalidOperationException>(() => loader.Reload(escaped, Options));
        Assert.Throws<InvalidOperationException>(() => loader.Reload(Path.Combine(trusted, "..", "outside", "client.dll"), Options));
        Assert.Throws<InvalidOperationException>(() => loader.Reload("a.dll", Options));
        Assert.Throws<InvalidOperationException>(() => loader.Reload("//host/share/client.dll", Options));
        Assert.Throws<InvalidOperationException>(() => loader.Reload(@"\\host\share\client.dll", Options));
    }

    [Fact]
    public void SymlinkedFileOrDirectoryPointingOutsideTheRootIsRefused()
    {
        var target = Copy(outside);
        var fileLink = Path.Combine(trusted, "link.dll");
        var dirLink = Path.Combine(trusted, "dirlink");
        try
        {
            File.CreateSymbolicLink(fileLink, target);
            Directory.CreateSymbolicLink(dirLink, outside);
        }
        catch (Exception error) when (error is IOException or UnauthorizedAccessException)
        {
            return; // this host cannot create links
        }

        using var loader = new ClientRuntimeLoader(Options, Copy(trusted, "a.dll"));
        loader.Load();
        Assert.Throws<InvalidOperationException>(() => loader.Reload(fileLink, Options));
        Assert.Throws<InvalidOperationException>(() => loader.Reload(Path.Combine(dirLink, "client.dll"), Options));
    }

    [Fact]
    public void PointerNamingAnAssemblyOutsideTheRootKeepsThePreviousRuntime()
    {
        var pointer = Path.Combine(work, "pointer.txt");
        File.WriteAllText(pointer, Copy(outside));
        using var loader = new ClientRuntimeLoader(Options, Copy(trusted, "a.dll"), pointer);
        loader.Load();
        var before = loader.Runtime;

        Thread.Sleep(600); // the loader polls the pointer at most every 500 ms
        loader.Update(0.01f, new InputState());

        Assert.Same(before, loader.Runtime);
    }
}
