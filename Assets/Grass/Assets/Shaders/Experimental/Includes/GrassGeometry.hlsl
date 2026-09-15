#ifndef GRASS_GEOMETRY_INCLUDED
#define GRASS_GEOMETRY_INCLUDED

float4 _GrassWind; // normalized direction xy, strength z, speed w
float4 _GrassWindDetail; // gust, flutter, time, widening fraction
float4 _GrassViewDirection;
float4 _GrassNormal; // cos(roundness), sin(roundness), stabilization, angle

float4 _GrassWaveA, _GrassWaveB, _GrassGustWave, _GrassWaveParams;

#if defined(GRASS_TEST_LEGACYWIND)
TEXTURE2D(_WindMap); SAMPLER(sampler_WindMap);
float4 _WindVelocity;
float _WindFrequency, _WindDisableDistance, _WindGustStrength, _WindFlutterStrength;
#endif
struct GrassBendResult { float3 center; float3 tangent; };

float2 GrassWindOffset(float3 root, uint id)
{
#if defined(GRASS_TEST_WINDOFF)
    return 0;
#elif defined(GRASS_TEST_LEGACYWIND)
    float phase = GrassRandom(id, 0x85321u) * 6.2831853;
    float stiffness = lerp(_GrassWaveParams.y, _GrassWaveParams.z, GrassRandom(id, 0x91623u));
    #if defined(GRASS_HAS_LEGACY_FIELD)
        uint packed = _GrassLegacyRecords[id].windStiffness;
        phase = f16tof32(packed); stiffness = f16tof32(packed >> 16);
    #endif
    float fade = 1.0 - saturate((distance(root, _GrassCamera.xyz) - (_WindDisableDistance - 10.0)) / 10.0);
    float sample = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, root.xz * _WindFrequency + _GrassWindDetail.z * _WindVelocity.xy, 0).r;
    float flutter = sin(_GrassWindDetail.z * 15.0 + phase * 10.0) * _WindFlutterStrength;
    return normalize(_WindVelocity.xy + float2(1e-12, 0)) * ((sample * _WindGustStrength + flutter) * (2.0 - stiffness) * fade);
#else
    float time = _GrassWindDetail.z * _GrassWind.w;
    float phase = dot(root.xz, _GrassWaveA.xy) - time * _GrassWaveA.z + _GrassWaveA.w;
    float macro = sin(phase);
#if !defined(GRASS_TEST_ONEWAVE)
    macro = lerp(macro, sin(dot(root.xz, _GrassWaveB.xy) - time * _GrassWaveB.z + _GrassWaveB.w), _GrassWaveParams.x);
#endif
    float gust = 0.5 + 0.5 * sin(dot(root.xz, _GrassGustWave.xy) - time * _GrassGustWave.z);
    float flutter = sin(time * _GrassGustWave.w + GrassRandom(id, 0x85321u) * 6.2831853) * _GrassWindDetail.y;
    float stiffness = lerp(_GrassWaveParams.y, _GrassWaveParams.z, GrassRandom(id, 0x91623u));
    return _GrassWind.xy * ((macro * (1.0 + gust * _GrassWindDetail.x) + flutter) * _GrassWind.z / stiffness);
#endif
}

GrassBendResult GrassBend(float t, float height, float2 wind, float3 forward)
{
    GrassBendResult b;
    float3 bend = float3(wind.x, 0, wind.y) * height + forward * (_GrassBlade.w * height);
#if defined(GRASS_TEST_BEZIER)
    float3 p1 = float3(0, height * 0.33, 0) + forward * (_GrassBlade.w * height * 0.1);
    float3 p2 = GrassSafeNormal(float3(0, height * 0.66, 0) + bend * 0.65) * (height * 0.66);
    float3 p3 = GrassSafeNormal(float3(0, height, 0) + bend) * height;
    float u = 1.0 - t;
    b.center = 3.0 * u * u * t * p1 + 3.0 * u * t * t * p2 + t * t * t * p3;
    b.tangent = GrassSafeNormal(3.0 * u * u * p1 + 6.0 * u * t * (p2 - p1) + 3.0 * t * t * (p3 - p2));
#else
    // C(t) = h*t*normalize(up + t*bend/h). Root pinned; bounded by h; analytic derivative.
    float3 q = float3(0, 1, 0) + bend * (t / height);
    float invLength = rsqrt(dot(q, q));
    float3 direction = q * invLength;
    b.center = direction * (height * t);
    float3 derivative = bend / height;
    b.tangent = GrassSafeNormal(direction + t * (derivative * invLength - q * dot(q, derivative) * invLength * invLength * invLength));
#endif
    return b;
}

float3 GrassTerrainNormal(float2 local)
{
    float2 stepSize = _GrassTerrainSize.xz / max(float2(1, 1), _GrassHeightTextureSize.xy - 1.0);
    float dx = (GrassSampleHeight(local + float2(stepSize.x, 0)) - GrassSampleHeight(local - float2(stepSize.x, 0))) / (2.0 * stepSize.x);
    float dz = (GrassSampleHeight(local + float2(0, stepSize.y)) - GrassSampleHeight(local - float2(0, stepSize.y))) / (2.0 * stepSize.y);
    return GrassSafeNormal(float3(-dx, 1, -dz));
}

float3 GrassStableTarget(float3 root, uint id)
{
    float3 stable = float3(0, 1, 0);
    #if defined(GRASS_TEST_STABILIZATIONTERRAIN)
        stable = GrassTerrainNormal(root.xz - _GrassOrigin.xz);
    #elif defined(GRASS_TEST_STABILIZATIONPATCH)
        uint2 cell = GrassCell(id) >> 5;
        uint patch = cell.y * 65537u + cell.x;
        stable = GrassSafeNormal(float3((GrassRandom(patch, 0x72u) - 0.5) * 0.3, 1, (GrassRandom(patch, 0x79u) - 0.5) * 0.3));
    #endif
    return stable;
}

float3 GrassStabilize(float3 normal, float3 root, uint id)
{
#if defined(GRASS_TEST_STABILIZATIONOFF)
    return normal;
#else
    return GrassSafeNormal(lerp(normal, GrassStableTarget(root, id), _GrassNormal.z));
#endif
}
#endif
