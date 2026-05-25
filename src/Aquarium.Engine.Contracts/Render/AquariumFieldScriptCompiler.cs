using System.Globalization;
using System.Numerics;

namespace Aquarium.Engine.Render;

public static class AquariumFieldScriptCompiler
{
    public static AquariumBufferFieldFrame Compile(
        string source,
        IReadOnlyDictionary<string, AquariumTextureFieldBinding> textureBindings,
        float reservoirRadius = 1.0f)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(textureBindings);

        var loweringPolicy = AquariumFieldLoweringPolicy.Default;
        var textures = new List<AquariumTextureFieldBinding>();
        var programs = new List<AquariumTextureSplineFieldProgram>();
        var nodes = new List<AquariumFieldGraphNode>();

        var lines = source.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
        {
            var line = StripComment(lines[lineIndex]).Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var tokens = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var args = ParseArgs(tokens.Skip(1), lineIndex);
            switch (tokens[0])
            {
                case "texture":
                    var textureId = Required(args, "id", lineIndex);
                    if (!textureBindings.TryGetValue(textureId, out var texture))
                    {
                        throw new FormatException($"Unknown texture binding `{textureId}` at line {lineIndex + 1}.");
                    }

                    textures.Add(texture);
                    break;
                case "surface":
                    nodes.Add(ParseSurfaceNode(args, lineIndex));
                    break;
                case "splinefield":
                    programs.Add(ParseSplineField(args, nodes, lineIndex));
                    break;
                case "lowering":
                    loweringPolicy = ParseLoweringPolicy(args, lineIndex);
                    break;
                default:
                    throw new FormatException($"Unknown field DSL command `{tokens[0]}` at line {lineIndex + 1}.");
            }
        }

        if (textures.Count == 0)
        {
            throw new FormatException("Field DSL must bind at least one `texture id=...`.");
        }

        if (programs.Count == 0)
        {
            throw new FormatException("Field DSL must declare at least one `splinefield`.");
        }

        loweringPolicy = loweringPolicy.Normalized();
        var reservoir = DefaultReservoir(
            loweringPolicy.MaxReservoirSplats,
            reservoirRadius);

