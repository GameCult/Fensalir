using Vortice.DXGI;

namespace Aquarium.Engine.Render;

internal static class D3D12FieldTextureFormat
{
    public static bool TryFormat(string formatName, out Format format)
    {
        format = formatName switch
        {
            "Bayer8" or "Gray8" or "R8" or "R8Unorm" or "R8_UNorm" or "R8_UNORM" => Format.R8_UNorm,
            "R16Float" or "R16_Float" or "R16_FLOAT" => Format.R16_Float,
            "R32Float" or "R32_Float" or "R32_FLOAT" or "Float32" => Format.R32_Float,
            "Rg8" or "Rg8Unorm" or "R8G8_UNorm" or "R8G8_UNORM" or "LeapStereoIr" or "LeapPackedMap" => Format.R8G8_UNorm,
            "Bgra8" or "Bgra8Unorm" or "B8G8R8A8_UNorm" or "B8G8R8A8_UNORM" => Format.B8G8R8A8_UNorm,
            "Rgba8Unorm" or "R8G8B8A8_UNorm" or "R8G8B8A8_UNORM" => Format.R8G8B8A8_UNorm,
            "Rgba16Float" or "R16G16B16A16_Float" or "R16G16B16A16_FLOAT" => Format.R16G16B16A16_Float,
            "Yuy2" or "YUY2" => Format.YUY2,
            "Nv12" or "NV12" => Format.NV12,
            _ => Format.Unknown,
        };
        return format != Format.Unknown;
    }
}
