Shader "GrassFlow/Generated/a92861cd5fafe5a05fea320db209b0df/LOD0" { SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" } Pass {
Tags { "LightMode"="UniversalForward" } Cull Off ZWrite On ZTest LEqual HLSLPROGRAM
#pragma target 4.5
#pragma vertex GrassVertex
#pragma fragment GrassFragment
#pragma multi_compile_local _ GRASS_DIRECT_GRID
#define GRASS_LOD 0
#define GRASS_TEST_LEGACYROUNDNESS 1
#define GRASS_TEST_LEGACYCOLOR 1
#define GRASS_TEST_LEGACYDRYNESS 1
#define GRASS_TEST_EXACTVIEW 1
#define GRASS_TEST_AMBIENTSH 1
#define GRASS_TEST_TRANSMISSIONLEGACY 1
#define GRASS_TEST_SPECULARLEGACY 1
#define GRASS_TEST_LOD2FRAGMENT 1
#define GRASS_TEST_LEGACYAO 1
#include "Assets/Grass/Assets/Shaders/Experimental/Includes/GrassProgram.hlsl"
ENDHLSL
} } }
