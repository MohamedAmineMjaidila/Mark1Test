#ifndef GRASS_LIGHTING_INCLUDED
#define GRASS_LIGHTING_INCLUDED

float4 _GrassRootColor, _GrassMidColor, _GrassTipColor, _GrassDryColor;
float4 _GrassSky, _GrassGround, _GrassSunColor, _GrassSunDirection, _GrassHalfDirection;
float4 _GrassColorParams; // variation, dryness, root darkness, specular
float4 _GrassLightParams; // transmission, legacy transmission exponent, legacy spec exponent, unused
float4 _GrassAoWeights;

float GrassLegacyHash(float2 p)
{
    p = frac(p * float2(123.34, 456.21));
    p += dot(p, p + 45.32);
    return frac(p.x * p.y);
}

float GrassPatch(uint2 cell, uint salt)
{
    // Smooth interpolation avoids square patch borders in lighting/color.
    float2 p = float2(cell) / 32.0;
    uint2 a = (uint2)floor(p);
    float2 f = frac(p); f = f * f * (3.0 - 2.0 * f);
    uint id = a.y * 65537u + a.x;
    return lerp(lerp(GrassRandom(id, salt), GrassRandom(id + 1u, salt), f.x),
                lerp(GrassRandom(id + 65537u, salt), GrassRandom(id + 65538u, salt), f.x), f.y);
}

grass_real3 GrassBladeData(uint id, float heightT)
{
    uint2 cell = GrassCell(id);
    float blade = GrassRandom(id, 0x7346u);
    float variation = blade;
#if !defined(GRASS_TEST_BLADECOLOR)
    variation = GrassPatch(cell, 0x38271u) * 0.8 + blade * 0.2;
#endif
    float dry = GrassRandom(id, 0x6844u);
#if !defined(GRASS_TEST_BLADEDRYNESS)
    dry = GrassPatch(cell, 0x55321u) * 0.85 + dry * 0.15;
#endif
    dry = saturate((dry - 0.5) * 2.0 + _GrassColorParams.y) * _GrassColorParams.y;
    return grass_real3(heightT, variation, dry);
}

grass_real3 GrassShade(grass_real3 N, grass_real3 data, float3 view, float3 root)
{
    grass_real t = data.x;
    grass_real variation = data.y;
    grass_real dryness = data.z;
#if defined(GRASS_FRAGMENT_HASH)
    float legacyNoise = GrassLegacyHash(root.xz * 0.3);
    #if defined(GRASS_TEST_LEGACYCOLOR) || defined(GRASS_TEST_REFERENCESHADING)
        variation = legacyNoise;
    #endif
    #if defined(GRASS_TEST_LEGACYDRYNESS) || defined(GRASS_TEST_REFERENCESHADING)
        dryness = saturate(legacyNoise * 2.0 - 1.0 + _GrassColorParams.y) * _GrassColorParams.y;
    #endif
#endif
    grass_real3 baseColor = lerp((grass_real3)_GrassRootColor.rgb, (grass_real3)_GrassMidColor.rgb, saturate(t * (grass_real)2));
    baseColor = lerp(baseColor, (grass_real3)_GrassTipColor.rgb, saturate(t * (grass_real)2 - (grass_real)1));
    baseColor = lerp(baseColor, (grass_real3)_GrassDryColor.rgb, dryness);
    grass_real hue = (variation - (grass_real)0.5) * (grass_real)_GrassColorParams.x;
    baseColor = lerp(baseColor, baseColor * grass_real3(1 + hue, 1, 1 - hue * (grass_real)0.5), (grass_real)_GrassColorParams.x);

#if defined(GRASS_TEST_MINIMALLIGHTING)
    return baseColor;
#else
    grass_real noL = dot(N, (grass_real3)_GrassSunDirection.xyz);
    #if defined(GRASS_TEST_LAMBERT)
        grass_real diffuse = saturate(noL);
    #else
        grass_real diffuse = saturate(noL * (grass_real)0.5 + (grass_real)0.5);
    #endif
    grass_real3 ambient;
    #if defined(GRASS_TEST_AMBIENTOFF)
        ambient = 0;
    #elif defined(GRASS_TEST_AMBIENTSH) || defined(GRASS_TEST_REFERENCESHADING)
        ambient = SampleSH(N);
    #else
        ambient = lerp((grass_real3)_GrassGround.rgb, (grass_real3)_GrassSky.rgb, saturate(N.y * (grass_real)0.5 + (grass_real)0.5));
    #endif
    grass_real transmission = 0;
    #if !defined(GRASS_TEST_TRANSMISSIONOFF) && GRASS_LOD < 2
        #if defined(GRASS_TEST_TRANSMISSIONLEGACY) || defined(GRASS_TEST_REFERENCESHADING)
            float back = saturate(dot(view, -_GrassSunDirection.xyz + N * 0.3));
            transmission = pow(back, _GrassLightParams.y) * _GrassLightParams.x * t;
        #else
            transmission = saturate(-noL) * (grass_real)_GrassLightParams.x * t;
        #endif
    #endif
    grass_real specular = 0;
    #if GRASS_LOD == 0 && !defined(GRASS_TEST_SPECULAROFF)
        float3 H = _GrassHalfDirection.xyz;
        #if defined(GRASS_EXACT_VIEW)
            H = GrassSafeNormal(_GrassSunDirection.xyz + view);
        #endif
        grass_real noH = saturate(dot(N, (grass_real3)H));
        #if defined(GRASS_TEST_SPECULARLEGACY) || defined(GRASS_TEST_REFERENCESHADING)
            specular = pow(noH, _GrassLightParams.z);
        #else
            grass_real s2 = noH * noH;
            grass_real s4 = s2 * s2;
            specular = s4 * s4;
        #endif
        specular *= (grass_real)_GrassColorParams.w * t;
    #endif
    grass_real aoT = dot(grass_real3(t, t * t, t * t * (3 - 2 * t)), (grass_real3)_GrassAoWeights.xyz);
    grass_real ao = lerp((grass_real)_GrassColorParams.z, (grass_real)1, aoT);
    #if defined(GRASS_TEST_REFERENCESHADING)
        ao = lerp((grass_real)1, saturate(t * (grass_real)2 + (grass_real)0.3), (grass_real)(1 - _GrassColorParams.z));
    #endif
    return (baseColor * (ambient + (grass_real3)_GrassSunColor.rgb * (diffuse + transmission)) + specular * (grass_real3)_GrassSunColor.rgb) * ao;
#endif
}
#endif
