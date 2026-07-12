using System.Numerics;
using System.Runtime.InteropServices;
using Aquarium.Engine.Fractal;
using Aquarium.Engine.Fractal.Lod;
using Aquarium.Engine.Render;
using Aquarium.Zyphos;

namespace Aquarium.Engine.Tests;

public sealed class D3D12PlanetarySurfacePageTests
{
    [Fact]
    public void ZyphosRegistersAndPublishesAllSdfAuthoritiesInIndexOrder()
    {
        var plan = ZyphosRenderPlan.Create();
        Assert.Equal(
            new[] { "D3D12ZyphosPlanet.hlsl", "D3D12ZyphosUmbros.hlsl", "D3D12ZyphosStar.hlsl" },
            plan.Shaders.SdfShaderPaths.Select(Path.GetFileName));

        var scene = ZyphosSceneBuilder.Build(1.0f, 0.5f, ZyphosUmbrosSystem.ZyphosCenter + Vector3.UnitZ * 12.0f, default);
        Assert.Equal(ZyphosRenderPlan.SdfObjectCount, scene.SdfObjects.Count);
        Assert.Equal(ZyphosUmbrosSystem.ZyphosBoundRadius, scene.SdfObjects[ZyphosRenderPlan.PlanetIndex].CenterRadius.W);
        Assert.Equal(ZyphosUmbrosSystem.UmbrosBoundRadius, scene.SdfObjects[ZyphosRenderPlan.UmbrosIndex].CenterRadius.W);
        Assert.Equal(ZyphosUmbrosSystem.PrimaryStarVisualRadius, scene.SdfObjects[ZyphosRenderPlan.StarIndex].CenterRadius.W);
    }

    [Fact]
    public void CameraFaceSelectionReusesResidentPageAndVersionsReplacement()
    {
        var center = ZyphosUmbrosSystem.ZyphosCenter;
        var first = ZyphosSceneBuilder.Build(0.0f, 0.0f, center + Vector3.UnitZ * 12.0f, default).PlanetarySurfacePages;
        var sameFace = ZyphosSceneBuilder.Build(0.0f, 0.0f, center + Vector3.Normalize(new Vector3(0.1f, 0.1f, 1.0f)) * 12.0f, default).PlanetarySurfacePages;
        var replacement = ZyphosSceneBuilder.Build(0.0f, 0.0f, center + Vector3.UnitX * 12.0f, default).PlanetarySurfacePages;

        Assert.Same(first, sameFace);
        Assert.NotEqual(first.Version, replacement.Version);
        Assert.NotEqual(first.Metadata.Address.X, replacement.Metadata.Address.X);
    }

