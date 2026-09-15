#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"
#include "GrassModes.hlsl"
#include "GrassField.hlsl"
#include "GrassGeometry.hlsl"
#include "GrassLighting.hlsl"

StructuredBuffer<uint> _GrassVisibleIds;

struct GrassAttributes
{
    float4 positionOS : POSITION;
    float2 uv : TEXCOORD0;
};

struct GrassVaryings
{
    float4 positionCS : SV_POSITION;
#if defined(GRASS_FRAGMENT_LIT)
    #if defined(GRASS_TEST_ANALYTICROUNDNESS) && !defined(GRASS_TEST_STABILIZATIONOFF)
        grass_real3 stableTarget : TEXCOORD5;
    #endif
    grass_real3 normalWS : TEXCOORD0;
    grass_real3 grassData : TEXCOORD1;
    #if defined(GRASS_DUAL_NORMAL) || defined(GRASS_TEST_ANALYTICROUNDNESS)
        grass_real4 normalFrame : TEXCOORD2;
    #endif
    #if defined(GRASS_EXACT_VIEW)
        float3 positionWS : TEXCOORD3;
    #endif
    #if defined(GRASS_FRAGMENT_HASH)
        float3 rootWS : TEXCOORD4;
    #endif
#else
    grass_real3 color : TEXCOORD0;
#endif
};

GrassVaryings GrassVertex(GrassAttributes input, uint instance : SV_InstanceID)
{
    GrassVaryings output = (GrassVaryings)0;
    InitIndirectDrawArgs(0);
    uint ordinal = GetIndirectInstanceID(instance);
    uint id;
    float3 root;
#if defined(GRASS_DIRECT_GRID)
    uint tile = _GrassTiles[_GrassTileOffset + ordinal / 1024u];
    uint2 cell = GrassTileCell(tile, ordinal % 1024u);
    id = cell.y * (uint)_GrassColumns + cell.x;
    int selectedLod = GrassSelect(cell, id, root);
    if (selectedLod != GRASS_LOD)
    {
        // All vertices receive the same finite, outside-clip position. No NaNs or fragment discard.
        output.positionCS = float4(2, 2, 2, 1);
        return output;
    }
#else
    id = _GrassVisibleIds[ordinal];
    root = GrassRoot(GrassCell(id));
#endif
    float heightT = saturate(input.uv.y);
    float height = GrassHeight(id);
#if defined(GRASS_HAS_LEGACY_FIELD)
    uint facing = _GrassLegacyRecords[id].facing;
    float s = f16tof32(facing), c = f16tof32(facing >> 16);
#else
    float angle = GrassRandom(id, 0x61823u) * 6.2831853;
    float s, c; sincos(angle, s, c);
#endif
    float3 side = float3(c, 0, -s);
    float3 forward = float3(s, 0, c);
    GrassBendResult bend = GrassBend(heightT, height, GrassWindOffset(root, id), forward);
    // Keep the ribbon frame perpendicular to its deformed tangent, including lateral wind.
    side = GrassSafeNormal(side - bend.tangent * dot(side, bend.tangent));
    float3 flat = GrassSafeNormal(cross(side, bend.tangent));
    float widthCoordinate = input.uv.x * 2.0 - 1.0;
#if defined(GRASS_HAS_LEGACY_SELECTION)
    float distanceToCamera = length(root - _GrassCamera.xyz);
#else
    float distanceToCamera = length(root.xz - _GrassCamera.xz);
#endif
    float width = _GrassBlade.z * GrassWidthScale(distanceToCamera) * (1.0 - heightT);
    float3 position = root + bend.center + side * (widthCoordinate * width * 0.5);
#if defined(GRASS_TEST_WIDENING)
    float edge = 1.0 - abs(dot(flat, _GrassViewDirection.xyz));
    float3 cameraRight = GrassSafeNormal(cross(float3(0, 1, 0), _GrassViewDirection.xyz));
    position += cameraRight * (widthCoordinate * width * edge * _GrassWindDetail.w * 0.5);
#endif
    float3 normal = flat;
#if !defined(GRASS_TEST_FLATNORMAL) && ((!defined(GRASS_DUAL_NORMAL) && !defined(GRASS_TEST_ANALYTICROUNDNESS)) || !defined(GRASS_FRAGMENT_LIT))
    normal = GrassSafeNormal(flat * _GrassNormal.x + side * (widthCoordinate * _GrassNormal.y));
#endif
    normal = GrassStabilize(normal, root, id);
    grass_real3 data = GrassBladeData(id, heightT);
    output.positionCS = TransformWorldToHClip(position);
#if defined(GRASS_FRAGMENT_LIT)
    output.normalWS = (grass_real3)normal;
    output.grassData = data;
    #if defined(GRASS_DUAL_NORMAL)
        float3 normalA = float3(flat.x * _GrassNormal.x - flat.z * _GrassNormal.y, flat.y, flat.x * _GrassNormal.y + flat.z * _GrassNormal.x);
        float3 normalB = float3(flat.x * _GrassNormal.x + flat.z * _GrassNormal.y, flat.y, -flat.x * _GrassNormal.y + flat.z * _GrassNormal.x);
        output.normalWS = GrassStabilize(normalA, root, id);
        output.normalFrame = grass_real4(GrassStabilize(normalB, root, id), input.uv.x);
    #elif defined(GRASS_TEST_ANALYTICROUNDNESS)
        output.normalWS = (grass_real3)flat;
        output.normalFrame = grass_real4(side, widthCoordinate);
        #if !defined(GRASS_TEST_STABILIZATIONOFF)
            output.stableTarget = (grass_real3)GrassStableTarget(root, id);
        #endif
    #endif
    #if defined(GRASS_EXACT_VIEW)
        output.positionWS = position;
    #endif
    #if defined(GRASS_FRAGMENT_HASH)
        output.rootWS = root;
    #endif
#else
    float3 vertexView = _GrassViewDirection.xyz;
    #if defined(GRASS_EXACT_VIEW)
        vertexView = GrassSafeNormal(_GrassCamera.xyz - position);
    #endif
    output.color = GrassShade((grass_real3)normal, data, vertexView, root);
#endif
    return output;
}

half4 GrassFragment(GrassVaryings input) : SV_Target
{
#if defined(GRASS_FRAGMENT_LIT)
    grass_real3 normal = input.normalWS;
    #if defined(GRASS_DUAL_NORMAL)
        normal = lerp(normal, input.normalFrame.xyz, input.normalFrame.w);
    #elif defined(GRASS_TEST_ANALYTICROUNDNESS)
        float angle = input.normalFrame.w * _GrassNormal.w;
        normal = normal * cos(angle) + input.normalFrame.xyz * sin(angle);
        #if !defined(GRASS_TEST_STABILIZATIONOFF)
            normal = lerp(normal, input.stableTarget, (grass_real)_GrassNormal.z);
        #endif
    #endif
    normal = normalize(normal);
    float3 view = _GrassViewDirection.xyz;
    #if defined(GRASS_EXACT_VIEW)
        view = GrassSafeNormal(_GrassCamera.xyz - input.positionWS);
    #endif
    float3 root = 0;
    #if defined(GRASS_FRAGMENT_HASH)
        root = input.rootWS;
    #endif
    return half4(GrassShade(normal, input.grassData, view, root), 1);
#else
    return half4(input.color, 1);
#endif
}
