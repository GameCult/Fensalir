using Vortice.D3DCompiler;

namespace Aquarium.Engine.Render;

internal static class D3D12ShaderCompiler
{
    public static ReadOnlyMemory<byte> Compile(string path, string entryPoint, string profile, bool skipOptimizationInDebug = true)
    {
        var flags = ShaderFlags.EnableStrictness;
#if DEBUG
        flags |= ShaderFlags.Debug;
        if (skipOptimizationInDebug) flags |= ShaderFlags.SkipOptimization;
#endif
        var source = ExpandIncludes(path, []);
        return Compiler.Compile(source, entryPoint, path, profile, flags, EffectFlags.None);
    }

    internal static string ExpandIncludes(string path, HashSet<string> stack)
    {
        var fullPath = Path.GetFullPath(path);
        if (!stack.Add(fullPath)) throw new InvalidOperationException($"Circular shader include detected at {fullPath}");
        var lines = File.ReadAllLines(fullPath);
        var expanded = new List<string>(lines.Length);
        var directory = Path.GetDirectoryName(fullPath) ?? AppContext.BaseDirectory;
        for (var index = 0; index < lines.Length; index++)
        {
            var trimmed = lines[index].TrimStart();
            if (trimmed.StartsWith("#include \"", StringComparison.Ordinal))
            {
                var first = trimmed.IndexOf('"');
                var second = trimmed.IndexOf('"', first + 1);
                if (second > first)
                {
                    var include = ResolveInclude(directory, trimmed.Substring(first + 1, second - first - 1));
                    expanded.Add($"#line 1 \"{include.Replace("\\", "\\\\")}\"");
                    expanded.Add(ExpandIncludes(include, stack));
                    expanded.Add($"#line {index + 2} \"{fullPath.Replace("\\", "\\\\")}\"");
                    continue;
                }
            }
            expanded.Add(lines[index]);
        }
        stack.Remove(fullPath);
        return string.Join(Environment.NewLine, expanded);
    }

    private static string ResolveInclude(string directory, string includeName)
    {
        var local = Path.GetFullPath(Path.Combine(directory, includeName));
        if (File.Exists(local)) return local;
        var normalized = includeName.Replace('\\', '/');
        if (normalized.StartsWith("CultMath/", StringComparison.Ordinal))
        {
            var relative = normalized["CultMath/".Length..].Replace('/', Path.DirectorySeparatorChar);
            foreach (var start in new[] { directory, AppContext.BaseDirectory })
            {
                var current = Path.GetFullPath(start);
                while (!string.IsNullOrEmpty(current))
                {
                    var candidate = Path.Combine(current, "CultMath", "shaders", relative);
                    if (File.Exists(candidate)) return candidate;
                    var parent = Path.GetDirectoryName(current);
                    if (string.IsNullOrEmpty(parent) || parent == current) break;
                    current = parent;
                }
            }
        }
        const string cultMathUnityPrefix = "Packages/org.gamecult.cultmath/shaders/";
        if (normalized.StartsWith(cultMathUnityPrefix, StringComparison.Ordinal))
        {
            var relative = normalized[cultMathUnityPrefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            foreach (var start in new[] { directory, AppContext.BaseDirectory })
            {
                var current = Path.GetFullPath(start);
                while (!string.IsNullOrEmpty(current))
                {
                    var localPackage = Path.Combine(current, "CultMath", relative);
                    if (File.Exists(localPackage)) return localPackage;
                    var siblingRepository = Path.Combine(current, "CultMath", "shaders", relative);
                    if (File.Exists(siblingRepository)) return siblingRepository;
                    var parent = Path.GetDirectoryName(current);
                    if (string.IsNullOrEmpty(parent) || parent == current) break;
                    current = parent;
                }
            }
        }
        if (normalized.StartsWith("GameCult.Geometry/", StringComparison.Ordinal))
        {
            var relative = normalized["GameCult.Geometry/".Length..].Replace('/', Path.DirectorySeparatorChar);
            foreach (var start in new[] { directory, AppContext.BaseDirectory })
            {
                var current = Path.GetFullPath(start);
                while (!string.IsNullOrEmpty(current))
                {
                    var candidate = Path.Combine(current, "CultLib", "src", "GameCult.Geometry", "Shaders", relative);
                    if (File.Exists(candidate)) return candidate;
                    var parent = Path.GetDirectoryName(current);
                    if (string.IsNullOrEmpty(parent) || parent == current) break;
                    current = parent;
                }
            }
        }
        return local;
    }
}
