using CultMath;

namespace Aquarium.Engine.Fractal.Tests;

public sealed class PlanetarySurfacePageTests
{
    [Fact]
    public void SiblingPagesShareBitStableBoundaryDirections()
    {
        var left = new PlanetaryPageLayout(new PlanetaryTileAddress(PlanetaryCubeFace.PositiveZ, 1, 0, 0), 17, 2);
        var right = new PlanetaryPageLayout(new PlanetaryTileAddress(PlanetaryCubeFace.PositiveZ, 1, 1, 0), 17, 2);
        for (var y = 0; y < left.InteriorSize; y++)
        {
            var a = PlanetaryPageSampling.Direction(left, left.BorderSize + left.InteriorSize - 1, left.BorderSize + y);
            var b = PlanetaryPageSampling.Direction(right, right.BorderSize, right.BorderSize + y);
            Assert.Equal(a, b);
        }
    }

    [Fact]
    public void BorderSamplesExtendPastTileWithoutNeighborResidency()
    {
        var page = new PlanetaryPageLayout(new PlanetaryTileAddress(PlanetaryCubeFace.PositiveX, 0, 0, 0), 17, 3);
        var edge = PlanetaryPageSampling.Direction(page, page.BorderSize + page.InteriorSize - 1, page.BorderSize + 8);
        var border = PlanetaryPageSampling.Direction(page, page.StorageSize - 1, page.BorderSize + 8);
        Assert.NotEqual(edge, border);
        Assert.Equal(1.0f, math.length(border), precision: 5);
    }

    [Fact]
    public void AngularTexelSizeShrinksWithQuadtreeLevel()
    {
        var root = new PlanetaryPageLayout(new PlanetaryTileAddress(PlanetaryCubeFace.PositiveZ, 0, 0, 0), 129, 4);
        var child = new PlanetaryPageLayout(new PlanetaryTileAddress(PlanetaryCubeFace.PositiveZ, 1, 0, 0), 129, 4);
        Assert.True(PlanetaryPageSampling.AngularTexelSize(child) < PlanetaryPageSampling.AngularTexelSize(root));
    }
}
