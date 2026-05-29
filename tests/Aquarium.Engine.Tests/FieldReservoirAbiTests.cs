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
