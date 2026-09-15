#ifndef GRASS_FIELD_INCLUDED
#define GRASS_FIELD_INCLUDED

StructuredBuffer<uint> _GrassOccupancy;
StructuredBuffer<uint> _GrassTiles;
Texture2D<float> _GrassHeights;
SamplerState sampler_GrassHeights;
int _GrassColumns, _GrassRows, _GrassWordsPerRow, _GrassTileColumns;
int _GrassSeed, _GrassTileOffset;
float4 _GrassLegacyBakeOrigin;
float _GrassLegacyHeightMultiplier;
#if defined(GRASS_TEST_LEGACYFIELD) || defined(GRASS_LEGACY_FIELD)
    #define GRASS_HAS_LEGACY_FIELD
    struct GrassLegacyRecord { float3 position; uint facing; uint heightWidth; uint windStiffness; };
    StructuredBuffer<GrassLegacyRecord> _GrassLegacyRecords;
#endif
#if defined(GRASS_TEST_LEGACYSELECTION) || defined(GRASS_LEGACY_SELECTION)
    #define GRASS_HAS_LEGACY_SELECTION
#endif
float GrassReferenceHash(float2 p)
{
    float3 p3 = frac(float3(p.xyx) * .1031);
    p3 += dot(p3, p3.yzx + 33.33);
    return frac((p3.x + p3.y) * p3.z);
}
float4 _GrassTerrainSize; // xyz = metres, w = cell spacing
float4 _GrassOrigin;
float4 _GrassHeightTextureSize; // xy = resolution, zw = reciprocal resolution
float4 _GrassHeightModel; // base, amplitude, x frequency, z frequency
float4 _GrassCamera;
float4 _GrassDistance; // near, middle, far, density falloff start
float4 _GrassDensity; // minimum density, width compensation, width cap, transition metres
float4 _GrassBlade; // min height, max height, width, natural bend
float4 _GrassFrustum[6];

uint GrassHash(uint value)
{
    value ^= value >> 16; value *= 0x7feb352du;
    value ^= value >> 15; value *= 0x846ca68bu;
    return value ^ (value >> 16);
}
float GrassRandom(uint id, uint salt) { return (GrassHash(id ^ (uint)_GrassSeed ^ salt) & 0x00ffffffu) * (1.0 / 16777216.0); }
float3 GrassSafeNormal(float3 value) { return value * rsqrt(max(dot(value, value), 1e-12)); }

uint2 GrassCell(uint id) { return uint2(id % (uint)_GrassColumns, id / (uint)_GrassColumns); }
bool GrassOccupied(uint2 cell)
{
    if (cell.x >= (uint)_GrassColumns || cell.y >= (uint)_GrassRows) return false;
    uint word = cell.y * (uint)_GrassWordsPerRow + (cell.x >> 5);
    return (_GrassOccupancy[word] & (1u << (cell.x & 31u))) != 0;
}
uint2 GrassTileCell(uint tile, uint localCell)
{
    return uint2(tile % (uint)_GrassTileColumns, tile / (uint)_GrassTileColumns) * 32u + uint2(localCell & 31u, localCell >> 5);
}

float GrassSampleHeight(float2 localXZ)
{
#if defined(GRASS_MATH_HEIGHT) || defined(GRASS_TEST_MATHEMATICALHEIGHT)
    return _GrassHeightModel.x + _GrassHeightModel.y * sin(localXZ.x * _GrassHeightModel.z) * cos(localXZ.y * _GrassHeightModel.w);
#else
    float2 uv = saturate(localXZ / _GrassTerrainSize.xz);
    uv = (uv * (_GrassHeightTextureSize.xy - 1.0) + 0.5) * _GrassHeightTextureSize.zw;
    return _GrassHeights.SampleLevel(sampler_GrassHeights, uv, 0) * _GrassTerrainSize.y;
#endif
}

