Shader "GrassFlow/Generated/e2817dc033b03ae0ec9b8d766467bc70/LOD0" { SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" } Pass {
Tags { "LightMode"="UniversalForward" } Cull Off ZWrite On ZTest LEqual HLSLPROGRAM
#pragma target 4.5
#pragma vertex GrassVertex
#pragma fragment GrassFragment
#pragma multi_compile_local _ GRASS_DIRECT_GRID
#define GRASS_LOD 0
#define GRASS_TEST_TRANSMISSIONLEGACY 1
#include "Assets/Grass/Assets/Shaders/Experimental/Includes/GrassProgram.hlsl"
ENDHLSL
} } }
