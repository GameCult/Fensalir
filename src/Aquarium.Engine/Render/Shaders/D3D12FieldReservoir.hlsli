struct FieldReservoirSample
{
    float4 colorTravel;
    float4 metadata;
    float4 control;
    float4 guide;
    float4 motion;
    float4 proposal;
    float4 stats;
};

static const uint FieldReservoirSlotsPerPixel = 4u;
static const uint FieldReservoirRowCurrent = 0u;
static const uint FieldReservoirRowTemporal = 1u;
static const uint FieldReservoirRowSpatial = 2u;
static const uint FieldReservoirRowFinal = 3u;
static const float FieldReservoirEpsilon = 0.000001;
static const float FieldProposalKindDeterministicStructural = 1.0;

uint fieldReservoirHash(uint value)
{
    value ^= value >> 16;
    value *= 0x7feb352du;
    value ^= value >> 15;
    value *= 0x846ca68bu;
    value ^= value >> 16;
    return value;
}

float fieldReservoirRandom01(uint2 pixel, uint frame, uint producer)
{
    uint seed = pixel.x * 1973u ^ pixel.y * 9277u ^ frame * 26699u ^ producer * 911u;
    return (float)(fieldReservoirHash(seed) & 0x00ffffffu) / 16777216.0;
}

FieldReservoirSample emptyFieldReservoirSample(float farTravel)
{
    FieldReservoirSample sample;
    sample.colorTravel = float4(0.0, 0.0, 0.0, farTravel);
    sample.metadata = 0.0;
    sample.control = 0.0;
    sample.guide = float4(1.0, 0.0, 1.0, 2.0);
    sample.motion = 0.0;
    sample.proposal = 0.0;
    sample.stats = 0.0;
    return sample;
}

float fieldReservoirLuminance(float3 color)
{
    return dot(max(color, 0.0), float3(0.2126, 0.7152, 0.0722));
}

float fieldReservoirDefaultTarget(float4 colorTravel, float4 control, float4 guide)
{
    float coverage = saturate(control.x);
    float confidence = guide.x > 0.0 ? saturate(guide.x) : (control.w > 0.0 ? saturate(control.w) : 1.0);
    float emissionRelevance = 0.25 + fieldReservoirLuminance(colorTravel.rgb);
    return max(coverage * confidence * emissionRelevance, FieldReservoirEpsilon);
}

FieldReservoirSample makeFieldReservoirSample(
    float4 colorTravel,
    float4 metadata,
    float4 control,
    float4 guide,
    float4 motion,
    float target,
    float sourcePdf,
    float representedCandidateCount,
    float proposalKind)
{
    FieldReservoirSample sample;
    float safeTarget = max(target, FieldReservoirEpsilon);
    float safePdf = max(sourcePdf, FieldReservoirEpsilon);
    float safeCount = max(representedCandidateCount, 1.0);
    float weight = safeTarget / safePdf;
    sample.colorTravel = colorTravel;
    sample.metadata = metadata;
    sample.control = control;
    sample.guide = guide;
    sample.motion = motion;
    sample.proposal = float4(safeTarget, safePdf, safeCount, proposalKind);
    sample.stats = float4(safeTarget, weight, safeCount, weight / max(safeCount * safeTarget, FieldReservoirEpsilon));
    return sample;
}

bool fieldReservoirSampleValid(FieldReservoirSample sample, float farTravel)
{
    return sample.metadata.x > 0.5 &&
        sample.colorTravel.w > 0.0 &&
        sample.colorTravel.w <= farTravel &&
        saturate(sample.control.x) > 0.0 &&
        saturate(sample.guide.z) > 0.0 &&
        sample.proposal.x > 0.0 &&
        sample.proposal.y > 0.0 &&
        sample.stats.x > 0.0 &&
        sample.stats.y > 0.0 &&
        sample.stats.z > 0.0;
}

float fieldReservoirContributionWeight(FieldReservoirSample sample)
{
    return sample.stats.w > 0.0
        ? sample.stats.w
        : sample.stats.y / max(sample.stats.z * sample.stats.x, FieldReservoirEpsilon);
}

float3 fieldReservoirResolvedColor(FieldReservoirSample sample, float farTravel)
{
    if (!fieldReservoirSampleValid(sample, farTravel))
    {
        return 0.0;
    }

    return sample.colorTravel.rgb * fieldReservoirContributionWeight(sample);
}

FieldReservoirSample scaleFieldReservoirSampleWeight(FieldReservoirSample sample, float scale)
{
    float safeScale = saturate(scale);
    sample.stats.y *= safeScale;
    sample.stats.w = sample.stats.y / max(sample.stats.z * sample.stats.x, FieldReservoirEpsilon);
    sample.guide.x *= safeScale;
    return sample;
}

FieldReservoirSample mergeFieldReservoirSamples(
    FieldReservoirSample current,
    FieldReservoirSample other,
    float rng,
    float farTravel)
{
    bool currentValid = fieldReservoirSampleValid(current, farTravel);
    bool otherValid = fieldReservoirSampleValid(other, farTravel);
    if (!currentValid)
    {
        if (otherValid)
        {
            return other;
        }

        return emptyFieldReservoirSample(farTravel + 1.0);
    }

    if (!otherValid)
    {
        return current;
    }

    float currentWeightSum = max(current.stats.y, 0.0);
    float otherWeightSum = max(other.stats.y, 0.0);
    float mergedWeightSum = currentWeightSum + otherWeightSum;
    if (mergedWeightSum <= FieldReservoirEpsilon)
    {
        return emptyFieldReservoirSample(farTravel + 1.0);
    }

    FieldReservoirSample merged = current;
    if (rng < otherWeightSum / mergedWeightSum)
    {
        merged = other;
    }
    merged.stats.x = max(merged.proposal.x, FieldReservoirEpsilon);
    merged.stats.y = mergedWeightSum;
    merged.stats.z = max(current.stats.z, 0.0) + max(other.stats.z, 0.0);
    merged.stats.w = mergedWeightSum / max(merged.stats.z * merged.stats.x, FieldReservoirEpsilon);
    merged.guide.x = saturate(max(current.guide.x, other.guide.x));
    merged.guide.z = saturate(min(current.guide.z, other.guide.z));
    return merged;
}

FieldReservoirSample mergeFieldReservoirVisibilityProposals(
    FieldReservoirSample current,
    FieldReservoirSample other,
    float rng,
    float farTravel)
{
    bool currentValid = fieldReservoirSampleValid(current, farTravel);
    bool otherValid = fieldReservoirSampleValid(other, farTravel);
    if (!currentValid || !otherValid)
    {
        return mergeFieldReservoirSamples(current, other, rng, farTravel);
    }

    float nearTravel = min(current.colorTravel.w, other.colorTravel.w);
    float visibilityTolerance = max(0.025, nearTravel * 0.006);
    float travelDelta = abs(current.colorTravel.w - other.colorTravel.w);
    if (travelDelta > visibilityTolerance)
    {
        if (current.colorTravel.w <= other.colorTravel.w)
        {
            return current;
        }

        return other;
    }

    return mergeFieldReservoirSamples(current, other, rng, farTravel);
}
