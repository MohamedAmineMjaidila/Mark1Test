Shader "GrassFlow/Generated/6eeb5fb1ee86dfa3dee2437c6b040119/LOD0" { SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" } Pass {
Tags { "LightMode"="UniversalForward" } Cull Off ZWrite On ZTest LEqual HLSLPROGRAM
#pragma target 4.5
#pragma vertex GrassVertex
#pragma fragment GrassFragment
#pragma multi_compile_local _ GRASS_DIRECT_GRID
#define GRASS_LOD 0
#define GRASS_TEST_BEZIER 1
#define GRASS_TEST_ONEWAVE 1
#define GRASS_TEST_LEGACYDRYNESS 1
#include "Assets/Grass/Assets/Shaders/Experimental/Includes/GrassProgram.hlsl"
ENDHLSL
} } }
