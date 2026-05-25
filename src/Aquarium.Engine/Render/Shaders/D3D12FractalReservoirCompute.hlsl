struct FractalSdfSplat
{
    float4 centerRadius;
    float4 orientation;
    float4 radiiFalloff;
    float4 materialConfidence;
    float4 key;
};

struct SdfEnvelopeReservoir
{
    float4 centerRadius;
    float4 radiiFalloff;
    float4 weightTargetCount;
    float4 validation;
};

struct PbrMaterialReservoir
{
    float4 baseColorRoughMetal;
    float4 normalVariance;
    float4 weightTargetCount;
    float4 validation;
};

struct RadiosityReservoir
{
    float4 radianceDistance;
    float4 directionOcclusion;
    float4 weightTargetCount;
    float4 validation;
};

struct FractalIfsTransform
{
    float4 offsetScaleAmplitude;
    float4 radiiRotationFalloff;
    float4 materialSeedShape;
    float4 tileAddress;
    float4 postMatrix;
    float4 postTranslation;
};

struct TextureSplineFieldProgram
{
    float4 dimensionsAxisMode;
    float4 axisModeOffset;
    float4 columnModulo;
    float4 subdivisionProbe;
    float4 originAmplitude;
    float4 axisStepRadius;
    float4 columnStepAlpha;
    float4 emission;
    float4 surfaceWeights0;
    float4 surfaceWeights1;
};

struct FlameIterationState
{
    float4 pointSupportMaterial;
    float4 randomStep;
};

cbuffer ReceiptConstants : register(b0)
{
    uint SplatCount;
    uint FrameIndex;
    uint Depth;
    uint Seed;
    uint CandidatesPerPass;
    uint ReservoirUpdatesPerPass;
    uint ProgramTransformCount;
    uint ProgramMode;
    uint SplatDispatchCount;
    float PriorityFocusX;
    float PriorityFocusY;
    float PriorityFocusRadius;
    float PriorityFocusStrength;
};

RWStructuredBuffer<FractalSdfSplat> Splats : register(u0);
RWStructuredBuffer<SdfEnvelopeReservoir> SdfReservoirs : register(u1);
RWStructuredBuffer<PbrMaterialReservoir> PbrReservoirs : register(u2);
RWStructuredBuffer<RadiosityReservoir> RadiosityReservoirs : register(u3);
RWStructuredBuffer<FlameIterationState> FlameStates : register(u4);
StructuredBuffer<FractalIfsTransform> ProgramTransforms : register(t0);
StructuredBuffer<TextureSplineFieldProgram> TextureSplinePrograms : register(t1);
StructuredBuffer<float> TextureFieldSamples : register(t2);

static const float FIELD_ENCODING_SIGNED_DISTANCE = 0.0;
static const float FIELD_ENCODING_DENSITY = 2.0;

uint Hash(uint x)
{
    x ^= x >> 16;
    x *= 747796405u;
    x ^= x >> 16;
    x *= 2891336453u;
    x ^= x >> 16;
    return x;
}

float Random01(uint value)
{
    return (float)(Hash(value) & 16777215u) / 16777216.0;
}

uint XorShift(inout uint state)
{
    uint value = state == 0u ? 0xA341316Cu : state;
    value ^= value << 13;
    value ^= value >> 17;
    value ^= value << 5;
    state = value == 0u ? 0xA341316Cu : value;
    return state;
}

float StateRandom01(inout uint state)
{
    return (float)(XorShift(state) & 16777215u) / 16777216.0;
}

float2 CubeTileFaceUv(float2 authoredPoint, float4 tileAddress)
{
    float level = max(tileAddress.y, 0.0);
    float axisTileCount = exp2(level);
    float2 tile = tileAddress.zw;
    float2 local01 = saturate((authoredPoint / 32.0) * 0.5 + 0.5);
    return -1.0 + 2.0 * ((tile + local01) / max(axisTileCount, 1.0));
}

float3 CubeSphereDirection(float face, float2 uv)
{
    uint f = (uint)round(face);
    if (f == 0u)
    {
        return normalize(float3(1.0, uv.y, -uv.x));
    }

    if (f == 1u)
    {
        return normalize(float3(-1.0, uv.y, uv.x));
    }

    if (f == 2u)
    {
        return normalize(float3(uv.x, 1.0, -uv.y));
    }

    if (f == 3u)
    {
        return normalize(float3(uv.x, -1.0, uv.y));
    }

    if (f == 5u)
    {
        return normalize(float3(uv.x, uv.y, -1.0));
    }

    return normalize(float3(uv.x, uv.y, 1.0));
}

