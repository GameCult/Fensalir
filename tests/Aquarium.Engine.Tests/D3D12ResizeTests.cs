namespace Aquarium.Engine.Tests;

public sealed class D3D12ResizeTests
{
    [Fact]
    public void ResizeReleasesOverlayAndProgramOutputReferencesBeforeResizeBuffers()
    {
        var repoRoot = FindRepoRoot();
        var renderer = File.ReadAllText(Path.Combine(repoRoot, "src", "Aquarium.Engine", "Render", "D3D12Renderer.cs"));

        var disposeOverlayIndex = renderer.IndexOf("private void DisposeBackBufferOverlays()", StringComparison.Ordinal);
        Assert.True(disposeOverlayIndex >= 0);
        var disposeOverlayEndIndex = renderer.IndexOf("private void ResizeIfNeeded", disposeOverlayIndex, StringComparison.Ordinal);
        Assert.True(disposeOverlayEndIndex > disposeOverlayIndex);
        var disposeOverlayBody = renderer[disposeOverlayIndex..disposeOverlayEndIndex];

        Assert.Contains("overlayContext.ClearState();", disposeOverlayBody);
        Assert.Contains("overlayContext.Flush();", disposeOverlayBody);

        var resizeIndex = renderer.IndexOf("private void ResizeIfNeeded", StringComparison.Ordinal);
        Assert.True(resizeIndex >= 0);
        var resizeBuffersIndex = renderer.IndexOf("swapChain.ResizeBuffers", resizeIndex, StringComparison.Ordinal);
        Assert.True(resizeBuffersIndex > resizeIndex);
        var preResizeBuffersBody = renderer[resizeIndex..resizeBuffersIndex];

        Assert.Contains("DisposeBackBufferOverlays();", preResizeBuffersBody);
        Assert.Contains("DisposeProgramOutputTexture();", preResizeBuffersBody);
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
