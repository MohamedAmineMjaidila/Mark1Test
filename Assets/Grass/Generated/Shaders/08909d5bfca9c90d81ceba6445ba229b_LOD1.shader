Shader "GrassFlow/Generated/08909d5bfca9c90d81ceba6445ba229b/LOD1" { SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" } Pass {
Tags { "LightMode"="UniversalForward" } Cull Off ZWrite On ZTest LEqual HLSLPROGRAM
#pragma target 4.5
#pragma vertex GrassVertex
#pragma fragment GrassFragment
#pragma multi_compile_local _ GRASS_DIRECT_GRID
#define GRASS_LOD 1
#define GRASS_TEST_BEZIER 1
#define GRASS_TEST_WINDOFF 1
#define GRASS_TEST_ANALYTICROUNDNESS 1
#define GRASS_TEST_LEGACYCOLOR 1
#define GRASS_TEST_BLADEDRYNESS 1
#define GRASS_TEST_EXACTVIEW 1
#define GRASS_TEST_SPECULARLEGACY 1
#define GRASS_TEST_STABILIZATIONOFF 1
#define GRASS_TEST_LOD2FRAGMENT 1
#define GRASS_TEST_FLOATPRECISION 1
#define GRASS_TEST_WIDENING 1
#include "Assets/Grass/Assets/Shaders/Experimental/Includes/GrassProgram.hlsl"
ENDHLSL
} } }
