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
    public void CpuSurfaceNormalUsesTheSameWorldDistanceGradientContract()
    {
        var direction=Vector3.Normalize(new Vector3(0.31f,-0.27f,0.91f));
        var tangentX=Vector3.Normalize(Vector3.Cross(Vector3.UnitY,direction));
        var tangentY=Vector3.Normalize(Vector3.Cross(direction,tangentX));
        var gradient=tangentX*0.35f+tangentY*-0.18f;
        var normal=PlanetarySurfaceDifferential.SurfaceNormal(direction,gradient);
        Assert.InRange(MathF.Abs(Vector3.Dot(normal,Vector3.Normalize(direction-gradient)))-1.0f,-1.0e-6f,1.0e-6f);
        Assert.True(Vector3.Dot(normal,direction)>0.0f);
    }

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
    public void QuadtreeSelectionKeepsSixRootsAndAddsOneResidualAncestorChain()
    {
        ZyphosPlanetarySurfacePages.Reset();
        var center = ZyphosUmbrosSystem.ZyphosCenter;
        var orbital = ZyphosSceneBuilder.Build(0.0f, 0.0f, center + Vector3.UnitZ * 1000.0f, default).PlanetarySurfacePages;
        var near = ZyphosSceneBuilder.Build(2.0f, 1.0f, center + Vector3.Normalize(new Vector3(0.13f, 0.19f, 1.0f)) * (ZyphosUmbrosSystem.ZyphosSurfaceRadius + 0.05f), default).PlanetarySurfacePages;

        Assert.Equal(6, orbital.Pages.Count);
        Assert.Equal(Enum.GetValues<CubeFace>().Select(face => (float)face), orbital.Pages.Select(page => page.Metadata.Address.X));
        Assert.True(near.Pages.Count > 6);
        Assert.Equal(Enumerable.Range(1, near.Pages.Count - 6).Select(level => (float)level), near.Pages.Skip(6).Select(page => page.Metadata.Address.Y));
        Assert.All(near.Pages.Skip(6), page => Assert.True(page.Samples[0].Sampling.Y > page.Samples[0].Sampling.X));
    }

    [Fact]
    public void ChildPageStoresOnlyTheNewlyResolvableResidualBand()
    {
        const float radius=8.4f;
        var parent=new PlanetarySurfacePageRequest(new CubeTileKey(CubeFace.PositiveZ,2,2,1),9,2);
        var child=new PlanetarySurfacePageRequest(parent.Tile.Child(1,0),9,2);
        var direction=PlanetarySurfacePageSampling.DirectionAtLocal(child,0.43,0.57);
        var childSpacing=PlanetarySurfacePageSampling.NominalAngularTexelSize(child)*radius;
        var parentSpacing=PlanetarySurfacePageSampling.NominalAngularTexelSize(parent)*radius;
        var input=new[]
        {
            new PageInput(new Vector4(direction,radius),new Vector4(childSpacing,0,0,0)),
            new PageInput(new Vector4(direction,radius),new Vector4(parentSpacing,0,0,0)),
            new PageInput(new Vector4(direction,radius),new Vector4(childSpacing,parentSpacing,0,0)),
        };
        var output=D3D12ComputeProbe.Run<PageInput,PageOutput>(ShaderPath(),"D3D12ZyphosTerrainPageCS",input);
        Assert.InRange(MathF.Abs(output[0].HeightGradient.X-(output[1].HeightGradient.X+output[2].HeightGradient.X)),0,2.0e-5f);
        Assert.InRange(MathF.Abs(output[0].HeightGradient.Y-(output[1].HeightGradient.Y+output[2].HeightGradient.Y)),0,2.0e-4f);
        Assert.InRange(MathF.Abs(output[0].HeightGradient.Z-(output[1].HeightGradient.Z+output[2].HeightGradient.Z)),0,2.0e-4f);
        Assert.InRange(MathF.Abs(output[0].HeightGradient.W-(output[1].HeightGradient.W+output[2].HeightGradient.W)),0,2.0e-4f);
    }

    [Fact]
    public void ResidualArrivalChangesPresentationWithoutRegeneratingContent()
    {
        ZyphosPlanetarySurfacePages.Reset();
        var center=ZyphosUmbrosSystem.ZyphosCenter;
        var direction=Vector3.Normalize(new Vector3(-0.17f,-1.0f,0.23f));
        var camera=center+direction*(ZyphosUmbrosSystem.ZyphosSurfaceRadius+0.03f);
        var start=ZyphosSceneBuilder.Build(100.0f,99.9f,camera,default).PlanetarySurfacePages;
        var middle=ZyphosSceneBuilder.Build(100.175f,100.0f,camera,default).PlanetarySurfacePages;
        var settled=ZyphosSceneBuilder.Build(100.35f,100.175f,camera,default).PlanetarySurfacePages;
        Assert.Equal(start.ContentVersion,middle.ContentVersion);
        Assert.Equal(middle.ContentVersion,settled.ContentVersion);
        Assert.NotEqual(start.PresentationVersion,middle.PresentationVersion);
        Assert.All(start.Pages.Skip(6),page=>Assert.Equal(0.0f,page.Metadata.State.Y));
        Assert.All(middle.Pages.Skip(6),page=>Assert.InRange(page.Metadata.State.Y,0.49f,0.51f));
        Assert.All(settled.Pages.Skip(6),page=>Assert.InRange(page.Metadata.State.Y,0.999f,1.0f));
    }

    [Fact]
    public void TeleportCrossFadesOldAndNewResidualChainsBeforeEviction()
    {
        ZyphosPlanetarySurfacePages.Reset();
        var center=ZyphosUmbrosSystem.ZyphosCenter;
        var radius=ZyphosUmbrosSystem.ZyphosSurfaceRadius+0.03f;
        var cameraA=center+Vector3.Normalize(new Vector3(1.0f,0.12f,0.18f))*radius;
        var cameraB=center+Vector3.Normalize(new Vector3(-0.21f,1.0f,-0.14f))*radius;
        _=ZyphosSceneBuilder.Build(200.0f,199.9f,cameraA,default);
        var settledA=ZyphosSceneBuilder.Build(200.35f,200.0f,cameraA,default).PlanetarySurfacePages;
        var oldKeys=settledA.Pages.Skip(6).Select(page=>page.ContentKey).ToHashSet();
        var departure=ZyphosSceneBuilder.Build(201.0f,200.35f,cameraB,default).PlanetarySurfacePages;
        var newKeys=departure.Pages.Skip(6).Select(page=>page.ContentKey).Where(key=>!oldKeys.Contains(key)).ToHashSet();
        Assert.NotEmpty(newKeys);
        Assert.All(departure.Pages.Where(page=>oldKeys.Contains(page.ContentKey)),page=>Assert.InRange(page.Metadata.State.Y,0.999f,1.0f));
        Assert.All(departure.Pages.Where(page=>newKeys.Contains(page.ContentKey)),page=>Assert.Equal(0.0f,page.Metadata.State.Y));

        var midpoint=ZyphosSceneBuilder.Build(201.175f,201.0f,cameraB,default).PlanetarySurfacePages;
        Assert.All(midpoint.Pages.Where(page=>oldKeys.Contains(page.ContentKey)||newKeys.Contains(page.ContentKey)),page=>Assert.InRange(page.Metadata.State.Y,0.49f,0.51f));
        var settledB=ZyphosSceneBuilder.Build(201.36f,201.175f,cameraB,default).PlanetarySurfacePages;
        Assert.DoesNotContain(settledB.Pages,page=>oldKeys.Contains(page.ContentKey));
        Assert.All(settledB.Pages.Where(page=>newKeys.Contains(page.ContentKey)),page=>Assert.InRange(page.Metadata.State.Y,0.999f,1.0f));
    }

    [Fact]
    public void ProjectedErrorSelectionAddsLevelsMonotonicallyDuringDescent()
    {
        ZyphosPlanetarySurfacePages.Reset();
        var center=ZyphosUmbrosSystem.ZyphosCenter;
        var direction=Vector3.Normalize(new Vector3(0.11f,0.17f,1.0f));
        var distances=new[]{1000.0f,100.0f,20.0f,ZyphosUmbrosSystem.ZyphosSurfaceRadius+0.05f};
        var levels=new List<int>();
        for(var index=0;index<distances.Length;index++)
        {
            var pages=ZyphosSceneBuilder.Build((float)index,(float)index-0.1f,center+direction*distances[index],default).PlanetarySurfacePages;
            levels.Add((int)pages.Pages.Where(page=>page.Metadata.Address.X==(float)CubeFace.PositiveZ).Max(page=>page.Metadata.Address.Y));
        }
        Assert.Equal(levels.Order(),levels);
        Assert.True(levels[^1]>levels[0]);
    }

    [Fact]
    public void ReloadReconstructsTheSameCutAndFadesResidualsFromItsParent()
    {
        ZyphosPlanetarySurfacePages.Reset();
        var center=ZyphosUmbrosSystem.ZyphosCenter;
        var camera=center+Vector3.Normalize(new Vector3(0.61f,-0.22f,0.76f))*(ZyphosUmbrosSystem.ZyphosSurfaceRadius+0.04f);
        var before=ZyphosSceneBuilder.Build(401.0f,400.0f,camera,default).PlanetarySurfacePages;
        var reloaded=ZyphosSceneBuilder.Build(0.0f,0.0f,camera,default).PlanetarySurfacePages;
        Assert.Equal(before.Pages.Select(page=>page.ContentKey),reloaded.Pages.Select(page=>page.ContentKey));
        Assert.Equal(before.ContentVersion,reloaded.ContentVersion);
        Assert.All(reloaded.Pages.Take(6),page=>Assert.Equal(1.0f,page.Metadata.State.Y));
        Assert.All(reloaded.Pages.Skip(6),page=>Assert.Equal(0.0f,page.Metadata.State.Y));
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
            var patchPath = Path.Combine(temporaryRoot, "D3D12ZyphosTerrainPatch.hlsl");
            Assert.False(D3D12ShaderCompiler.Compile(patchPath, "D3D12ZyphosTerrainPatchVS", "vs_5_0", skipOptimizationInDebug: false).IsEmpty);
            Assert.False(D3D12ShaderCompiler.Compile(patchPath, "D3D12ZyphosTerrainPatchPS", "ps_5_0", skipOptimizationInDebug: false).IsEmpty);
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
        Assert.All(output,s=>{Assert.True(float.IsFinite(s.HeightGradient.X));Assert.True(float.IsFinite(s.HeightGradient.Y));Assert.True(float.IsFinite(s.HeightGradient.Z));Assert.True(float.IsFinite(s.HeightGradient.W));Assert.InRange(s.Masks.X,0,1);Assert.InRange(s.Masks.Y,0,1);});
        var min=output.Min(s=>s.HeightGradient.X); var max=output.Max(s=>s.HeightGradient.X); var slope=output.Max(s=>new Vector3(s.HeightGradient.Y,s.HeightGradient.Z,s.HeightGradient.W).Length());
        Assert.True(min<=max); Assert.True(slope>=0); Assert.True(output.Max(s=>s.Masks.W)>=0);
        var shader=Path.Combine(RepositoryRoot(),"src","Aquarium.Zyphos","Shaders","D3D12ZyphosTerrainPageSummary.hlsl");
        var summary=D3D12ComputeProbe.Run<PageOutput,PageSummary>(shader,"D3D12ZyphosTerrainPageSummaryCS",output)[0];
        Assert.Equal(min,summary.Bounds.X); Assert.Equal(max,summary.Bounds.Y); Assert.Equal(slope,summary.Bounds.Z); Assert.Equal(output.Max(s=>s.Masks.W),summary.Bounds.W);
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
        var maximumSlope=page.Max(s=>new Vector3(s.HeightGradient.Y,s.HeightGradient.Z,s.HeightGradient.W).Length());
        for(var index=0;index<points.Length;index++)
        {
            var texel=points[index]*(request.InteriorSize-1)+new Vector2(request.BorderSize);
            var x=Math.Clamp((int)MathF.Floor(texel.X),0,request.StorageSize-2); var y=Math.Clamp((int)MathF.Floor(texel.Y),0,request.StorageSize-2); var f=texel-new Vector2(x,y);
            var interpolated=Lerp(Lerp(H(x,y),H(x+1,y),f.X),Lerp(H(x,y+1),H(x+1,y+1),f.X),f.Y);
            var directTravel=rayOriginRadius-(radius+direct[index].HeightGradient.X);
            var pageTravel=rayOriginRadius-(radius+interpolated);
            var declaredError=direct[index].Masks.W+maximumSlope*spacing*1.414214f;
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