float3 FractalPoint(uint index, out float radius, out float fieldEncoding)
{
    if (ProgramTransformCount > 0u && ProgramMode == 3u)
    {
        uint programIndex = index % ProgramTransformCount;
        TextureSplineFieldProgram program = TextureSplinePrograms[programIndex];
        uint localIndex = index / ProgramTransformCount;
        uint width = max((uint)round(program.dimensionsAxisMode.y), 1u);
        uint height = max((uint)round(program.dimensionsAxisMode.z), 1u);
        uint channels = max((uint)round(program.dimensionsAxisMode.w), 1u);
        uint frequencyAxis = (uint)round(program.axisModeOffset.x);
        uint rollingMode = (uint)round(program.axisModeOffset.y);
        uint rollingOffset = (uint)round(program.axisModeOffset.z);
        uint sampleOffset = (uint)round(program.axisModeOffset.w);
        uint firstColumn = (uint)max(round(program.columnModulo.x), 0.0);
        uint columnCount = max((uint)round(program.columnModulo.y), 1u);
        uint columnStride = max((uint)round(program.columnModulo.z), 1u);
        uint rollingModulo = (uint)max(round(program.columnModulo.w), 0.0);
        uint axisSamples = frequencyAxis == 0u ? width : height;
        uint column = (localIndex / axisSamples) % columnCount;
        uint frequencyIndex = localIndex % axisSamples;
        uint textureColumn = firstColumn + column * columnStride;
        if (rollingModulo > 0u)
        {
            textureColumn = (textureColumn + rollingOffset) % rollingModulo;
        }

        uint x = frequencyAxis == 0u ? frequencyIndex : textureColumn;
        uint y = frequencyAxis == 0u ? textureColumn : frequencyIndex;
        if (rollingMode == 1u)
        {
            x = (x + rollingOffset) % width;
        }
        else if (rollingMode == 2u)
        {
            y = (y + rollingOffset) % height;
        }

        x = min(x, width - 1u);
        y = min(y, height - 1u);
        uint sampleIndex = sampleOffset + ((y * width + x) * channels);
        float amplitude = TextureFieldSamples[sampleIndex];
        uint h = Hash(index + FrameIndex * 1664525u + asuint(program.surfaceWeights1.z));
        float jitter = (Random01(h) - 0.5) / max((float)axisSamples, 1.0);
        float t = ((float)frequencyIndex + 0.5 + jitter) / max((float)axisSamples, 1.0);
        float c = ((float)column + 0.5) / max((float)columnCount, 1.0);
        float3 p = program.originAmplitude.xyz +
            program.axisStepRadius.xyz * ((float)frequencyIndex + jitter) +
            program.columnStepAlpha.xyz * (float)column +
            float3(0.0, amplitude * program.originAmplitude.w, 0.0);

        float neighbor = TextureFieldSamples[sampleOffset + ((y * width + min(x + 1u, width - 1u)) * channels)];
        float derivative = neighbor - amplitude;
        float visualContribution = saturate(abs(amplitude) + abs(derivative) * program.surfaceWeights0.w);
        radius = max(program.axisStepRadius.w * (0.55 + visualContribution * 0.90), 0.0002);
        fieldEncoding = FIELD_ENCODING_DENSITY;
        return p + float3(0.0, 0.0, (Random01(h + 31u) - 0.5) * radius * 0.35 + t * 0.0 + c * 0.0);
    }

    if (ProgramTransformCount > 0u && ProgramMode == 2u)
    {
        fieldEncoding = FIELD_ENCODING_DENSITY;
        FlameIterationState state = FlameStates[index];
        uint n = asuint(state.randomStep.x);
        float step = state.randomStep.y;
        if (FrameIndex == 0u || n == 0u)
        {
            n = Hash(index ^ Seed);
            state.pointSupportMaterial = float4(0.0, 0.0, 1.0, 0.0);
            step = 0.0;
        }

        float2 p = state.pointSupportMaterial.xy;
        float support = max(state.pointSupportMaterial.z, 0.000001);
        float material = state.pointSupportMaterial.w;
        float totalWeight = 0.0;
        [loop]
        for (uint weightIndex = 0u; weightIndex < ProgramTransformCount; weightIndex++)
        {
            totalWeight += max(ProgramTransforms[weightIndex].materialSeedShape.y, 0.0);
        }

        [loop]
        for (uint depth = 0; depth < Depth; depth++)
        {
            float target = StateRandom01(n) * max(totalWeight, 0.000001);
            float cumulative = 0.0;
            uint transformIndex = ProgramTransformCount - 1u;
            [loop]
            for (uint candidateIndex = 0u; candidateIndex < ProgramTransformCount; candidateIndex++)
            {
                cumulative += max(ProgramTransforms[candidateIndex].materialSeedShape.y, 0.0);
                if (target <= cumulative)
                {
                    transformIndex = candidateIndex;
                    break;
                }
            }

            FractalIfsTransform transform = ProgramTransforms[transformIndex];
            float4 m = transform.offsetScaleAmplitude;
            float2 t = transform.radiiRotationFalloff.xy;
            float2 affine = float2((m.x * p.x) + (m.y * p.y) + t.x, (m.z * p.x) + (m.w * p.y) + t.y);
            float r2 = dot(affine, affine);
            float r = sqrt(max(r2, 0.0));
            float theta = atan2(affine.y, affine.x);
            float2 nextPoint = affine * transform.radiiRotationFalloff.z;
            nextPoint += affine * (transform.radiiRotationFalloff.w / max(r2, 0.000001));
            nextPoint += affine * (transform.materialSeedShape.x * 4.0 / (r2 + 4.0));
            float disc = transform.tileAddress.x;
            if (disc != 0.0)
            {
                float discTheta = disc * atan2(affine.x, affine.y) / 3.14159265358979323846;
                nextPoint += float2(sin(3.14159265358979323846 * r), cos(3.14159265358979323846 * r)) * discTheta;
            }

            float julian = transform.tileAddress.y;
            if (julian != 0.0)
            {
                float power = abs(transform.tileAddress.z) < 1.0 ? 1.0 : transform.tileAddress.z;
                float absPower = max(abs(power), 1.0);
                float branch = floor(StateRandom01(n) * absPower);
                float angle = (theta + 6.28318530717958647692 * branch) / power;
                float radial = pow(max(r, 0.000001), transform.materialSeedShape.w / power);
                nextPoint += julian * radial * float2(cos(angle), sin(angle));
            }

            float blur = transform.tileAddress.w;
            if (blur != 0.0)
            {
                float blurRadius = blur * (
                    StateRandom01(n) +
                    StateRandom01(n) +
                    StateRandom01(n) +
                    StateRandom01(n) - 2.0);
                float blurAngle = StateRandom01(n) * 6.28318530717958647692;
                nextPoint += blurRadius * float2(cos(blurAngle), sin(blurAngle));
            }

            float4 post = transform.postMatrix;
            float2 postT = transform.postTranslation.xy;
            nextPoint = float2(
                (post.x * nextPoint.x) + (post.y * nextPoint.y) + postT.x,
                (post.z * nextPoint.x) + (post.w * nextPoint.y) + postT.y);
            p = nextPoint;
            material = transform.materialSeedShape.z;
            support *= saturate(max(length(m.xy), length(m.zw)));
        }

        FlameIterationState nextState;
        nextState.pointSupportMaterial = float4(p, support, material);
        nextState.randomStep = float4(asfloat(n), step + (float)Depth, 0.0, 0.0);
        FlameStates[index] = nextState;
        radius = max(0.0025 * max(support, 0.04), 0.00015);
        return float3(p, material * 0.08);
    }

    if (ProgramTransformCount > 0u && ProgramMode == 1u)
    {
        uint h = Hash(index ^ Seed);
        uint transformIndex = h % ProgramTransformCount;
        FractalIfsTransform transform = ProgramTransforms[transformIndex];
        fieldEncoding = transform.postTranslation.z;
        float rx = Random01(h + FrameIndex * 17u) * 2.0 - 1.0;
        float ry = Random01(h + 7919u) * 2.0 - 1.0;
        float c = transform.materialSeedShape.z;
        float s = transform.materialSeedShape.w;
        float tileScale = 1.0 / max(exp2(max(transform.tileAddress.y, 0.0)), 1.0);
        float2 local = float2(rx * transform.radiiRotationFalloff.x, ry * transform.radiiRotationFalloff.y) * 0.65;
        float2 rotated = float2((local.x * c) - (local.y * s), (local.x * s) + (local.y * c));
        radius = max(max(transform.radiiRotationFalloff.x, transform.radiiRotationFalloff.y) * (1.0 / 32.0) * tileScale * (0.12 + Random01(h + 104729u) * 0.04), 0.0004);
        float relief = transform.offsetScaleAmplitude.w * 0.02 + (Random01(h + 1299721u) - 0.5) * radius * 0.5;
        float2 surfacePoint = transform.offsetScaleAmplitude.xy + rotated;
        float2 faceUv = CubeTileFaceUv(surfacePoint, transform.tileAddress);
        float3 dir = CubeSphereDirection(transform.tileAddress.x, faceUv);
        return dir * (1.0 + relief + transform.materialSeedShape.x * 0.012);
    }

    if (ProgramTransformCount > 0u)
    {
        uint n = index ^ Seed;
        float2 p = 0.0;
        float z = 0.0;
        float scale = 1.0;
        float material = 0.0;
        fieldEncoding = FIELD_ENCODING_SIGNED_DISTANCE;
        radius = 0.01;
        [loop]
        for (uint depth = 0; depth < Depth; depth++)
        {
            uint transformIndex = Hash(n + depth * 747796405u) % ProgramTransformCount;
            FractalIfsTransform transform = ProgramTransforms[transformIndex];
            float c = transform.materialSeedShape.z;
            float s = transform.materialSeedShape.w;
            float2 rotated = float2((p.x * c) - (p.y * s), (p.x * s) + (p.y * c));
            float childScale = saturate(transform.offsetScaleAmplitude.z);
            p = rotated * max(childScale, 0.01) + transform.offsetScaleAmplitude.xy;
            scale *= max(childScale, 0.01);
            z += transform.offsetScaleAmplitude.w * scale;
            radius = max(max(transform.radiiRotationFalloff.x, transform.radiiRotationFalloff.y) * max(scale, 0.001), 0.0001);
            material = transform.materialSeedShape.x;
            n = Hash(n + asuint(transform.materialSeedShape.y) + transformIndex + depth);
        }

        return float3(p, z + material * 0.05);
    }

    uint n = index ^ Seed;
    float3 p = 0.0;
    float scale = 1.0;
    fieldEncoding = FIELD_ENCODING_SIGNED_DISTANCE;
    [loop]
    for (uint depth = 0; depth < Depth; depth++)
    {
        uint branch = (n >> (depth * 2u)) & 3u;
        float2 dir = branch == 0u ? float2(1.0, 0.0) : branch == 1u ? float2(-0.42, 0.91) : branch == 2u ? float2(-0.76, -0.65) : float2(0.72, -0.69);
        scale *= 0.535;
        p.xy += dir * scale;
        p.z += ((float)branch - 1.5) * scale * 0.19;
        n = Hash(n + branch + FrameIndex + depth * 17u);
    }

    radius = max(scale * 0.75, 0.0001);
    return p;
}