    [Fact]
    public void ZyphosPlanetaryProxyShadersCompileWithPageBindings()
    {
        var temporaryRoot = Path.Combine(Path.GetTempPath(), $"aquarium-zyphos-shaders-{Guid.NewGuid():N}");
        Directory.CreateDirectory(temporaryRoot);
        try
        {
            CopyShaderTree(Path.Combine(RepositoryRoot(), "src", "Aquarium.Engine", "Render", "Shaders"), temporaryRoot);
            CopyShaderTree(Path.Combine(RepositoryRoot(), "src", "Aquarium.Zyphos", "Shaders"), temporaryRoot);
            CopyShaderTree(Path.GetFullPath(Path.Combine(RepositoryRoot(), "..", "CultMath", "shaders")), Path.Combine(temporaryRoot, "CultMath"));
            foreach (var shaderName in new[] { "D3D12ZyphosPlanet.hlsl", "D3D12ZyphosUmbros.hlsl", "D3D12ZyphosStar.hlsl" })
            {
                var bytecode = D3D12ShaderCompiler.Compile(Path.Combine(temporaryRoot, shaderName), "D3D12SdfProxyPS", "ps_5_0", skipOptimizationInDebug: false);
                Assert.False(bytecode.IsEmpty);
            }
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [Fact]
    public void IndependentlyGeneratedSiblingPagesAgreeOnSharedBoundary()
    {
        var left = new PlanetarySurfacePageRequest(new CubeTileKey(CubeFace.PositiveZ,1,0,0),9,2);
        var right = new PlanetarySurfacePageRequest(new CubeTileKey(CubeFace.PositiveZ,1,1,0),9,2);
        var leftOutput = Generate(left); var rightOutput = Generate(right);
        for(int y=0;y<left.InteriorSize;y++)
        {
            var li=(left.BorderSize+y)*left.StorageSize+left.BorderSize+left.InteriorSize-1;
            var ri=(right.BorderSize+y)*right.StorageSize+right.BorderSize;
            Assert.Equal(leftOutput[li],rightOutput[ri]);
        }
    }

    [Fact]
    public void GeneratedPagePublishesFiniteChannelsAndSummaryEvidence()
    {
        var request=new PlanetarySurfacePageRequest(new CubeTileKey(CubeFace.PositiveX,2,1,2),9,2);
        var output=Generate(request);
        Assert.All(output,s=>{Assert.True(float.IsFinite(s.HeightGradient.X));Assert.True(float.IsFinite(s.HeightGradient.Y));Assert.InRange(s.Masks.X,0,1);Assert.InRange(s.Masks.Y,0,1);});
        var min=output.Min(s=>s.HeightGradient.X); var max=output.Max(s=>s.HeightGradient.X); var slope=output.Max(s=>MathF.Sqrt(s.HeightGradient.Y*s.HeightGradient.Y+s.HeightGradient.Z*s.HeightGradient.Z));
        Assert.True(min<=max); Assert.True(slope>=0); Assert.True(output.Max(s=>s.HeightGradient.W)>=0);
        var shader=Path.Combine(RepositoryRoot(),"src","Aquarium.Zyphos","Shaders","D3D12ZyphosTerrainPageSummary.hlsl");
        var summary=D3D12ComputeProbe.Run<PageOutput,PageSummary>(shader,"D3D12ZyphosTerrainPageSummaryCS",output)[0];
        Assert.Equal(min,summary.Bounds.X); Assert.Equal(max,summary.Bounds.Y); Assert.Equal(slope,summary.Bounds.Z); Assert.Equal(output.Max(s=>s.HeightGradient.W),summary.Bounds.W);
    }

    [Fact]
    public void PageBackedRadialHitsAgreeWithDirectOracleWithinDeclaredError()
    {
        var request=new PlanetarySurfacePageRequest(new CubeTileKey(CubeFace.PositiveZ,1,1,0),9,2);
        var page=Generate(request); const float radius=8.4f,rayOriginRadius=12.0f;
        var spacing=PlanetarySurfacePageSampling.NominalAngularTexelSize(request)*radius;
        var points=new[]{new Vector2(0.08f,0.11f),new Vector2(0.37f,0.61f),new Vector2(0.52f,0.48f),new Vector2(0.79f,0.22f),new Vector2(0.91f,0.88f)};
        var directInput=points.Select(point=>new PageInput(new Vector4(PlanetarySurfacePageSampling.DirectionAtLocal(request,point.X,point.Y),radius),new Vector4(spacing,0,0,0))).ToArray();
        var direct=D3D12ComputeProbe.Run<PageInput,PageOutput>(ShaderPath(),"D3D12ZyphosTerrainPageCS",directInput);
        float H(int px,int py)=>page[py*request.StorageSize+px].HeightGradient.X;
        float Lerp(float a,float b,float t)=>a+(b-a)*t;
        var maximumSlope=page.Max(s=>MathF.Sqrt(s.HeightGradient.Y*s.HeightGradient.Y+s.HeightGradient.Z*s.HeightGradient.Z));
        for(var index=0;index<points.Length;index++)
        {
            var texel=points[index]*(request.InteriorSize-1)+new Vector2(request.BorderSize);
            var x=Math.Clamp((int)MathF.Floor(texel.X),0,request.StorageSize-2); var y=Math.Clamp((int)MathF.Floor(texel.Y),0,request.StorageSize-2); var f=texel-new Vector2(x,y);
            var interpolated=Lerp(Lerp(H(x,y),H(x+1,y),f.X),Lerp(H(x,y+1),H(x+1,y+1),f.X),f.Y);
            var directTravel=rayOriginRadius-(radius+direct[index].HeightGradient.X);
            var pageTravel=rayOriginRadius-(radius+interpolated);
            var declaredError=direct[index].HeightGradient.W+maximumSlope*spacing*1.414214f;
            Assert.InRange(MathF.Abs(pageTravel-directTravel),0,declaredError);
        }
    }

    private static PageOutput[] Generate(PlanetarySurfacePageRequest request)
    {
        const float radius=8.4f; var spacing=PlanetarySurfacePageSampling.NominalAngularTexelSize(request)*radius;
        var input=new PageInput[request.StorageSize*request.StorageSize];
        for(int y=0;y<request.StorageSize;y++) for(int x=0;x<request.StorageSize;x++)
        { var d=PlanetarySurfacePageSampling.Direction(request,x,y); input[y*request.StorageSize+x]=new PageInput(new Vector4(d,radius),new Vector4(spacing,0,0,0)); }
        var shader=ShaderPath();
        return D3D12ComputeProbe.Run<PageInput,PageOutput>(shader,"D3D12ZyphosTerrainPageCS",input);
    }

    private static string ShaderPath()=>Path.Combine(RepositoryRoot(),"src","Aquarium.Zyphos","Shaders","D3D12ZyphosTerrainPage.hlsl");

    private static void CopyShaderTree(string source, string destination)
    {
        foreach (var file in Directory.EnumerateFiles(source, "*.*", SearchOption.AllDirectories))
        {
            var relative = Path.GetRelativePath(source, file);
            var target = Path.Combine(destination, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, overwrite: true);
        }
    }

    private static string RepositoryRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"Fensalir.sln")))d=d.Parent;return d?.FullName??throw new InvalidOperationException();}
    [StructLayout(LayoutKind.Sequential)] private readonly record struct PageInput(Vector4 DirectionRadius,Vector4 Sampling);
    [StructLayout(LayoutKind.Sequential)] private readonly record struct PageOutput(Vector4 HeightGradient,Vector4 Masks);
    [StructLayout(LayoutKind.Sequential)] private readonly record struct PageSummary(Vector4 Bounds,Vector4 Metadata);
}
