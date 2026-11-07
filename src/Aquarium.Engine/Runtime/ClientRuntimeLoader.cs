using System.Reflection;
using System.Runtime.Loader;
using System.Diagnostics;

namespace Aquarium.Engine.Runtime;

public sealed class ClientRuntimeLoader : IDisposable
{
    private readonly string? clientAssemblyPath;
    private readonly string? clientReloadPointerPath;
    private readonly string? trustedRoot;
    private readonly Stopwatch reloadClock = Stopwatch.StartNew();
    private readonly AquariumRuntimeOptions options;
    private ReloadedRuntime? current;
    private TimeSpan lastReloadCheck;
    private string? currentAssemblyPath;

    /// <summary>
    /// Every assembly this loader runs must resolve, links followed, to a path under <paramref name="trustedRoot"/>
    /// (default: the directory of the initial assembly). A relative initial path is taken against the working directory;
    /// a relative or UNC reload target is refused.
    /// </summary>
    public ClientRuntimeLoader(
        AquariumRuntimeOptions options,
        string? clientAssemblyPath = null,
        string? clientReloadPointerPath = null,
        string? trustedRoot = null)
    {
        this.clientAssemblyPath = string.IsNullOrWhiteSpace(clientAssemblyPath) ? null : Path.GetFullPath(clientAssemblyPath);
        this.clientReloadPointerPath = clientReloadPointerPath;
        this.trustedRoot = string.IsNullOrWhiteSpace(trustedRoot)
            ? this.clientAssemblyPath is null ? null : Path.GetDirectoryName(this.clientAssemblyPath)
            : Path.GetFullPath(trustedRoot);
        this.options = options;
    }

    public IAquariumRuntime Runtime
    {
        get
        {
            if (current == null)
            {
                Reload(RequireClientAssemblyPath(), options);
            }

            return current!.Runtime;
        }
    }

    public IAquariumRuntime Load()
    {
        Reload(RequireClientAssemblyPath(), options);
        return Runtime;
    }

    public void Reload(string assemblyPath, AquariumRuntimeOptions options)
    {
        RequireTrusted(assemblyPath);
        var previous = current;
        var loadContext = new ClientAssemblyLoadContext(assemblyPath);
        IAquariumRuntime? nextRuntime = null;

        try
        {
            var assembly = loadContext.LoadFromAssemblyPath(assemblyPath);
            var factoryTypes = assembly.GetTypes()
                .Where(type => typeof(IAquariumRuntimeFactory).IsAssignableFrom(type)
                    && type is { IsAbstract: false, IsInterface: false })
                .ToArray();
            if (factoryTypes.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Client runtime assembly must contain exactly one concrete {nameof(IAquariumRuntimeFactory)} implementation; found {factoryTypes.Length}.");
            }

            var factoryType = factoryTypes[0];
            var factory = (IAquariumRuntimeFactory?)Activator.CreateInstance(factoryType)
                ?? throw new InvalidOperationException($"Client runtime factory could not be created: {factoryType.FullName}");

            nextRuntime = factory.Create(options);
            nextRuntime.Start();
        }
        catch
        {
            nextRuntime?.Dispose();
            loadContext.Unload();
            throw;
        }

        current = new ReloadedRuntime(loadContext, nextRuntime);
        currentAssemblyPath = assemblyPath;
        previous?.Runtime.Dispose();
        previous?.Unload();
    }

    public void Update(float deltaSeconds, Input.InputState input)
    {
        TryReloadFromPointer();
        Runtime.Update(deltaSeconds, input);
    }

    private void TryReloadFromPointer()
    {
        if (string.IsNullOrWhiteSpace(clientReloadPointerPath) || reloadClock.Elapsed - lastReloadCheck < TimeSpan.FromMilliseconds(500))
        {
            return;
        }

        lastReloadCheck = reloadClock.Elapsed;
        string candidatePath;
        try
        {
            candidatePath = File.ReadAllText(clientReloadPointerPath).Trim();
        }
        catch
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(candidatePath)
            || string.Equals(candidatePath, currentAssemblyPath, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(candidatePath))
        {
            return;
        }

        try
        {
            current?.Runtime.FlushState();
            Reload(candidatePath, options);
            Console.WriteLine($"Client runtime reload applied: {candidatePath}");
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Client runtime reload failed; keeping previous runtime. {error.Message}");
        }
    }

    private void RequireTrusted(string assemblyPath)
    {
        if (trustedRoot is null)
        {
            throw new InvalidOperationException("No trusted root for client runtime assemblies is set.");
        }

        if (!Path.IsPathFullyQualified(assemblyPath)
            || assemblyPath.StartsWith(@"\\", StringComparison.Ordinal)
            || assemblyPath.StartsWith("//", StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Client runtime assembly path must be local and fully qualified: {assemblyPath}");
        }

        var relative = Path.GetRelativePath(ResolveLinks(trustedRoot), ResolveLinks(Path.GetFullPath(assemblyPath)));
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException($"Client runtime assembly is outside the trusted root '{trustedRoot}': {assemblyPath}");
        }
    }

    // The path with every symlink or junction on it replaced by its final target.
    private static string ResolveLinks(string path, int depth = 0)
    {
        if (depth > 32)
        {
            throw new InvalidOperationException($"Too many links resolving {path}.");
        }

        for (var probe = path; probe is not null; probe = Path.GetDirectoryName(probe))
        {
            FileSystemInfo info = Directory.Exists(probe) ? new DirectoryInfo(probe) : new FileInfo(probe);
            if (info.LinkTarget is null)
            {
                continue;
            }

            var target = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                ?? throw new InvalidOperationException($"Link does not resolve: {probe}");
            var rest = Path.GetRelativePath(probe, path);
            return ResolveLinks(rest == "." ? target : Path.GetFullPath(Path.Combine(target, rest)), depth + 1);
        }

        return path;
    }

    private string RequireClientAssemblyPath()
    {
        if (!string.IsNullOrWhiteSpace(clientAssemblyPath))
        {
            return clientAssemblyPath;
        }

        throw new InvalidOperationException("A client runtime assembly path is required. Pass --client-assembly or set AQUARIUM_CLIENT_ASSEMBLY.");
    }

    public void Dispose()
    {
        var runtime = current;
        current = null;
        runtime?.Runtime.Dispose();
        runtime?.Unload();
    }

    private sealed class ReloadedRuntime(ClientAssemblyLoadContext loadContext, IAquariumRuntime runtime)
    {
        public IAquariumRuntime Runtime { get; } = runtime;

        public void Unload()
        {
            loadContext.Unload();
        }
    }

    private sealed class ClientAssemblyLoadContext(string assemblyPath) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver resolver = new(assemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == typeof(IAquariumRuntime).Assembly.GetName().Name)
            {
                return null;
            }

            var path = resolver.ResolveAssemblyToPath(assemblyName);
            return path == null ? null : LoadFromAssemblyPath(path);
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path == null ? IntPtr.Zero : LoadUnmanagedDllFromPath(path);
        }
    }
}
