using System.Text.RegularExpressions;

namespace Aquarium.Engine.Tests;

public sealed class FieldReservoirAbiTests
{
    [Fact]
    public void HlslReservoirRowsCarryNativeDomainState()
    {
        var repoRoot = FindRepoRoot();
        var hlsl = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "Render", "Shaders", "D3D12FieldReservoir.hlsli"));
        var renderer = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "Render", "D3D12Renderer.cs"));

        var structMatch = Regex.Match(
            hlsl,
            @"struct\s+FieldReservoirSample\s*\{(?<body>.*?)\};",
            RegexOptions.Singleline);

        Assert.True(structMatch.Success);
        var lanes = Regex.Matches(structMatch.Groups["body"].Value, @"\bfloat4\s+\w+\s*;")
            .Select(match => match.Value)
            .ToArray();

        Assert.Equal(9, lanes.Length);
        Assert.Contains("float4 domainSample;", lanes);
        Assert.Contains("float4 domainSupport;", lanes);
        Assert.Contains("private const int FieldReservoirCandidateStrideBytes = 144;", renderer);
        Assert.Contains("private const int FieldReservoirHistoryStrideBytes = 144;", renderer);
    }

    [Fact]
    public void NativeDomainSamplesAreRequiredForReservoirValidity()
    {
        var repoRoot = FindRepoRoot();
        var hlsl = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "Render", "Shaders", "D3D12FieldReservoir.hlsli"));

        Assert.Contains("bool fieldReservoirDomainStateValid", hlsl);
        Assert.Contains("fieldReservoirDomainStateValid(sample) &&", hlsl);
        Assert.Contains("float fieldReservoirDomainSupportOverlap", hlsl);
    }

    [Fact]
    public void PostHistoryValidationHasProducerReplayGates()
    {
        var repoRoot = FindRepoRoot();
        var post = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "Render", "Shaders", "D3D12Post.hlsl"));

        Assert.Contains("float tubeFieldReplayValidationWeight", post);
        Assert.Contains("float sdfObjectReplayValidationWeight", post);
        Assert.Contains("previousCameraPosition", post);
        Assert.Contains("sdfObject.previousCenterPad", post);
        Assert.Contains("return 10.0;", post);
    }

    [Fact]
    public void TubeFieldReplayUsesManifestInsteadOfSingleBatchBinding()
    {
        var repoRoot = FindRepoRoot();
        var post = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "Render", "Shaders", "D3D12Post.hlsl"));
        var renderer = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "Render", "D3D12Renderer.cs"));

        Assert.Contains("StructuredBuffer<TubeFieldReplayManifestEntry> tubeFieldReplayManifest", post);
        Assert.Contains("bool tubeFieldReplayEntryFor", post);
        Assert.Contains("[loop]", post);
        Assert.DoesNotContain("tubeFieldDrawBatches.Count == 1", renderer);
        Assert.Contains("MaxTubeFieldReplaySources", renderer);
        Assert.Contains("CreateRawShaderResourceView", renderer);
    }

    [Fact]
    public void ReservoirBaselineModeIsAnExplicitValidationSwitch()
    {
        var repoRoot = FindRepoRoot();
        var settings = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine.Contracts", "GraphicsSettings.cs"));
        var host = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "AquariumHost.cs"));
        var post = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "Render", "Shaders", "D3D12Post.hlsl"));
        var capture = File.ReadAllText(Path.Combine(repoRoot, "scripts", "capture-fensalir-frame.ps1"));

        Assert.Contains("FieldReservoirModeNativeDomain", settings);
        Assert.Contains("FieldReservoirModeTexelBaseline", settings);
        Assert.Contains("--field-reservoir-mode", host);
        Assert.Contains("nativeDomainReservoirEnabled", post);
        Assert.Contains("FieldReservoirMode", capture);
    }

    [Fact]
    public void ReservoirSpatialReuseHasExplicitBudgetOwner()
    {
        var repoRoot = FindRepoRoot();
        var settings = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine.Contracts", "GraphicsSettings.cs"));
        var host = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "AquariumHost.cs"));
        var renderer = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "Render", "D3D12Renderer.cs"));
        var post = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "Render", "Shaders", "D3D12Post.hlsl"));
        var capture = File.ReadAllText(Path.Combine(repoRoot, "scripts", "capture-reservoir-mode-sequence.ps1"));

        Assert.Contains("FieldReservoirSpatialReuseBudget", settings);
        Assert.Contains("MinFieldReservoirSpatialReuseBudget = 0.25f", settings);
        Assert.Contains("--field-reservoir-spatial-reuse-budget", host);
        Assert.Contains("ReservoirBudgetInfo", renderer);
        Assert.Contains("fieldReservoirShouldSpendSpatialReuse", post);
        Assert.Contains("SpatialReuseBudget", capture);
    }

    [Fact]
    public void ReservoirDisocclusionDebugModeIsOwnerDerived()
    {
        var repoRoot = FindRepoRoot();
        var settings = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine.Contracts", "GraphicsSettings.cs"));
        var renderer = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "Render", "D3D12Renderer.cs"));
        var post = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "Render", "Shaders", "D3D12Post.hlsl"));

        Assert.Contains("MaxRenderDebugMode = 19", settings);
        Assert.Contains("Reservoir Disocclusion", renderer);
        Assert.Contains("previousUvOut", post);
        Assert.Contains("previousCloser", post);
        Assert.Contains("supportLost", post);
    }

    [Fact]
    public void ReservoirWorkGridIsBudgetedBelowPresentationResolution()
    {
        var repoRoot = FindRepoRoot();
        var settings = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine.Contracts", "GraphicsSettings.cs"));
        var renderer = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "Render", "D3D12Renderer.cs"));
        var capture = File.ReadAllText(Path.Combine(repoRoot, "scripts", "capture-fensalir-frame.ps1"));

        Assert.Contains("MaxFieldReservoirScale = 0.75f", settings);
        Assert.Contains("FieldReservoirScale: 0.5f", settings);
        Assert.Contains("ResolveReservoirWorkGrid", renderer);
        Assert.Contains("reservoirWidth", renderer);
        Assert.Contains("new Vector2(reservoirWidth, reservoirHeight)", renderer);
        Assert.Contains("RSSetViewports(reservoirViewport)", renderer);
        Assert.Contains("RSSetViewports(viewport)", renderer);
        Assert.Contains("--field-reservoir-scale", capture);
    }

    private static string FindRepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Fensalir.sln")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not locate Fensalir.sln from test output directory.");
    }
}
