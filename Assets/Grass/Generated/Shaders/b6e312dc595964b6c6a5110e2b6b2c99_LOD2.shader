Shader "GrassFlow/Generated/b6e312dc595964b6c6a5110e2b6b2c99/LOD2" { SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" } Pass {
Tags { "LightMode"="UniversalForward" } Cull Off ZWrite On ZTest LEqual HLSLPROGRAM
#pragma target 4.5
#pragma vertex GrassVertex
#pragma fragment GrassFragment
#pragma multi_compile_local _ GRASS_DIRECT_GRID
#define GRASS_LOD 2
#define GRASS_TEST_BEZIER 1
#define GRASS_TEST_WINDOFF 1
#define GRASS_TEST_ANALYTICROUNDNESS 1
#define GRASS_TEST_LEGACYCOLOR 1
#define GRASS_TEST_BLADEDRYNESS 1
#define GRASS_TEST_EXACTVIEW 1
#define GRASS_TEST_SPECULARLEGACY 1
#define GRASS_TEST_STABILIZATIONTERRAIN 1
#define GRASS_TEST_LOD2FRAGMENT 1
#define GRASS_TEST_FLOATPRECISION 1
#define GRASS_TEST_WIDENING 1
#include "Assets/Grass/Assets/Shaders/Experimental/Includes/GrassProgram.hlsl"
ENDHLSL
} } }
