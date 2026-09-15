#ifndef GRASS_MODES_INCLUDED
#define GRASS_MODES_INCLUDED

#if defined(GRASS_TEST_FLOATPRECISION)
    #define grass_real float
    #define grass_real3 float3
    #define grass_real4 float4
#else
    #define grass_real half
    #define grass_real3 half3
    #define grass_real4 half4
#endif

#if GRASS_LOD < 2 || defined(GRASS_TEST_LOD2FRAGMENT) || defined(GRASS_TEST_REFERENCESHADING)
    #define GRASS_FRAGMENT_LIT
#endif
#if defined(GRASS_TEST_LEGACYROUNDNESS) || defined(GRASS_TEST_REFERENCESHADING)
    #define GRASS_DUAL_NORMAL
#endif
#if defined(GRASS_TEST_EXACTVIEW) || defined(GRASS_TEST_REFERENCESHADING)
    #define GRASS_EXACT_VIEW
#endif
#if defined(GRASS_TEST_LEGACYCOLOR) || defined(GRASS_TEST_LEGACYDRYNESS) || defined(GRASS_TEST_REFERENCESHADING)
    #define GRASS_FRAGMENT_HASH
#endif
#endif
