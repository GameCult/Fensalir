using System.Globalization;
using System.Numerics;

namespace Aquarium.Engine.Render;

public static class AquariumFieldScriptCompiler
{
    public static AquariumFieldEvidenceFrame CompileEvidence(
        string source,
        IReadOnlyDictionary<string, AquariumFieldResourceDeclaration> resourceBindings)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(resourceBindings);

        var resources = new List<AquariumFieldResourceDeclaration>();
        var domains = new List<AquariumFieldDomain>();
        var claims = new List<AquariumFieldClaim>();
        var candidates = new List<AquariumFieldCandidate>();
        var tubeSplineLowerings = new List<AquariumFieldTubeSplineLowering>();
        var resourceAliases = new Dictionary<string, AquariumFieldResourceDeclaration>(StringComparer.Ordinal);
        var domainKeys = new HashSet<string>(StringComparer.Ordinal);

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
                case "resource":
                    BindResource(args, resourceBindings, resourceAliases, resources, lineIndex);
                    break;
                case "texture2d":
                    BindTexture2D(args, resourceAliases, resources, lineIndex);
                    break;
                case "surfacepage":
                    BindSurfacePage(args, resourceAliases, resources, lineIndex);
                    break;
                case "domain":
                    var domain = ParseFieldDomain(args, lineIndex);
                    if (domainKeys.Add(domain.DomainKey))
                    {
                        domains.Add(domain);
                    }

