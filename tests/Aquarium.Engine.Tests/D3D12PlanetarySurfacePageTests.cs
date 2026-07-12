using System.Numerics;
using System.Runtime.InteropServices;
using Aquarium.Engine.Fractal;
using Aquarium.Engine.Fractal.Lod;
using Aquarium.Engine.Render;

namespace Aquarium.Engine.Tests;

public sealed class D3D12PlanetarySurfacePageTests
{
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

    private static string RepositoryRoot(){var d=new DirectoryInfo(AppContext.BaseDirectory);while(d is not null&&!File.Exists(Path.Combine(d.FullName,"Fensalir.sln")))d=d.Parent;return d?.FullName??throw new InvalidOperationException();}
    [StructLayout(LayoutKind.Sequential)] private readonly record struct PageInput(Vector4 DirectionRadius,Vector4 Sampling);
    [StructLayout(LayoutKind.Sequential)] private readonly record struct PageOutput(Vector4 HeightGradient,Vector4 Masks);
    [StructLayout(LayoutKind.Sequential)] private readonly record struct PageSummary(Vector4 Bounds,Vector4 Metadata);
}