float ReservoirPriorityScore(uint index, uint salt, float radius, float strength)
{
    FractalSdfSplat splat = Splats[index];
    float2 delta = splat.centerRadius.xy - float2(PriorityFocusX, PriorityFocusY);
    float nearFocus = 1.0 - smoothstep(radius, radius * 2.0, length(delta));
    float exploration = Random01(salt);
    return lerp(exploration, nearFocus + exploration * 0.15, strength);
}

uint ReservoirIndex(uint updateIndex, uint passKind)
{
    if (ReservoirUpdatesPerPass >= SplatCount)
    {
        return updateIndex % SplatCount;
    }

    uint baseSalt = updateIndex * 1664525u + FrameIndex * 1013904223u + passKind * 747796405u + Seed;
    uint bestIndex = Hash(baseSalt) % SplatCount;
    float strength = saturate(PriorityFocusStrength);
    float radius = max(PriorityFocusRadius, 0.0);
    if (strength <= 0.0 || radius <= 0.0)
    {
        return bestIndex;
    }

    float bestScore = ReservoirPriorityScore(bestIndex, Hash(baseSalt + 17u), radius, strength);
    [unroll]
    for (uint probe = 1u; probe < 4u; probe++)
    {
        uint candidateSalt = Hash(baseSalt + probe * 747796405u);
        uint candidateIndex = candidateSalt % SplatCount;
        float candidateScore = ReservoirPriorityScore(candidateIndex, candidateSalt, radius, strength);
        if (candidateScore > bestScore)
        {
            bestScore = candidateScore;
            bestIndex = candidateIndex;
        }
    }

    return bestIndex;
}

