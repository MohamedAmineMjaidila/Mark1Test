Shader "GrassFlow/Generated/a4f9b37152b9501cd105ddf0e272edb5/LOD2" { SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" } Pass {
Tags { "LightMode"="UniversalForward" } Cull Off ZWrite On ZTest LEqual HLSLPROGRAM
#pragma target 4.5
#pragma vertex GrassVertex
#pragma fragment GrassFragment
#pragma multi_compile_local _ GRASS_DIRECT_GRID
#define GRASS_LOD 2
#define GRASS_TEST_ONEWAVE 1
#define GRASS_TEST_LAMBERT 1
#define GRASS_TEST_TRANSMISSIONLEGACY 1
#define GRASS_TEST_LOD2FRAGMENT 1
#define GRASS_TEST_LEGACYAO 1
#include "Assets/Grass/Assets/Shaders/Experimental/Includes/GrassProgram.hlsl"
ENDHLSL
} } }
