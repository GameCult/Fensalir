namespace Aquarium.Engine.Render;

public static class AquariumBuiltInFieldResources
{
    public const string BlackbodyRampResourceKey = "aquarium:resource:ramp:blackbody";
    public const string BlackbodyRampRelativePath = "Assets/Textures/Gradients/blackbody.png";

    public static string ResolveBlackbodyRampPath(string? baseDirectory = null) =>
        System.IO.Path.Combine(
            baseDirectory ?? System.AppContext.BaseDirectory,
            "Assets",
            "Textures",
            "Gradients",
            "blackbody.png");

    public static AquariumFieldResourceDeclaration BlackbodyRamp(ulong version = 1, string? baseDirectory = null) =>
        AquariumFieldResourceDeclaration.LocalTexture2D(
            BlackbodyRampResourceKey,
            ResolveBlackbodyRampPath(baseDirectory),
            version: version);
}