float4 ReservoirStats(uint index, uint passKind, float baseTarget, out uint selectedCandidate)
{
    float weightSum = 0.0;
    float selectedTarget = 0.0;
    selectedCandidate = 0u;
    [loop]
    for (uint candidate = 0u; candidate < CandidatesPerPass; candidate++)
    {
        float phase = Random01(index * 1664525u + passKind * 1013904223u + candidate * 747796405u + FrameIndex);
        float target = max(baseTarget * (0.55 + phase), 0.000001);
        float weight = target * max((float)CandidatesPerPass, 1.0);
        float nextWeightSum = weightSum + weight;
        if (weightSum <= 0.0 || Random01(index + candidate * 13007u + passKind * 7919u) < weight / max(nextWeightSum, 0.000001))
        {
            selectedTarget = target;
            selectedCandidate = candidate;
        }

        weightSum = nextWeightSum;
    }

    float contribution = weightSum / max((float)CandidatesPerPass * selectedTarget, 0.000001);
    return float4(weightSum, selectedTarget, (float)CandidatesPerPass, contribution);
}

[numthreads(256, 1, 1)]
void D3D12FractalSplatReceiptCS(uint3 id : SV_DispatchThreadID)
{
    uint updateIndex = id.x;
    if (updateIndex >= SplatDispatchCount)
    {
        return;
    }

    uint index = updateIndex;
    if (FrameIndex > 0u && SplatDispatchCount < SplatCount)
    {
        index = ReservoirIndex(updateIndex, 4u);
    }

    float radius;
    float fieldEncoding;
    float3 p = FractalPoint(index, radius, fieldEncoding);
    uint h = Hash(index + FrameIndex * 1664525u + Seed);
    FractalSdfSplat splat;
    splat.centerRadius = float4(p, radius);
    splat.orientation = float4(0.0, 0.0, 0.0, 1.0);
    splat.radiiFalloff = float4(radius, radius * 0.72, radius * 0.45, 4.0);
    float material = ProgramMode == 3u
        ? saturate(abs(p.y) * 0.7 + radius * 18.0 + Random01(h) * 0.15)
        : (float)(h & 1023u) / 1023.0;
    splat.materialConfidence = float4(material, 1.0, fieldEncoding, 1.0);
    splat.key = float4((float)index, (float)FrameIndex, (float)Depth, asfloat(h));
    Splats[index] = splat;
}

