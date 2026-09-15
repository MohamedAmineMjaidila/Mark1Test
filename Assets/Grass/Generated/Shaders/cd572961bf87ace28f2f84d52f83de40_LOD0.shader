Shader "GrassFlow/Generated/cd572961bf87ace28f2f84d52f83de40/LOD0" { SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" } Pass {
Tags { "LightMode"="UniversalForward" } Cull Off ZWrite On ZTest LEqual HLSLPROGRAM
#pragma target 4.5
#pragma vertex GrassVertex
#pragma fragment GrassFragment
#pragma multi_compile_local _ GRASS_DIRECT_GRID
#define GRASS_LOD 0
#define GRASS_TEST_ONEWAVE 1
#define GRASS_TEST_AMBIENTSH 1
#define GRASS_TEST_TRANSMISSIONLEGACY 1
#define GRASS_TEST_LOD2FRAGMENT 1
#define GRASS_TEST_LEGACYAO 1
#include "Assets/Grass/Assets/Shaders/Experimental/Includes/GrassProgram.hlsl"
ENDHLSL
} } }
