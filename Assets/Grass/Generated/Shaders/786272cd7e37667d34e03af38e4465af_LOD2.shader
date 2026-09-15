Shader "GrassFlow/Generated/786272cd7e37667d34e03af38e4465af/LOD2" { SubShader { Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
Pass { Name "ForwardLit" Tags { "LightMode"="UniversalForward" } Cull Off ZWrite On ZTest LEqual
HLSLPROGRAM
#pragma target 4.5
#pragma vertex GrassVertex
#pragma fragment GrassFragment
#pragma multi_compile_local _ GRASS_DIRECT_GRID
#pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
#pragma multi_compile _ _SHADOWS_SOFT
#define GRASS_LOD 2
#define GRASS_TEST_LEGACYMODEL 1
#define GRASS_TEST_LEGACYFIELD 1
#define GRASS_TEST_LEGACYSELECTION 1
#include "Assets/Grass/Assets/Shaders/Experimental/Includes/GrassLegacyProgram.hlsl"
ENDHLSL
}
Pass { Name "ShadowCaster" Tags { "LightMode"="ShadowCaster" } Cull Off ZWrite On ZTest LEqual
ColorMask 0
HLSLPROGRAM
#pragma target 4.5
#pragma vertex GrassShadowVertex
#pragma fragment GrassDepthFragment
#pragma multi_compile_local _ GRASS_DIRECT_GRID
#pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
#define GRASS_LOD 2
#define GRASS_TEST_LEGACYMODEL 1
#define GRASS_TEST_LEGACYFIELD 1
#define GRASS_TEST_LEGACYSELECTION 1
#include "Assets/Grass/Assets/Shaders/Experimental/Includes/GrassLegacyProgram.hlsl"
ENDHLSL
}
Pass { Name "DepthOnly" Tags { "LightMode"="DepthOnly" } Cull Off ZWrite On ZTest LEqual
ColorMask 0
HLSLPROGRAM
#pragma target 4.5
#pragma vertex GrassDepthVertex
#pragma fragment GrassDepthFragment
#pragma multi_compile_local _ GRASS_DIRECT_GRID
#define GRASS_LOD 2
#define GRASS_TEST_LEGACYMODEL 1
#define GRASS_TEST_LEGACYFIELD 1
#define GRASS_TEST_LEGACYSELECTION 1
#include "Assets/Grass/Assets/Shaders/Experimental/Includes/GrassLegacyProgram.hlsl"
ENDHLSL
}
} }