[numthreads(256, 1, 1)]
void D3D12SdfEnvelopeReservoirCS(uint3 id : SV_DispatchThreadID)
{
    uint updateIndex = id.x;
    if (updateIndex >= ReservoirUpdatesPerPass)
    {
        return;
    }

    uint index = ReservoirIndex(updateIndex, 0u);
    FractalSdfSplat splat = Splats[index];
    uint selected;
    float4 stats = ReservoirStats(index, 0u, splat.centerRadius.w, selected);
    SdfEnvelopeReservoir r;
    r.centerRadius = splat.centerRadius;
    r.radiiFalloff = splat.radiiFalloff;
    r.weightTargetCount = stats;
    r.validation = float4(saturate(stats.y), (float)FrameIndex, (float)selected, asfloat(Hash(index + selected)));
    SdfReservoirs[index] = r;
}

[numthreads(256, 1, 1)]
void D3D12PbrMaterialReservoirCS(uint3 id : SV_DispatchThreadID)
{
    uint updateIndex = id.x;
    if (updateIndex >= ReservoirUpdatesPerPass)
    {
        return;
    }

    uint index = ReservoirIndex(updateIndex, 1u);
    FractalSdfSplat splat = Splats[index];
    uint selected;
    float4 stats = ReservoirStats(index, 1u, splat.materialConfidence.x + 0.1, selected);
    PbrMaterialReservoir r;
    r.baseColorRoughMetal = float4(splat.materialConfidence.x, 1.0 - splat.materialConfidence.x * 0.4, 0.18 + 0.5 * splat.materialConfidence.x, 0.02);
    r.normalVariance = float4(normalize(splat.centerRadius.xyz + 0.001), splat.radiiFalloff.x);
    r.weightTargetCount = stats;
    r.validation = float4(saturate(stats.y), (float)FrameIndex, (float)selected, asfloat(Hash(index + selected + 17u)));
    PbrReservoirs[index] = r;
}

[numthreads(256, 1, 1)]
void D3D12RadiosityReservoirCS(uint3 id : SV_DispatchThreadID)
{
    uint updateIndex = id.x;
    if (updateIndex >= ReservoirUpdatesPerPass)
    {
        return;
    }

    uint index = ReservoirIndex(updateIndex, 2u);
    FractalSdfSplat splat = Splats[index];
    uint selected;
    float energy = abs(cos(length(splat.centerRadius.xyz) * 7.0 + (float)FrameIndex * 0.01));
    float4 stats = ReservoirStats(index, 2u, energy + 0.05, selected);
    RadiosityReservoir r;
    r.radianceDistance = float4(energy, energy * 0.62, energy * 0.31, length(splat.centerRadius.xyz));
    r.directionOcclusion = float4(normalize(-splat.centerRadius.xyz + 0.01), 1.0 - energy * 0.35);
    r.weightTargetCount = stats;
    r.validation = float4(saturate(stats.y), (float)FrameIndex, (float)selected, asfloat(Hash(index + selected + 29u)));
    RadiosityReservoirs[index] = r;
}
