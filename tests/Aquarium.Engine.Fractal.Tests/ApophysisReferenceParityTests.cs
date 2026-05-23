using System.Globalization;
using System.Numerics;
using System.Xml.Linq;
using Aquarium.Engine.Fractal.Grammar;
using Aquarium.Engine.Fractal.Lod;

namespace Aquarium.Engine.Fractal.Tests;

public sealed class ApophysisReferenceParityTests
{
    [Fact]
    public void AquariumAffineIfsMatchesApophysisLinearFixtureForFixedXformSequence()
    {
        var root = FindRepoRoot();
        var flamePath = Path.Combine(root, "tests", "Aquarium.Engine.Fractal.Tests", "Fixtures", "Apophysis", "linear-sierpinski.flame");
        var aquageoPath = Path.Combine(root, "tests", "Aquarium.Engine.Fractal.Tests", "Fixtures", "Apophysis", "linear-sierpinski.aquageo");

        var reference = LoadLinearApophysisFixture(flamePath);
        var candidate = FractalDslCompiler.Compile(File.ReadAllText(aquageoPath)).AffineIfsDefinitions.Single();

        Assert.Equal(reference.Count, candidate.Transforms.Count);
        var referencePoint = Vector2.Zero;
        var candidatePoint = candidate.Start;
        var sequence = new[] { 0, 2, 1, 1, 0, 2, 2, 1, 0, 0, 2, 1 };
        foreach (var transformIndex in sequence)
        {
            referencePoint = reference[transformIndex].Apply(referencePoint);
            candidatePoint = candidate.Transforms[transformIndex].Apply(candidatePoint);
            AssertClose(referencePoint, candidatePoint);
        }
    }

    [Fact]
    public void AquariumAffineIfsChaosGameMatchesIndependentReferenceMoments()
    {
        var root = FindRepoRoot();
        var aquageoPath = Path.Combine(root, "tests", "Aquarium.Engine.Fractal.Tests", "Fixtures", "Apophysis", "linear-sierpinski.aquageo");
        var candidate = FractalDslCompiler.Compile(File.ReadAllText(aquageoPath)).AffineIfsDefinitions.Single();

        var points = FractalAffineIfsChaosGame.Generate(candidate, count: 4096, burnIn: 16, new FractalXorShiftRandom(0xA90F_1357u));
        var min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        var max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        var sum = Vector2.Zero;
        foreach (var point in points)
        {
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
            sum += point;
        }

        var mean = sum / points.Length;
        Assert.InRange(min.X, 0.0f, 0.01f);
        Assert.InRange(min.Y, 0.0f, 0.01f);
        Assert.InRange(max.X, 0.98f, 1.0f);
        Assert.InRange(max.Y, 0.84f, 0.8661f);
        Assert.InRange(mean.X, 0.47f, 0.53f);
        Assert.InRange(mean.Y, 0.27f, 0.33f);
    }

    [Fact]
    public void FlameParserReadsApophysisVariationSubset()
    {
        var root = FindRepoRoot();
        var flamePath = Path.Combine(root, "tests", "Aquarium.Engine.Fractal.Tests", "Fixtures", "Apophysis", "linear-spherical-bubble.flame");

        var flame = FractalFlameFileParser.ParseFirst(File.ReadAllText(flamePath), seed: 77);

        Assert.Equal("linear-spherical-bubble", flame.Name);
        Assert.Equal(77, flame.Seed);
        Assert.Equal(3, flame.Transforms.Count);
        Assert.Equal(1.0f, flame.Transforms[0].Variations.Linear);
        Assert.Equal(0.72f, flame.Transforms[1].Variations.Spherical);
        Assert.Equal(0.84f, flame.Transforms[2].Variations.Bubble);
        Assert.Equal(new Vector2(-0.20f, 0.08f), flame.Transforms[0].Translation);
    }

    [Fact]
    public void FlameVariationEvaluatorMatchesIndependentReference()
    {
        var root = FindRepoRoot();
        var flamePath = Path.Combine(root, "tests", "Aquarium.Engine.Fractal.Tests", "Fixtures", "Apophysis", "linear-spherical-bubble.flame");

        var flame = FractalFlameFileParser.ParseFirst(File.ReadAllText(flamePath), seed: 77);
        var point = new Vector2(0.37f, -0.21f);
        foreach (var transform in flame.Transforms)
        {
            var expected = ApplyReferenceVariation(transform, point);
            var actual = transform.Apply(point);
            AssertClose(expected, actual);
        }
    }