                    break;
                case "tubeclaim":
                    AddTubeClaim(args, resourceAliases, domainKeys, domains, claims, candidates, lineIndex);
                    break;
                case "tubespline":
                    AddTubeSpline(args, resourceAliases, domainKeys, domains, claims, candidates, tubeSplineLowerings, lineIndex);
                    break;
                default:
                    throw new FormatException($"Unknown field evidence DSL command `{tokens[0]}` at line {lineIndex + 1}.");
            }
        }

        var frame = new AquariumFieldEvidenceFrame
        {
            Resources = resources,
            Domains = domains,
            Claims = claims,
            Candidates = candidates,
            TubeSplineLowerings = tubeSplineLowerings,
        };
        var validation = AquariumFieldEvidenceValidator.Validate(frame);
        if (validation.HasErrors)
        {
            var first = validation.Issues.First(issue => issue.Severity == AquariumFieldEvidenceIssueSeverity.Error);
            throw new FormatException($"Invalid field evidence DSL output for `{first.Key}`: {first.Message}");
        }

        return frame;
    }

    private static void BindTexture2D(
        IReadOnlyDictionary<string, string> args,
        IDictionary<string, AquariumFieldResourceDeclaration> resourceAliases,
        ICollection<AquariumFieldResourceDeclaration> resources,
        int lineIndex)
    {
        var id = Required(args, "id", lineIndex);
        var key = StringValue(args, "key", $"aquarium:resource:texture2d:{id}");
        var path = Required(args, "path", lineIndex);
        var resource = AquariumFieldResourceDeclaration.LocalTexture2D(
            key,
            path,
            StringValue(args, "format", "Rgba8Unorm"),
            UInt64(args, "version", 0, lineIndex),
            Int(args, "width", 0, lineIndex),
            Int(args, "height", 0, lineIndex));

        resourceAliases[id] = resource;
        resourceAliases[key] = resource;
        if (resources.All(existing => !string.Equals(existing.ResourceKey, resource.ResourceKey, StringComparison.Ordinal)))
        {
            resources.Add(resource);
        }
    }

    private static void BindSurfacePage(
        IReadOnlyDictionary<string, string> args,
        IDictionary<string, AquariumFieldResourceDeclaration> resourceAliases,
        ICollection<AquariumFieldResourceDeclaration> resources,
        int lineIndex)
    {
        var id = Required(args, "id", lineIndex);
        var key = StringValue(args, "key", $"aquarium:resource:surface-page:{id}");
        var resource = AquariumFieldResourceDeclaration.SurfacePage(
            key,
            Int(args, "width", 1, lineIndex),
            Int(args, "height", 1, lineIndex),
            StringValue(args, "format", "R16Float"),
            UInt64(args, "version", 0, lineIndex),
            StringValue(args, "path", ""));

        resourceAliases[id] = resource;
        resourceAliases[key] = resource;
        if (resources.All(existing => !string.Equals(existing.ResourceKey, resource.ResourceKey, StringComparison.Ordinal)))
        {
            resources.Add(resource);
        }
    }

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

    private static void BindResource(
        IReadOnlyDictionary<string, string> args,
        IReadOnlyDictionary<string, AquariumFieldResourceDeclaration> resourceBindings,
        IDictionary<string, AquariumFieldResourceDeclaration> resourceAliases,
        ICollection<AquariumFieldResourceDeclaration> resources,
        int lineIndex)
    {
        var id = Required(args, "id", lineIndex);
        var key = StringValue(args, "key", id);
        if (!resourceBindings.TryGetValue(id, out var resource) &&
            !resourceBindings.TryGetValue(key, out resource))
        {
            throw new FormatException($"Unknown field resource `{key}` at line {lineIndex + 1}.");
        }

        resourceAliases[id] = resource;
        if (resources.All(existing => !string.Equals(existing.ResourceKey, resource.ResourceKey, StringComparison.Ordinal)))
        {
            resources.Add(resource);
        }
    }

    private static AquariumFieldDomain ParseFieldDomain(IReadOnlyDictionary<string, string> args, int lineIndex) =>
        new(
            Required(args, "id", lineIndex),
            StringValue(args, "parent", ""),
            Enum.Parse<AquariumFieldDomainKind>(StringValue(args, "kind", nameof(AquariumFieldDomainKind.RollingBuffer)), ignoreCase: true),
            Matrix4x4.Identity,
            Matrix4x4.Identity,
            Vec3(args, "min", Vector3.Zero, lineIndex),
            Vec3(args, "max", Vector3.One, lineIndex),
            Vec3(args, "period", Vector3.Zero, lineIndex),
            StringValue(args, "owner", "AquariumFieldScriptCompiler"));

    private static void AddTubeClaim(
        IReadOnlyDictionary<string, string> args,
        IReadOnlyDictionary<string, AquariumFieldResourceDeclaration> resourceAliases,
        ISet<string> domainKeys,
        ICollection<AquariumFieldDomain> domains,
        ICollection<AquariumFieldClaim> claims,
        ICollection<AquariumFieldCandidate> candidates,
        int lineIndex)
    {
        var id = Required(args, "id", lineIndex);
        var resourceId = Required(args, "resource", lineIndex);
        if (!resourceAliases.TryGetValue(resourceId, out var resource))
        {
            throw new FormatException($"Tube claim `{id}` references unbound resource `{resourceId}` at line {lineIndex + 1}.");
        }

        var domainKey = StringValue(args, "domain", $"dsl:domain:{id}");
        if (domainKeys.Add(domainKey))
        {
            domains.Add(new AquariumFieldDomain(
                domainKey,
                "",
                AquariumFieldDomainKind.RollingBuffer,
                Matrix4x4.Identity,
                Matrix4x4.Identity,
                Vector3.Zero,
                Vector3.One,
                Vector3.Zero,
                "AquariumFieldScriptCompiler"));
        }

        var confidence = Math.Clamp(Float(args, "confidence", 1.0f, lineIndex), 0.0f, 1.0f);
        var supportRadius = MathF.Max(0.0001f, Float(args, "radius", 0.01f, lineIndex));
        var claim = new AquariumFieldClaim(
            ClaimKey: $"dsl:tube:{id}",
            DomainKey: domainKey,
            ProducerKey: StringValue(args, "producer", resource.ResourceKey),
            Layer: AquariumFieldLayer.Form,
            Encoding: AquariumFieldEncoding.Tube,
            Support: new AquariumFieldSupport(
                Vec3(args, "center", Vector3.Zero, lineIndex),
                Vec3(args, "support", new Vector3(supportRadius), lineIndex),
                Matrix4x4.Identity,
                supportRadius,
                ProjectedError: 0.0f,
                Curvature: Float(args, "curvature", 0.0f, lineIndex),
                TemporalUncertainty: Float(args, "temporal", 0.0f, lineIndex)),
            Proposal: new AquariumFieldProposalPolicy(
                AquariumFieldProposalKind.DeterministicStructural,
                SourcePdf: 1.0f,
                TargetContribution: confidence,
                RepresentedCandidateCount: Math.Max(1, resource.DepthOrCount),
                Seed: UInt(args, "seed", StableSeed(id), lineIndex)),
            PayloadHandle: resource.ResourceKey,
            ObservedTimeNs: resource.ValidUntilNs,
            Confidence: confidence);
        claims.Add(claim);
        candidates.Add(new AquariumFieldCandidate(
            CandidateKey: $"dsl:tube:{id}:candidate",
            ClaimKey: claim.ClaimKey,
            Layer: claim.Layer,
            Encoding: claim.Encoding,
            Proposal: claim.Proposal,
            Guide: AquariumFieldGuide.Valid(confidence, Float(args, "age", 0.0f, lineIndex))));
    }

    private static void AddTubeSpline(
        IReadOnlyDictionary<string, string> args,
        IReadOnlyDictionary<string, AquariumFieldResourceDeclaration> resourceAliases,
        ISet<string> domainKeys,
        ICollection<AquariumFieldDomain> domains,
        ICollection<AquariumFieldClaim> claims,
        ICollection<AquariumFieldCandidate> candidates,
        ICollection<AquariumFieldTubeSplineLowering> lowerings,
        int lineIndex)
    {
        var id = Required(args, "id", lineIndex);
        AddTubeClaim(args, resourceAliases, domainKeys, domains, claims, candidates, lineIndex);
        var claimKey = $"dsl:tube:{id}";
        var resourceId = Required(args, "resource", lineIndex);
        var resource = resourceAliases[resourceId];
        var ramp = StringValue(args, "ramp", "");
        var rampResourceKey = resourceAliases.TryGetValue(ramp, out var rampResource)
            ? rampResource.ResourceKey
            : "";
        var rampTexturePath = rampResourceKey.Length > 0
            ? rampResource.SourceUri
            : ramp;
        var lowering = new AquariumFieldTubeSplineLowering(
            LoweringKey: $"dsl:tube-spline:{id}",
            ClaimKey: claimKey,
            ResourceKey: resource.ResourceKey,
            Width: Int(args, "width", Math.Max(2, resource.Width), lineIndex),
            Height: Int(args, "height", Math.Max(1, resource.Height), lineIndex),
            StrideBytes: Int(args, "stride", Math.Max(4, resource.StrideBytes), lineIndex),
            FirstColumn: Int(args, "firstColumn", 0, lineIndex),
            ColumnCount: Int(args, "columns", Math.Max(1, resource.Height), lineIndex),
            ColumnStride: Int(args, "columnStride", 1, lineIndex),
            RollingModulo: Int(args, "rollingModulo", Math.Max(0, resource.Height), lineIndex),
            RollingOffset: Int(args, "rollingOffset", 0, lineIndex),
            Origin: Vec3(args, "origin", new Vector3(-0.78f, -0.52f, -0.10f), lineIndex),
            AxisStep: Vec3(args, "axisStep", new Vector3(0.016f, 0.0f, 0.0f), lineIndex),
            ColumnStep: Vec3(args, "columnStep", new Vector3(0.0f, 0.0f, 0.030f), lineIndex),
            AmplitudePower: Float(args, "amplitudePower", 2.0f, lineIndex),
            AmplitudeScale: Float(args, "amplitudeScale", 0.42f, lineIndex),
            NormalizeMin: Float(args, "normalizeMin", 0.0f, lineIndex),
            NormalizeMax: Float(args, "normalizeMax", 1.0f, lineIndex),
            BaseRadius: Float(args, "radius", 0.012f, lineIndex),
            RadiusScale: Float(args, "radiusScale", 0.030f, lineIndex),
            Alpha: Float(args, "alpha", 0.92f, lineIndex),
            Feather: Float(args, "feather", 0.20f, lineIndex),
            RampTexturePath: rampTexturePath,
            RampResourceKey: rampResourceKey,
            EmissionScale: Float(args, "emissionScale", 10.0f, lineIndex),
            CatmullRomSubdivisions: Int(args, "subdivisions", 4, lineIndex)).Normalized();
        lowerings.Add(lowering);
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
            Int(args, "columnGroupSize", 0, lineIndex),
            Vec3(args, "columnGroupStep", Vector3.Zero, lineIndex),
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

    private static ulong UInt64(IReadOnlyDictionary<string, string> args, string key, ulong fallback, int lineIndex) =>
        args.TryGetValue(key, out var value) ? ulong.Parse(value, CultureInfo.InvariantCulture) : fallback;

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

    private static uint StableSeed(string key)
    {
        const uint fnvPrime = 16777619u;
        var hash = 2166136261u;
        foreach (var character in key)
        {
            hash ^= character;
            hash *= fnvPrime;
        }

        return hash == 0 ? 1u : hash;
    }
}