float3 GrassRoot(uint2 cell)
{
#if defined(GRASS_HAS_LEGACY_FIELD)
    return _GrassLegacyRecords[cell.y * (uint)_GrassColumns + cell.x].position + (_GrassOrigin.xyz - _GrassLegacyBakeOrigin.xyz);
#else
    float2 local = (float2(cell) + 0.5) * _GrassTerrainSize.w;
    return _GrassOrigin.xyz + float3(local.x, GrassSampleHeight(local), local.y);
#endif
}
float GrassRawHeight(uint id)
{
#if defined(GRASS_HAS_LEGACY_FIELD)
    return f16tof32(_GrassLegacyRecords[id].heightWidth);
#else
    return lerp(_GrassBlade.x, _GrassBlade.y, GrassRandom(id, 0x92123u));
#endif
}
float GrassHeight(uint id)
{
#if defined(GRASS_HAS_LEGACY_FIELD)
    return GrassRawHeight(id) * _GrassLegacyHeightMultiplier;
#else
    return GrassRawHeight(id);
#endif
}
float GrassDensityAt(float distanceToCamera)
{
    float t = saturate((distanceToCamera - _GrassDistance.w) / max(0.001, _GrassDistance.z - _GrassDistance.w));
#if defined(GRASS_HAS_LEGACY_SELECTION)
    return lerp(1.0, _GrassDensity.x, t);
#else
    return lerp(1.0, _GrassDensity.x, t * t * (3.0 - 2.0 * t));
#endif
}
float GrassWidthScale(float distanceToCamera)
{
    float scale = lerp(1.0, rcp(max(0.001, GrassDensityAt(distanceToCamera))), _GrassDensity.y);
#if defined(GRASS_HAS_LEGACY_SELECTION)
    return f16tof32(f32tof16(scale));
#else
    return min(_GrassDensity.z, scale);
#endif
}

// Shared by compute selection and direct-grid VS rejection. Identity/density/LOD are identical.
int GrassSelect(uint2 cell, uint id, out float3 root)
{
    root = 0;
    if (!GrassOccupied(cell)) return -1;
#if defined(GRASS_HAS_LEGACY_SELECTION)
    root = GrassRoot(cell);
    float3 difference = root - _GrassCamera.xyz;
    float distanceSquared = dot(difference, difference);
    if (distanceSquared > _GrassDistance.z * _GrassDistance.z) return -1;
    float distanceToCamera = sqrt(distanceSquared);
    if (distanceToCamera > _GrassDistance.w && GrassReferenceHash(float2(id, id * 0.5)) > GrassDensityAt(distanceToCamera)) return -1;
    float rawHeight = GrassRawHeight(id);
    float3 center = root + float3(0, rawHeight * 0.5, 0);
    [unroll] for (int plane = 0; plane < 6; plane++)
        if (dot(_GrassFrustum[plane].xyz, center) + _GrassFrustum[plane].w < -rawHeight) return -1;
    return distanceSquared < _GrassDistance.x * _GrassDistance.x ? 0 : distanceSquared < _GrassDistance.y * _GrassDistance.y ? 1 : 2;
#else
#if defined(GRASS_HAS_LEGACY_FIELD)
    root = GrassRoot(cell);
    float2 delta = root.xz - _GrassCamera.xz;
#else
    float2 local = (float2(cell) + 0.5) * _GrassTerrainSize.w;
    float2 delta = local + _GrassOrigin.xz - _GrassCamera.xz;
#endif
    float d2 = dot(delta, delta);
    if (d2 >= _GrassDistance.z * _GrassDistance.z) return -1;
    float distanceToCamera = sqrt(d2);
    if (GrassRandom(id, 0x53172u) >= GrassDensityAt(distanceToCamera)) return -1;
#if !defined(GRASS_HAS_LEGACY_FIELD)
    root = _GrassOrigin.xyz + float3(local.x, GrassSampleHeight(local), local.y);
#endif
    float radius = GrassHeight(id) + _GrassBlade.z * _GrassDensity.z;
    [unroll] for (int i = 0; i < 6; i++)
        if (dot(_GrassFrustum[i].xyz, root) + _GrassFrustum[i].w < -radius) return -1;
    // A blade changes LOD once in a distributed transition band; it is never drawn in both LODs.
    float near = _GrassDistance.x + (GrassRandom(id, 0x12345u) - 0.5) * _GrassDensity.w;
    float middle = _GrassDistance.y + (GrassRandom(id, 0x67391u) - 0.5) * _GrassDensity.w;
    return distanceToCamera < near ? 0 : distanceToCamera < middle ? 1 : 2;
#endif
}
#endif
