using Aquarium.Engine.Render;
using Vortice.DXGI;

namespace Aquarium.Engine.Tests;

public sealed class D3D12FieldTextureFormatTests
{
    [Theory]
    [InlineData("Bgra8", Format.B8G8R8A8_UNorm)]
    [InlineData("Gray8", Format.R8_UNorm)]
    [InlineData("R8", Format.R8_UNorm)]
    [InlineData("Rg8", Format.R8G8_UNorm)]
    [InlineData("LeapStereoIr", Format.R8G8_UNorm)]
    [InlineData("Yuy2", Format.YUY2)]
    [InlineData("Nv12", Format.NV12)]
    public void FormatParserAcceptsMimirVideoResourceFormats(string sourceFormat, Format expected)
    {
        Assert.True(D3D12FieldTextureFormat.TryFormat(sourceFormat, out var actual));
        Assert.Equal(expected, actual);
    }
}