    [Fact]
    public void FlameHistogramIsDeterministicForReferenceFixture()
    {
        var root = FindRepoRoot();
        var flamePath = Path.Combine(root, "tests", "Aquarium.Engine.Fractal.Tests", "Fixtures", "Apophysis", "linear-spherical-bubble.flame");
        var flame = FractalFlameFileParser.ParseFirst(File.ReadAllText(flamePath), seed: 77);

        var points = FractalFlameChaosGame.Generate(flame, count: 8192, burnIn: 64, new FractalXorShiftRandom(0xBADC_0DEu));
        var histogram = FractalPointHistogramBuilder.Build(points, 64, 64, new Vector4(-8.0f, -8.0f, 8.0f, 8.0f));
        var occupied = histogram.Bins.Count(value => value > 0);
        var checksum = histogram.Bins.Aggregate(2166136261u, (hash, value) => unchecked((hash ^ (uint)value) * 16777619u));

        Assert.Equal(8192, histogram.HitCount);
        Assert.InRange(occupied, 450, 540);
        Assert.Equal(0xAEB1C81Bu, checksum);
    }

    [Fact]
    public void FlameParserReadsJWildfireVariationGroupSubset()
    {
        var root = FindRepoRoot();
        var flamePath = Path.Combine(root, "tests", "Aquarium.Engine.Fractal.Tests", "Fixtures", "JWildfire", "julian-disc-minimal.flame");

        var result = FractalFlameFileParser.ParseFirstWithReport(File.ReadAllText(flamePath), seed: 91);
        var flame = result.Definition;

        Assert.Equal("julian-disc-minimal", flame.Name);
        Assert.Equal(2, flame.Transforms.Count);
        Assert.Equal("JWildfire", result.Report.Dialect);
        Assert.Equal(2, result.Report.TransformCount);
        Assert.Equal(new Vector4(1.0f, 0.0f, 0.0f, 1.0f), flame.Transforms[0].Matrix);
        Assert.Equal(new Vector2(-0.1f, 0.2f), flame.Transforms[0].Translation);
        Assert.Equal(0.0f, flame.Transforms[0].Variations.Linear);
        Assert.Equal(0.5f, flame.Transforms[0].Variations.Julian);
        Assert.Equal(5.0f, flame.Transforms[0].Variations.JulianPower);
        Assert.Equal(1.25f, flame.Transforms[0].Variations.JulianDist);
        Assert.Equal(0.02f, flame.Transforms[0].Variations.GaussianBlur);
        Assert.Equal(new Vector4(0.9f, 0.0f, 0.0f, 0.9f), flame.Transforms[0].PostMatrix);
        Assert.Equal(new Vector4(0.25f, -0.75f, 0.75f, 0.25f), flame.Transforms[1].Matrix);
        Assert.Equal(0.8f, flame.Transforms[1].Variations.Disc);
        Assert.Contains("variationGroup:normal", result.Report.Transforms[0].IgnoredFields);
        Assert.Contains("variation:jwf_gaussian_blur -> stochastic gaussian_blur", result.Report.Transforms[0].ApproximatedFields);
        Assert.Contains("post", result.Report.Transforms[0].AcceptedFields);
        Assert.Empty(result.Report.Transforms[0].RejectedFields);
    }

    [Fact]
    public void JWildfireVariationSubsetFeedsGpuProgramRows()
    {
        var root = FindRepoRoot();
        var flamePath = Path.Combine(root, "tests", "Aquarium.Engine.Fractal.Tests", "Fixtures", "JWildfire", "julian-disc-minimal.flame");
        var flame = FractalFlameFileParser.ParseFirst(File.ReadAllText(flamePath), seed: 91);

        var rows = FractalGpuProgramCompiler.CompileFlame2D(flame, maxTransformCount: 8);

        Assert.Equal(2, rows.Length);
        Assert.Equal(0.5f, rows[0].TileAddress.Y);
        Assert.Equal(5.0f, rows[0].TileAddress.Z);
        Assert.Equal(0.02f, rows[0].TileAddress.W);
        Assert.Equal(0.8f, rows[1].TileAddress.X);
    }

    [Fact]
    public void JWildfireDiscVariationUsesReferenceAngleConvention()
    {
        var transform = new FractalFlameTransform2D(
            "disc",
            new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
            Vector2.Zero,
            new Vector4(1.0f, 0.0f, 0.0f, 1.0f),
            Vector2.Zero,
            1.0f,
            0.0f,
            new FractalFlameVariationWeights(0.0f, 0.0f, 0.0f, Disc: 1.0f));
        var point = new Vector2(0.25f, -0.75f);

        var actual = transform.Apply(point);
        var radius = point.Length();
        var phi = MathF.Atan2(point.X, point.Y);
        var expected = new Vector2(
            MathF.Sin(MathF.PI * radius) * phi / MathF.PI,
            MathF.Cos(MathF.PI * radius) * phi / MathF.PI);

        AssertClose(expected, actual);
    }

