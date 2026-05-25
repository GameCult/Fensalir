using System.Numerics;

namespace Aquarium.Engine;

public readonly record struct ViewFrame(Vector2 Center, float Radius)
{
    private const float MinimumRadius = 20.0f;
    private const float MaximumRadius = 80.0f;
    private const float CameraDistanceScale = 2.0f;

    public AquariumCameraFrustum Frustum { get; init; } = AquariumCameraFrustum.Default;

    public static ViewFrame FromCamera(Vector2 target, float distance)
    {
        return new ViewFrame(target, Math.Clamp(distance * CameraDistanceScale, MinimumRadius, MaximumRadius));
    }
}

public readonly record struct AquariumCameraFrustum(
    float Left,
    float Right,
    float Bottom,
    float Top,
    float Near,
    float Far)
{
    public static AquariumCameraFrustum Default { get; } = new(-1.6f, 1.6f, -0.9f, 0.9f, 1.0f, 1000.0f);

    public AquariumCameraFrustum Normalized()
    {
        var near = MathF.Max(Near, 0.0001f);
        var far = MathF.Max(Far, near + 0.001f);
        var left = MathF.Min(Left, Right - 0.0001f);
        var right = MathF.Max(Right, left + 0.0001f);
        var bottom = MathF.Min(Bottom, Top - 0.0001f);
        var top = MathF.Max(Top, bottom + 0.0001f);
        return new AquariumCameraFrustum(left, right, bottom, top, near, far);
    }
}
