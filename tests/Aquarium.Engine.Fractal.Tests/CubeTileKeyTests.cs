using CultMath;
using GameCult.Geometry;

namespace Aquarium.Engine.Fractal.Tests;

public sealed class PlanetaryTileAddressTests
{
    [Fact]
    public void PositionAtMapsTileLocalCoordinatesIntoFaceCoordinates()
    {
        var key = new PlanetaryTileAddress(PlanetaryCubeFace.PositiveZ, 2, 1, 2);

        var lowerLeft = key.PositionAt(0.0, 0.0);
        var upperRight = key.PositionAt(1.0, 1.0);

        Assert.Equal(-0.5, lowerLeft.U, 12);
        Assert.Equal(0.0, lowerLeft.V, 12);
        Assert.Equal(0.0, upperRight.U, 12);
        Assert.Equal(0.5, upperRight.V, 12);
    }

    [Fact]
    public void ChildAndParentPreserveQuadtreeAddress()
    {
        var root = new PlanetaryTileAddress(PlanetaryCubeFace.NegativeY, 0, 0, 0);
        var child = root.Child(1, 0).Child(0, 1);

        Assert.Equal(new PlanetaryTileAddress(PlanetaryCubeFace.NegativeY, 2, 2, 1), child);
        Assert.Equal(new PlanetaryTileAddress(PlanetaryCubeFace.NegativeY, 1, 1, 0), child.Parent());
        Assert.Equal("NegativeY/L02/2/1", child.ToString());
    }
}
