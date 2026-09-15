Shader "GrassFlow/Experimental/LOD1"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Name "GrassLOD1"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            ZWrite On
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 4.5
            #pragma vertex GrassVertex
            #pragma fragment GrassFragment
            #pragma multi_compile_local _ GRASS_DIRECT_GRID
            #pragma multi_compile_local _ GRASS_TEST_BEZIER GRASS_TEST_ONEWAVE GRASS_TEST_MATHEMATICALHEIGHT GRASS_TEST_FLATNORMAL GRASS_TEST_LEGACYROUNDNESS GRASS_TEST_ANALYTICROUNDNESS GRASS_TEST_BLADECOLOR GRASS_TEST_LEGACYCOLOR GRASS_TEST_BLADEDRYNESS GRASS_TEST_LEGACYDRYNESS GRASS_TEST_EXACTVIEW GRASS_TEST_LAMBERT GRASS_TEST_AMBIENTSH GRASS_TEST_AMBIENTOFF GRASS_TEST_TRANSMISSIONOFF GRASS_TEST_TRANSMISSIONLEGACY GRASS_TEST_SPECULAROFF GRASS_TEST_SPECULARLEGACY GRASS_TEST_STABILIZATIONOFF GRASS_TEST_STABILIZATIONTERRAIN GRASS_TEST_STABILIZATIONPATCH GRASS_TEST_WIDENING GRASS_TEST_FLOATPRECISION GRASS_TEST_LOD2FRAGMENT GRASS_TEST_REFERENCESHADING GRASS_TEST_MINIMALLIGHTING
            #define GRASS_LOD 1
            #include "Includes/GrassProgram.hlsl"
            ENDHLSL
        }
    }
}
