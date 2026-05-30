cbuffer D3D12StereoDepthConstants : register(b0)
{
    float4 Shape;  // width, height, minDisparity, disparityLevels
    float4 Match;  // censusRadius, aggregationPathCount, p1, p2
    float4 Output; // invDisparityScale, reserved
};

Texture2D<float2> PackedStereoIr : register(t28);
RWTexture2D<float> DisparityOut : register(u0);

float PackedLeft(uint2 pixel)
{
    return PackedStereoIr.Load(int3(pixel, 0)).r;
}

float PackedRight(uint2 pixel)
{
    return PackedStereoIr.Load(int3(pixel, 0)).g;
}

[numthreads(8, 8, 1)]
void D3D12PackedStereoDepthCS(uint3 dispatchThreadId : SV_DispatchThreadID)
{
    const uint width = max(1u, (uint)Shape.x);
    const uint height = max(1u, (uint)Shape.y);
    const uint2 pixel = dispatchThreadId.xy;
    if (pixel.x >= width || pixel.y >= height)
    {
        return;
    }

    const int minDisparity = max(0, (int)Shape.z);
    const int disparityLevels = max(1, min(256, (int)Shape.w));
    const int radius = max(1, min(4, (int)Match.x));
    const int maxDisparity = min(disparityLevels - 1, (int)pixel.x - radius - minDisparity);
    if (maxDisparity <= 0 || pixel.y < (uint)radius || pixel.y + (uint)radius >= height)
    {
        DisparityOut[pixel] = 0.0f;
        return;
    }

    float bestCost = 1.0e20f;
    int bestDisparity = 0;
    [loop]
    for (int disparity = minDisparity; disparity <= maxDisparity; disparity++)
    {
        float cost = 0.0f;
        [loop]
        for (int y = -radius; y <= radius; y++)
        {
            [loop]
            for (int x = -radius; x <= radius; x++)
            {
                const uint2 leftPixel = uint2((uint)((int)pixel.x + x), (uint)((int)pixel.y + y));
                const uint2 rightPixel = uint2((uint)((int)pixel.x + x - disparity), (uint)((int)pixel.y + y));
                cost += abs(PackedLeft(leftPixel) - PackedRight(rightPixel));
            }
        }

        if (cost < bestCost)
        {
            bestCost = cost;
            bestDisparity = disparity;
        }
    }

    DisparityOut[pixel] = (float)bestDisparity;
}
