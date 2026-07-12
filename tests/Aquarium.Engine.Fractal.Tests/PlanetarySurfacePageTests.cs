using Aquarium.Engine.Fractal.Lod;

namespace Aquarium.Engine.Fractal.Tests;

public sealed class PlanetarySurfacePageTests
{
    [Fact]
    public void SiblingPagesShareBitStableBoundaryDirections()
    {
        var left = new PlanetarySurfacePageRequest(new CubeTileKey(CubeFace.PositiveZ, 1, 0, 0), 17, 2);
        var right = new PlanetarySurfacePageRequest(new CubeTileKey(CubeFace.PositiveZ, 1, 1, 0), 17, 2);
        for (var y = 0; y < left.InteriorSize; y++)
        {
            var a = PlanetarySurfacePageSampling.Direction(left, left.BorderSize + left.InteriorSize - 1, left.BorderSize + y);
            var b = PlanetarySurfacePageSampling.Direction(right, right.BorderSize, right.BorderSize + y);
            Assert.Equal(a, b);
        }
    }

    [Fact]
    public void BorderSamplesExtendPastTileWithoutNeighborResidency()
    {
        var page = new PlanetarySurfacePageRequest(new CubeTileKey(CubeFace.PositiveX, 0, 0, 0), 17, 3);
        var edge = PlanetarySurfacePageSampling.Direction(page, page.BorderSize + page.InteriorSize - 1, page.BorderSize + 8);
        var border = PlanetarySurfacePageSampling.Direction(page, page.StorageSize - 1, page.BorderSize + 8);
        Assert.NotEqual(edge, border);
        Assert.Equal(1.0f, border.Length(), precision: 5);
    }

    [Fact]
    public void AngularTexelSizeShrinksWithQuadtreeLevel()
    {
        var root = new PlanetarySurfacePageRequest(new CubeTileKey(CubeFace.PositiveZ, 0, 0, 0), 129, 4);
        var child = new PlanetarySurfacePageRequest(new CubeTileKey(CubeFace.PositiveZ, 1, 0, 0), 129, 4);
        Assert.True(PlanetarySurfacePageSampling.AngularTexelSize(child) < PlanetarySurfacePageSampling.AngularTexelSize(root));
    }
}
