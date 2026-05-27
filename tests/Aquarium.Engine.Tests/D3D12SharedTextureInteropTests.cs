using System.Runtime.InteropServices;
using Aquarium.Engine.Render;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.Direct3D12;
using Vortice.DXGI;
using D3D11 = Vortice.Direct3D11.D3D11;
using D3D12 = Vortice.Direct3D12.D3D12;
using D3D11BindFlags = Vortice.Direct3D11.BindFlags;
using D3D11CpuAccessFlags = Vortice.Direct3D11.CpuAccessFlags;
using D3D11DeviceCreationFlags = Vortice.Direct3D11.DeviceCreationFlags;
using D3D11ResourceOptionFlags = Vortice.Direct3D11.ResourceOptionFlags;
using D3D11ResourceUsage = Vortice.Direct3D11.ResourceUsage;
using DxgiSharedResourceFlags = Vortice.DXGI.SharedResourceFlags;

namespace Aquarium.Engine.Tests;

public sealed class D3D12SharedTextureInteropTests
{
    [Fact]
    public void RegistryResolvesD3D11NtSharedBgraTextureAsD3D12Texture2D()
    {
        using var d3d11 = CreateD3D11Device();
        using var d3d12 = D3D12.D3D12CreateDevice<ID3D12Device>(IntPtr.Zero, FeatureLevel.Level_11_0);
        var description = new Texture2DDescription(
            Format.B8G8R8A8_UNorm,
            width: 16,
            height: 8,
            arraySize: 1,
            mipLevels: 1,
            bindFlags: D3D11BindFlags.ShaderResource | D3D11BindFlags.RenderTarget,
            usage: D3D11ResourceUsage.Default,
            cpuAccessFlags: D3D11CpuAccessFlags.None,
            sampleCount: 1,
            sampleQuality: 0,
            miscFlags: D3D11ResourceOptionFlags.SharedNTHandle | D3D11ResourceOptionFlags.SharedKeyedMutex);
        using var texture = d3d11.CreateTexture2D(description);
        using var sharedResource = texture.QueryInterface<IDXGIResource1>();
        var sharedHandle = sharedResource.CreateSharedHandle(null, DxgiSharedResourceFlags.Read | DxgiSharedResourceFlags.Write, null);

        try
        {
            var declaration = new AquariumFieldResourceDeclaration(
                ResourceKey: "test:shared-d3d11-texture:bgra",
                Kind: AquariumFieldResourceKind.Texture2D,
                Residency: AquariumFieldResourceResidency.SharedGpu,
                Access: AquariumFieldShaderAccess.ShaderResource,
                Format: "Bgra8",
                Width: 16,
                Height: 8,
                DepthOrCount: 1,
                StrideBytes: 64,
                ValidFromNs: 0,
                ValidUntilNs: 0,
                Version: 1,
                NativeHandle: sharedHandle,
                NativeHandleKind: "shared-d3d11-texture");

            using var registry = new D3D12FieldResourceRegistry();
            var resources = new D3D12ResourceRegistry();
            var stats = registry.Resolve(d3d12, resources, [declaration]);

            Assert.Equal(1, stats.Declared);
            Assert.Equal(1, stats.Texture2D);
            Assert.Equal(1, stats.Resolved);
            Assert.Equal(0, stats.Unsupported);
            Assert.True(registry.TryGetTexture2D(declaration.ResourceKey, out var resolved));
            Assert.Equal(16, resolved.Width);
            Assert.Equal(8, resolved.Height);
            Assert.Equal(Format.B8G8R8A8_UNorm, resolved.Format);

            var nextFrameStats = registry.Resolve(d3d12, resources, [declaration with { Version = 2 }]);

            Assert.Equal(1, nextFrameStats.Resolved);
            Assert.Equal(0, nextFrameStats.Unsupported);
            Assert.Equal(0, nextFrameStats.StaleRemoved);
            Assert.True(registry.TryGetTexture2D(declaration.ResourceKey, out var nextFrameResolved));
            Assert.Same(resolved, nextFrameResolved);
        }
        finally
        {
            CloseHandle(sharedHandle);
        }
    }

    private static ID3D11Device CreateD3D11Device()
    {
        return D3D11.D3D11CreateDevice(
            DriverType.Hardware,
            D3D11DeviceCreationFlags.BgraSupport,
            FeatureLevel.Level_11_0);
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);
}
