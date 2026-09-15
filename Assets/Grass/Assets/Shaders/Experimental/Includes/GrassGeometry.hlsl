#ifndef GRASS_GEOMETRY_INCLUDED
#define GRASS_GEOMETRY_INCLUDED

float4 _GrassWind; // normalized direction xy, strength z, speed w
float4 _GrassWindDetail; // gust, flutter, time, widening fraction
float4 _GrassViewDirection;
float4 _GrassNormal; // cos(roundness), sin(roundness), stabilization, angle

struct GrassBendResult { float3 center; float3 tangent; };

float2 GrassWindOffset(float3 root, uint id)
{
    float time = _GrassWindDetail.z * _GrassWind.w;
    float phase = dot(root.xz, float2(0.11, 0.07)) - time * 1.3;
    float macro = sin(phase);
#if !defined(GRASS_TEST_ONEWAVE)
    macro = macro * 0.65 + sin(dot(root.xz, float2(-0.055, 0.093)) - time * 0.79 + 1.8) * 0.35;
#endif
    float gust = 0.5 + 0.5 * sin(dot(root.xz, float2(0.017, 0.023)) - time * 0.37);
    float flutter = sin(time * 15.0 + GrassRandom(id, 0x85321u) * 6.2831853) * _GrassWindDetail.y;
    float stiffness = lerp(0.8, 1.0, GrassRandom(id, 0x91623u));
    return _GrassWind.xy * ((macro * (1.0 + gust * _GrassWindDetail.x) + flutter) * _GrassWind.z / stiffness);
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

float3 GrassStabilize(float3 normal, float3 root, uint id)
{
#if defined(GRASS_TEST_STABILIZATIONOFF)
    return normal;
#else
    float3 stable = float3(0, 1, 0);
    #if defined(GRASS_TEST_STABILIZATIONTERRAIN)
        stable = GrassTerrainNormal(root.xz - _GrassOrigin.xz);
    #elif defined(GRASS_TEST_STABILIZATIONPATCH)
        uint2 cell = GrassCell(id) >> 5;
        uint patch = cell.y * 65537u + cell.x;
        stable = GrassSafeNormal(float3((GrassRandom(patch, 0x72u) - 0.5) * 0.3, 1, (GrassRandom(patch, 0x79u) - 0.5) * 0.3));
    #endif
    return GrassSafeNormal(lerp(normal, stable, _GrassNormal.z));
#endif
}
#endif