        return new AquariumBufferFieldFrame
        {
            Textures = textures,
            TextureSplineFields = programs,
            Reservoir = reservoir,
            LoweringPolicy = loweringPolicy,
            SourceScript = source,
        };
    }

    private static AquariumTextureSplineFieldProgram ParseSplineField(
        IReadOnlyDictionary<string, string> args,
        IReadOnlyList<AquariumFieldGraphNode> surfaceNodes,
        int lineIndex)
    {
        var axis = Enum.Parse<AquariumTextureAxis>(Required(args, "frequencyAxis", lineIndex), ignoreCase: true);
        var radius = Float(args, "radius", 0.016f, lineIndex);
        var alpha = Float(args, "alpha", 0.88f, lineIndex);
        var feather = Float(args, "feather", 0.20f, lineIndex);
        var subdivisions = Int(args, "subdivisions", 4, lineIndex);
        var maxProbes = Int(args, "maxProbes", 4096, lineIndex);
        return new AquariumTextureSplineFieldProgram(
            Required(args, "id", lineIndex),
            Required(args, "texture", lineIndex),
            axis,
            Int(args, "firstColumn", 0, lineIndex),
            Int(args, "columns", 1, lineIndex),
            Int(args, "columnStride", 1, lineIndex),
            Int(args, "rollingModulo", 0, lineIndex),
            subdivisions,
            Vec3(args, "origin", new Vector3(-0.78f, -0.52f, -0.10f), lineIndex),
            Vec3(args, "axisStep", new Vector3(0.016f, 0.0f, 0.0f), lineIndex),
            Vec3(args, "columnStep", new Vector3(0.0f, -0.055f, 0.030f), lineIndex),
            Float(args, "amplitudeScale", 0.42f, lineIndex),
            new AquariumSplineTubeAppearance(
                Vec4(args, "emission", new Vector4(1.0f, 0.72f, 0.22f, 1.0f), lineIndex),
                radius,
                alpha,
                Float(args, "zeroThreshold", 0.74f, lineIndex),
                feather,
                Float(args, "tangent", 1.0f, lineIndex),
                Float(args, "curvature", 0.62f, lineIndex),
                Float(args, "normal", 0.34f, lineIndex),
                Float(args, "derivative", 0.82f, lineIndex)),
            new AquariumSplineTubeProbePolicy(
                maxProbes,
                Float(args, "density", 1.0f, lineIndex),
                Float(args, "minimumContribution", 0.006f, lineIndex),
                UInt(args, "seed", 0xB11FF13Du, lineIndex)),
            surfaceNodes.ToArray());
    }

    private static AquariumFieldGraphNode ParseSurfaceNode(IReadOnlyDictionary<string, string> args, int lineIndex)
    {
        var inputs = args.TryGetValue("inputs", out var inputText)
            ? inputText.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            : [];
        return new AquariumFieldGraphNode(
            Required(args, "id", lineIndex),
            Required(args, "op", lineIndex),
            inputs,
            Vec4(args, "value", Vector4.Zero, lineIndex));
    }

    private static AquariumFieldLoweringPolicy ParseLoweringPolicy(IReadOnlyDictionary<string, string> args, int lineIndex)
    {
        return new AquariumFieldLoweringPolicy(
            Enum.Parse<AquariumFieldLoweringMode>(StringValue(args, "mode", "auto"), ignoreCase: true),
            Int(args, "maxSplines", AquariumFieldLoweringPolicy.Default.MaxDirectSplines, lineIndex),
            Int(args, "maxControlPoints", AquariumFieldLoweringPolicy.Default.MaxDirectControlPoints, lineIndex),
            Int(args, "maxSplats", AquariumFieldLoweringPolicy.Default.MaxReservoirSplats, lineIndex),
            Float(args, "lodBias", AquariumFieldLoweringPolicy.Default.LodBias, lineIndex));
    }

    private static AquariumFractalReservoirField DefaultReservoir(
        int splatCount,
        float reservoirRadius,
        int? updates = null,
        int candidates = 2,
        uint seed = 0xA17EA11u)
    {
        var count = Math.Clamp(splatCount, 1, 4_194_304);
        return new AquariumFractalReservoirField
        {
            SplatCount = count,
            Depth = 1,
            Seed = seed,
            CandidatesPerReservoirUpdate = Math.Clamp(candidates, 1, 8),
            SplatUpdatesPerFrame = count,
            ReservoirUpdatesPerPass = Math.Clamp(updates ?? count, 1, count),
            ProgramMode = 3,
            WorldCenterRadius = new Vector4(0.0f, 0.0f, 12.0f, MathF.Max(0.0001f, reservoirRadius)),
        };
    }

    private static IReadOnlyDictionary<string, string> ParseArgs(IEnumerable<string> tokens, int lineIndex)
    {
        var args = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var token in tokens)
        {
            var separator = token.IndexOf('=', StringComparison.Ordinal);
            if (separator <= 0 || separator == token.Length - 1)
            {
                throw new FormatException($"Invalid key=value token `{token}` at line {lineIndex + 1}.");
            }

            args[token[..separator]] = token[(separator + 1)..];
        }

        return args;
    }

    private static string StripComment(string line)
    {
        var index = line.IndexOf('#', StringComparison.Ordinal);
        return index < 0 ? line : line[..index];
    }

    private static string Required(IReadOnlyDictionary<string, string> args, string key, int lineIndex)
    {
        if (!args.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
        {
            throw new FormatException($"Missing `{key}` at line {lineIndex + 1}.");
        }

        return value;
    }

    private static string StringValue(IReadOnlyDictionary<string, string> args, string key, string fallback) =>
        args.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : fallback;

    private static int Int(IReadOnlyDictionary<string, string> args, string key, int fallback, int lineIndex) =>
        args.TryGetValue(key, out var value) ? int.Parse(value, CultureInfo.InvariantCulture) : fallback;

    private static uint UInt(IReadOnlyDictionary<string, string> args, string key, uint fallback, int lineIndex) =>
        args.TryGetValue(key, out var value) ? uint.Parse(value, CultureInfo.InvariantCulture) : fallback;

    private static float Float(IReadOnlyDictionary<string, string> args, string key, float fallback, int lineIndex) =>
        args.TryGetValue(key, out var value) ? float.Parse(value, CultureInfo.InvariantCulture) : fallback;

    private static Vector3 Vec3(IReadOnlyDictionary<string, string> args, string key, Vector3 fallback, int lineIndex)
    {
        if (!args.TryGetValue(key, out var value))
        {
            return fallback;
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 3)
        {
            throw new FormatException($"`{key}` expects three comma-separated values at line {lineIndex + 1}.");
        }

        return new Vector3(Parse(parts[0]), Parse(parts[1]), Parse(parts[2]));
    }

    private static Vector4 Vec4(IReadOnlyDictionary<string, string> args, string key, Vector4 fallback, int lineIndex)
    {
        if (!args.TryGetValue(key, out var value))
        {
            return fallback;
        }

        var parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 4)
        {
            throw new FormatException($"`{key}` expects four comma-separated values at line {lineIndex + 1}.");
        }

        return new Vector4(Parse(parts[0]), Parse(parts[1]), Parse(parts[2]), Parse(parts[3]));
    }

    private static float Parse(string value) => float.Parse(value, CultureInfo.InvariantCulture);
}
