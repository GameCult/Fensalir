using System.Numerics;
using CultMath;
using Aquarium.Engine.Fractal.Grammar;

namespace Aquarium.Engine.Fractal.Lod;

public static class FractalGpuProgramCompiler
{
    public static AquariumPackedFractalIfsTransform[] CompileFlame2D(
        FractalFlameDefinition flame,
        int maxTransformCount)
    {
        ArgumentNullException.ThrowIfNull(flame);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTransformCount);

        var transforms = new AquariumPackedFractalIfsTransform[Math.Min(maxTransformCount, flame.Transforms.Count)];
        for (var index = 0; index < transforms.Length; index++)
        {
            transforms[index] = PackFlame(flame.Transforms[index]);
        }

        return transforms;
    }

    public static AquariumPackedFractalIfsTransform[] CompileSelectedTree(
        FractalOwnershipTree tree,
        IReadOnlyList<AquariumSelectedCut> selectedCut,
        int maxTransformCount)
    {
        ArgumentNullException.ThrowIfNull(tree);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxTransformCount);

        if (selectedCut.Count == 0 || tree.Claims.Count == 0)
        {
            return [];
        }

        var selected = selectedCut.Select(cut => cut.NodeKey).ToHashSet();
        var domains = tree.Domains.ToDictionary(domain => domain.Key);
        var transforms = new List<AquariumPackedFractalIfsTransform>(Math.Min(maxTransformCount, tree.Claims.Count));
        foreach (var node in tree.Nodes)
        {
            if (!selected.Contains(node.Key))
            {
                continue;
            }

            var claimEnd = Math.Min(node.FirstClaimIndex + node.ClaimCount, tree.Claims.Count);
            for (var claimIndex = node.FirstClaimIndex; claimIndex < claimEnd && transforms.Count < maxTransformCount; claimIndex++)
            {
                transforms.Add(Pack(tree.Claims[claimIndex], domains));
            }

            if (transforms.Count >= maxTransformCount)
            {
                break;
            }
        }

        return transforms.ToArray();
    }

    private static AquariumPackedFractalIfsTransform PackFlame(FractalFlameTransform2D transform)
    {
        return new AquariumPackedFractalIfsTransform(
            transform.Matrix,
            new Vector4(transform.Translation, transform.Variations.Linear, transform.Variations.Spherical),
            new Vector4(transform.Variations.Bubble, MathF.Max(transform.Weight, 0.0f), transform.Color, transform.Variations.JulianDist),
            new Vector4(transform.Variations.Disc, transform.Variations.Julian, transform.Variations.JulianPower, transform.Variations.GaussianBlur),
            transform.PostMatrix,
            new Vector4(transform.PostTranslation, 0.0f, 0.0f));
    }

    private static AquariumPackedFractalIfsTransform Pack(
        AquariumBrushClaim claim,
        IReadOnlyDictionary<AquariumFractalKey, AquariumFractalDomain> domains)
    {
        var radius = MathF.Max(claim.Radii.X, claim.Radii.Y);
        var material = StableUnit(claim.Tags, claim.Seed);
        var tileAddress = domains.TryGetValue(claim.DomainKey, out var domainRow)
            && domainRow.Kind == AquariumFractalDomainKind.CubeSphereTile
            ? domainRow.Parameters0
            : new Vector4((float)PlanetaryCubeFace.PositiveZ, 0.0f, 0.0f, 0.0f);
        var rotationCos = MathF.Cos(claim.RotationRadians);
        var rotationSin = MathF.Sin(claim.RotationRadians);

        return new AquariumPackedFractalIfsTransform(
            new Vector4(claim.Center, radius, claim.Amplitude),
            new Vector4(claim.Radii, claim.RotationRadians, claim.Falloff),
            new Vector4(material, claim.Seed, rotationCos, rotationSin),
            tileAddress,
            new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
            new Vector4(0.0f, 0.0f, (float)EncodingFor(claim.PayloadKind), 0.0f));
    }

    private static AquariumFieldEncoding EncodingFor(AquariumFractalPayloadKind payloadKind)
    {
        return payloadKind switch
        {
            AquariumFractalPayloadKind.Height => AquariumFieldEncoding.Height,
            AquariumFractalPayloadKind.SignedDistance => AquariumFieldEncoding.SignedDistance,
            AquariumFractalPayloadKind.Density => AquariumFieldEncoding.Density,
            AquariumFractalPayloadKind.Extinction => AquariumFieldEncoding.Extinction,
            AquariumFractalPayloadKind.Material => AquariumFieldEncoding.Material,
            _ => throw new ArgumentOutOfRangeException(nameof(payloadKind), payloadKind, "Unknown fractal payload kind."),
        };
    }

    private static float StableUnit(string tags, int seed)
    {
        var hash = unchecked((uint)seed);
        foreach (var ch in tags)
        {
            hash ^= ch;
            hash *= 16777619u;
        }

        return (hash & 0x00FF_FFFFu) / 16777215.0f;
    }
}
