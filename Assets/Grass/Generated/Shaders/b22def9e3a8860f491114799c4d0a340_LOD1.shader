Shader "GrassFlow/Generated/b22def9e3a8860f491114799c4d0a340/LOD1" { SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" } Pass {
Tags { "LightMode"="UniversalForward" } Cull Off ZWrite On ZTest LEqual HLSLPROGRAM
#pragma target 4.5
#pragma vertex GrassVertex
#pragma fragment GrassFragment
#pragma multi_compile_local _ GRASS_DIRECT_GRID
#define GRASS_LOD 1
#define GRASS_TEST_SPECULAROFF 1
#include "Assets/Grass/Assets/Shaders/Experimental/Includes/GrassProgram.hlsl"
ENDHLSL
} } }