    [Fact]
    public void JWildfireImportReportNamesUnsupportedLosses()
    {
        var root = FindRepoRoot();
        var flamePath = Path.Combine(root, "artifacts", "reference-tools", "j-wildfire-9.00", "lib", "FARenderJWF", "selftest.flame");
        if (!File.Exists(flamePath))
        {
            return;
        }

        var result = FractalFlameFileParser.ParseFirstWithReport(File.ReadAllText(flamePath), seed: 91);

        Assert.True(result.Report.AcceptedFieldCount > 0);
        Assert.True(result.Report.ApproximatedFieldCount > 0);
        Assert.True(result.Report.RejectedFieldCount > 0);
        Assert.Contains(result.Report.Transforms, transform => transform.AcceptedFields.Contains("post"));
        Assert.Contains(result.Report.Transforms, transform => transform.RejectedFields.Any(field => field.StartsWith("wfield_", StringComparison.Ordinal)));
    }

    [Fact]
    public void PpmReferenceReceiptReadsExternalRendererOutputShape()
    {
        var ppm = new byte[]
        {
            (byte)'P', (byte)'6', (byte)'\n',
            (byte)'#', (byte)' ', (byte)'F', (byte)'L', (byte)'A', (byte)'M', (byte)'3', (byte)'\n',
            (byte)'2', (byte)' ', (byte)'2', (byte)'\n',
            (byte)'2', (byte)'5', (byte)'5', (byte)'\n',
            0, 0, 0,
            255, 0, 0,
            0, 128, 0,
            0, 0, 64,
        };

        var receipt = FractalPpmImageReceiptBuilder.Build(ppm);

        Assert.Equal(2, receipt.Width);
        Assert.Equal(2, receipt.Height);
        Assert.Equal(4, receipt.PixelCount);
        Assert.Equal(3, receipt.NonBlackPixelCount);
        Assert.Equal(0x605A_BDE5_13A2_D19AUL, receipt.RgbChecksum);
        Assert.Equal(0xD56A_A5A0_E36F_3C2BUL, receipt.LuminanceChecksum);
    }

    [Fact]
    public void PpmReferenceReceiptDoesNotTreatPixelWhitespaceAsHeader()
    {
        var ppm = new byte[]
        {
            (byte)'P', (byte)'6', (byte)'\n',
            (byte)'1', (byte)' ', (byte)'1', (byte)'\n',
            (byte)'2', (byte)'5', (byte)'5', (byte)'\n',
            10, 13, 32,
        };

        var receipt = FractalPpmImageReceiptBuilder.Build(ppm);

        Assert.Equal(1, receipt.PixelCount);
        Assert.Equal(1, receipt.NonBlackPixelCount);
        Assert.Equal(0xDD2C_094F_57F3_A456UL, receipt.RgbChecksum);
    }

    private static IReadOnlyList<ReferenceAffineTransform> LoadLinearApophysisFixture(string path)
    {
        var document = XDocument.Load(path);
        var xforms = document.Descendants("xform").ToArray();
        Assert.NotEmpty(xforms);
        return xforms.Select(xform =>
        {
            Assert.Equal("1", (string?)xform.Attribute("linear"));
            var values = ((string?)xform.Attribute("coefs") ?? string.Empty)
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(value => float.Parse(value, CultureInfo.InvariantCulture))
                .ToArray();
            Assert.Equal(6, values.Length);
            return new ReferenceAffineTransform(
                new Vector4(values[0], values[1], values[3], values[4]),
                new Vector2(values[2], values[5]));
        }).ToArray();
    }

    private static void AssertClose(Vector2 expected, Vector2 actual)
    {
        Assert.True(Vector2.Distance(expected, actual) <= 0.000001f, $"Expected {expected}, got {actual}.");
    }

    private static Vector2 ApplyReferenceVariation(FractalFlameTransform2D transform, Vector2 point)
    {
        var affine = new Vector2(
            (transform.Matrix.X * point.X) + (transform.Matrix.Y * point.Y) + transform.Translation.X,
            (transform.Matrix.Z * point.X) + (transform.Matrix.W * point.Y) + transform.Translation.Y);
        var radiusSquared = Vector2.Dot(affine, affine);
        var result = affine * transform.Variations.Linear;
        if (transform.Variations.Spherical != 0.0f)
        {
            result += affine * (transform.Variations.Spherical / MathF.Max(radiusSquared, 0.000001f));
        }

        if (transform.Variations.Bubble != 0.0f)
        {
            result += affine * (transform.Variations.Bubble * 4.0f / (radiusSquared + 4.0f));
        }

        return result;
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Fensalir.sln")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Could not find Aquarium repo root.");
    }

    private readonly record struct ReferenceAffineTransform(Vector4 Matrix, Vector2 Translation)
    {
        public Vector2 Apply(Vector2 point)
        {
            return new Vector2(
                (Matrix.X * point.X) + (Matrix.Y * point.Y) + Translation.X,
                (Matrix.Z * point.X) + (Matrix.W * point.Y) + Translation.Y);
        }
    }
}
