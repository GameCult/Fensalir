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
    /// Every assembly this loader runs, and every dependency or native library it resolves, must lie, links followed,
    /// under <paramref name="trustedRoot"/> (default: the directory of the initial assembly). The root is resolved once
    /// here and pinned; a volume root is refused. A relative initial path is taken against the working directory;
    /// a relative or UNC reload target is refused. The boundary this enforces is the root directory's own ACLs.
    /// </summary>
    public ClientRuntimeLoader(
        AquariumRuntimeOptions options,
        string? clientAssemblyPath = null,
        string? clientReloadPointerPath = null,
        string? trustedRoot = null)
    {
        this.clientAssemblyPath = string.IsNullOrWhiteSpace(clientAssemblyPath) ? null : Path.GetFullPath(clientAssemblyPath);
        this.clientReloadPointerPath = clientReloadPointerPath;
        this.options = options;

        var root = string.IsNullOrWhiteSpace(trustedRoot)
            ? this.clientAssemblyPath is null ? null : Path.GetDirectoryName(this.clientAssemblyPath)
            : Path.GetFullPath(trustedRoot);
        if (root is not null)
        {
            root = ResolveLinks(root);
            if (string.Equals(
                    Path.TrimEndingDirectorySeparator(root),
                    Path.TrimEndingDirectorySeparator(Path.GetPathRoot(root) ?? root),
                    PathComparison))
            {
                throw new ArgumentException($"A drive or volume root cannot be the client runtime trusted root: {Printable(root)}", nameof(trustedRoot));
            }

            Console.WriteLine($"Client runtime trusted root: {Printable(root)}");
        }

        this.trustedRoot = root;
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

    public void Reload(string assemblyPath, AquariumRuntimeOptions options) =>
        LoadResolved(ResolveTrusted(assemblyPath), options);

    private void LoadResolved(string assemblyPath, AquariumRuntimeOptions options)
    {
        var previous = current;
        var loadContext = new ClientAssemblyLoadContext(assemblyPath, trustedRoot!);
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

        if (string.IsNullOrWhiteSpace(candidatePath))
        {
            return;
        }

        try
        {
            RequireLocalFullyQualified(candidatePath); // string rules first: no filesystem or network probe of a UNC path
            if (!File.Exists(candidatePath))
            {
                return;
            }

            var resolved = ResolveTrusted(candidatePath);
            if (string.Equals(resolved, currentAssemblyPath, PathComparison))
            {
                return;
            }

            current?.Runtime.FlushState();
            LoadResolved(resolved, options);
            Console.WriteLine($"Client runtime reload applied: {Printable(resolved)}");
        }
        catch (Exception error)
        {
            Console.Error.WriteLine($"Client runtime reload failed; keeping previous runtime. {Printable(error.Message)}");
        }
    }

    private static StringComparison PathComparison =>
        OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private string ResolveTrusted(string assemblyPath) =>
        trustedRoot is null
            ? throw new InvalidOperationException("No trusted root for client runtime assemblies is set.")
            : ResolveTrusted(trustedRoot, assemblyPath);

    // The final path of a local, fully qualified file under the already-resolved root; throws otherwise.
    internal static string ResolveTrusted(string resolvedRoot, string path)
    {
        RequireLocalFullyQualified(path);
        var resolved = ResolveLinks(Path.GetFullPath(path));
        var relative = Path.GetRelativePath(resolvedRoot, resolved);
        if (relative == ".." || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal) || Path.IsPathRooted(relative))
        {
            throw new InvalidOperationException($"Client runtime path is outside the trusted root '{Printable(resolvedRoot)}': {Printable(path)}");
        }

        return resolved;
    }

    internal static void RequireLocalFullyQualified(string path)
    {
        if (!Path.IsPathFullyQualified(path) || (path.Length >= 2 && IsSeparator(path[0]) && IsSeparator(path[1])))
        {
            throw new InvalidOperationException($"Client runtime path must be local and fully qualified: {Printable(path)}");
        }
    }

    // Paths come from files other people can write: show control characters escaped, never as raw log lines.
    internal static string Printable(string text)
    {
        var builder = new System.Text.StringBuilder(text.Length);
        foreach (var c in text)
        {
            if (char.IsControl(c))
            {
                builder.Append(c switch { '\n' => "\\n", '\r' => "\\r", '\t' => "\\t", _ => $"\\u{(int)c:x4}" });
            }
            else
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    private static bool IsSeparator(char c) => c is '/' or '\\';

    // The path with every symlink or junction on it replaced by its final target.
    internal static string ResolveLinks(string path, int depth = 0)
    {
        if (depth > 32)
        {
            throw new InvalidOperationException($"Too many links resolving {Printable(path)}.");
        }

        for (var probe = path; probe is not null; probe = Path.GetDirectoryName(probe))
        {
            FileSystemInfo info = Directory.Exists(probe) ? new DirectoryInfo(probe) : new FileInfo(probe);
            if (info.LinkTarget is null)
            {
                continue;
            }

            var target = info.ResolveLinkTarget(returnFinalTarget: true)?.FullName
                ?? throw new InvalidOperationException($"Link does not resolve: {Printable(probe)}");
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

    private sealed class ClientAssemblyLoadContext(string assemblyPath, string trustedRoot) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver resolver = new(assemblyPath);

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            if (assemblyName.Name == typeof(IAquariumRuntime).Assembly.GetName().Name)
            {
                return null;
            }

            var path = resolver.ResolveAssemblyToPath(assemblyName);
            return path == null ? null : LoadFromAssemblyPath(ResolveTrusted(trustedRoot, path));
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            var path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            return path == null ? IntPtr.Zero : LoadUnmanagedDllFromPath(ResolveTrusted(trustedRoot, path));
        }
    }
}
