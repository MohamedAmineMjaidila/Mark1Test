Shader "GrassFlow/Generated/97acda3777d9479171ef4531be11f587/LOD1" { SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" } Pass {
Tags { "LightMode"="UniversalForward" } Cull Off ZWrite On ZTest LEqual HLSLPROGRAM
#pragma target 4.5
#pragma vertex GrassVertex
#pragma fragment GrassFragment
#pragma multi_compile_local _ GRASS_DIRECT_GRID
#define GRASS_LOD 1
#define GRASS_TEST_BEZIER 1
#define GRASS_TEST_ONEWAVE 1
#define GRASS_TEST_AMBIENTSH 1
#include "Assets/Grass/Assets/Shaders/Experimental/Includes/GrassProgram.hlsl"
ENDHLSL
} } }
