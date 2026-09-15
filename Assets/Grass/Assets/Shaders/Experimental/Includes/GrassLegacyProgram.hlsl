// Reference equations preserved from GrassShader.shader; immutable cell IDs replace appended 24-byte draw records.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#define UNITY_INDIRECT_DRAW_ARGS IndirectDrawIndexedArgs
#include "UnityIndirect.cginc"
#include "GrassField.hlsl"
float _GrassLegacyTime;
StructuredBuffer<uint> _GrassVisibleIds;

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
        
        // Data Structures
        struct GrassDataPacked
        {
            float3 position;
            uint facing;
            uint heightWidth;
            uint windStiffness;
        };

        struct GrassData
        {
            float3 position;
            float height;
            float2 facing;
            float windPhase;
            float stiffness;
            float widthScale;
        };

        GrassData UnpackGrassData(GrassDataPacked packedData)
        {
            GrassData d;
            d.position = packedData.position;
            
            uint f = packedData.facing;
            d.facing = float2(f16tof32(f), f16tof32(f >> 16));
            
            uint hw = packedData.heightWidth;
            d.height = f16tof32(hw);
            d.widthScale = f16tof32(hw >> 16);
            
            uint ws = packedData.windStiffness;
            d.windPhase = f16tof32(ws);
            d.stiffness = f16tof32(ws >> 16);
            
            return d;
        }
        
        // Variables
        TEXTURE2D(_WindMap);
        SAMPLER(sampler_WindMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _RootColor;
            float4 _MidColor;
            float4 _TipColor;
            float4 _DryColor;
            float4 _WindMap_ST;
            float4 _WindVelocity;
            float _DryAmount;
            float _ColorNoiseScale;
            float _ColorNoiseStrength;
            float _BladeWidth;
            float _BladeHeight;
            float _BladeBend;
            float _CameraFacing;
            float _WindFrequency;
            float _WindGustStrength;
            float _WindFlutterStrength;
            float _WindDisableDistance;
            float _NormalRotationAngle;
            float _Translucency;
            float _TranslucencyPower;
            float _AmbientOcclusionStrength;
            float _ShadowSoftness;
            float _SpecularStrength;
            float _SpecularPower;
        CBUFFER_END
        
        #if defined(UNITY_PROCEDURAL_INSTANCING_ENABLED)
            StructuredBuffer<GrassDataPacked> _GrassDataBuffer;
        #endif

        // Helper Functions
        float easeOut(float t, float power)
        {
            return 1.0 - pow(abs(1.0 - t), power);
        }
        
        // Rotate vector around Y axis
        float3 RotateAroundY(float3 v, float angle)
        {
            float s = sin(angle);
            float c = cos(angle);
            return float3(
                v.x * c - v.z * s,
                v.y,
                v.x * s + v.z * c
            );
        }
        
        // Simple hash for color variation
        float Hash21(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float3 Bezier(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float u = 1.0 - t;
            float tt = t * t;
            float uu = u * u;
            float uuu = uu * u;
            float ttt = tt * t;
            return uuu * p0 + 3 * uu * t * p1 + 3 * u * tt * p2 + ttt * p3;
        }

        float3 BezierTangent(float3 p0, float3 p1, float3 p2, float3 p3, float t)
        {
            float u = 1.0 - t;
            float uu = u * u;
            float tt = t * t;
            return 3 * uu * (p1 - p0) + 6 * u * t * (p2 - p1) + 3 * tt * (p3 - p2);
        }

        // Shared Vertex Calculation
        void CalculateGrassVertex(inout float3 positionOS, inout float3 normalOS, float2 uv, GrassData grass)
        {
            // 1. Setup
            float t = saturate(uv.y);
            float height = grass.height * _BladeHeight;
            
            // 2. Wind Calculation
            float3 worldPos = grass.position;
            
            // Calculate distance fade for wind
            float dist = distance(worldPos, _WorldSpaceCameraPos);
            // Fade out wind over 10 meters before the disable distance
            float windFade = 1.0 - saturate((dist - (_WindDisableDistance - 10.0)) / 10.0);
            
            // Macro Wind
            float2 windUV = worldPos.xz * _WindFrequency + _GrassLegacyTime * _WindVelocity.xy;
            float windSample = SAMPLE_TEXTURE2D_LOD(_WindMap, sampler_WindMap, windUV, 0).r;
            
            // Micro Wind (Flutter)
            float flutter = sin(_GrassLegacyTime * 15.0 + grass.windPhase * 10.0) * _WindFlutterStrength;
            
            // Combine and apply stiffness (lower stiffness = more wind effect)
            // Apply distance fade
            float currentWindImpact = ((windSample * _WindGustStrength) + flutter) * (2.0 - grass.stiffness) * windFade;
            
            // Wind Direction (Object Space)
            float3 windDirWS = normalize(float3(_WindVelocity.x, 0, _WindVelocity.y));
            
            // Rotate wind to Object Space
            float2 facing = grass.facing;

            // Camera Facing Logic
            if (_CameraFacing > 0.0)
            {
                float3 viewDir = _WorldSpaceCameraPos - worldPos;
                float2 viewDirXZ = normalize(viewDir.xz);
                // Blend between original facing and camera facing
                facing = normalize(lerp(facing, viewDirXZ, _CameraFacing));
            }

            float3 right = float3(facing.y, 0, -facing.x);
            float3 forward = float3(facing.x, 0, facing.y);
            
            float3 windDirOS;
            windDirOS.x = dot(windDirWS, right);
            windDirOS.y = 0;
            windDirOS.z = dot(windDirWS, forward);
            
            // 3. Bezier Control Points
            float3 leanVector = windDirOS * currentWindImpact;
            
            // Tip Flapping:
            // 1. Tip bends more aggressively with strong wind (quadratic response)
            // 2. Extra high-frequency flutter at the tip when wind is strong
            float tipBendFactor = 2.0 + (windSample * 1.0); 
            float tipFlutter = sin(_GrassLegacyTime * 25.0 + grass.windPhase * 20.0) * windSample * _WindFlutterStrength;
            float3 tipFlutterVec = windDirOS * tipFlutter;

            // Natural bend - grass bends in its facing direction for organic look
            // The bend increases along the height of the blade (quadratic curve)
            float3 naturalBendDir = float3(0, 0, 1); // Bend forward in object space (along facing)
            float bendAmount = _BladeBend * height;
            
            float3 p0 = float3(0, 0, 0);
            float3 p1 = float3(0, height * 0.33, 0) + naturalBendDir * bendAmount * 0.1;
            
            // Calculate target positions for p2 and p3 with wind applied
            float3 p2_target = float3(0, height * 0.66, 0) + leanVector + naturalBendDir * bendAmount * 0.4;
            float3 p3_target = float3(0, height, 0) + (leanVector * tipBendFactor) + tipFlutterVec + naturalBendDir * bendAmount;

            // Length preservation: Constrain control points to their respective heights
            // This ensures the blade bends (rotates) rather than shearing (stretching)
            float3 p2 = normalize(p2_target) * (height * 0.66);
            float3 p3 = normalize(p3_target) * height;
            
            // 4. Calculate Position
            float3 spinePos = Bezier(p0, p1, p2, p3, t);
            
            // 5. Apply Width
            // Assuming input mesh is a quad with x centered at 0
            // We can use the input positionOS.x to determine width offset
            // Or use UV.x if we want to be procedural about width too
            float width = _BladeWidth * grass.widthScale;
            float3 widthOffset = float3(positionOS.x * (width / 0.05), 0, 0); // Scale based on default width 0.05
            
            positionOS = spinePos + widthOffset;
            
            // 6. Recalculate Normal
            float3 tangent = normalize(BezierTangent(p0, p1, p2, p3, t));
            normalOS = normalize(cross(float3(1, 0, 0), tangent));
        }
        


struct GrassLegacyAttributes { float4 positionOS : POSITION; float2 uv : TEXCOORD0; };

bool GrassLegacyGeometry(GrassLegacyAttributes input, uint instance, out GrassData grass, out float3 positionWS, out float3 normalWS, out float3 normalOS, out float2 facing)
{
    InitIndirectDrawArgs(0);
    uint ordinal = GetIndirectInstanceID(instance), id;
    grass = (GrassData)0; positionWS = normalWS = normalOS = 0; facing = 0;
#if defined(GRASS_DIRECT_GRID)
    uint tile = _GrassTiles[_GrassTileOffset + ordinal / 1024u];
    uint2 cell = GrassTileCell(tile, ordinal % 1024u);
    id = cell.y * (uint)_GrassColumns + cell.x;
    float3 root;
    if (GrassSelect(cell, id, root) != GRASS_LOD) return false;
#else
    id = _GrassVisibleIds[ordinal];
#endif
    GrassLegacyRecord record = _GrassLegacyRecords[id];
    GrassDataPacked packed;
    packed.position = record.position + (_GrassOrigin.xyz - _GrassLegacyBakeOrigin.xyz);
    packed.facing = record.facing; packed.heightWidth = record.heightWidth; packed.windStiffness = record.windStiffness;
    grass = UnpackGrassData(packed);
    grass.widthScale = GrassWidthScale(distance(grass.position, _GrassCamera.xyz));
    facing = grass.facing;
    if (_CameraFacing > 0.0) facing = normalize(lerp(facing, normalize((_WorldSpaceCameraPos - grass.position).xz), _CameraFacing));
    float3 positionOS = input.positionOS.xyz;
    normalOS = float3(0,0,1);
    CalculateGrassVertex(positionOS, normalOS, input.uv, grass);
    float3 right = float3(facing.y,0,-facing.x), forward = float3(facing.x,0,facing.y);
    positionWS = grass.position + right * positionOS.x + float3(0,positionOS.y,0) + forward * positionOS.z;
    normalWS = normalize(right * normalOS.x + float3(0,normalOS.y,0) + forward * normalOS.z);
    return true;
}
float3 GrassLegacyWorldNormal(float3 normal, float2 facing)
{
    return normalize(float3(facing.y,0,-facing.x) * normal.x + float3(0,normal.y,0) + float3(facing.x,0,facing.y) * normal.z);
}

struct GrassLegacyVaryings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float heightGradient : TEXCOORD2;
                float3 rotatedNormal1 : TEXCOORD3;
                float3 rotatedNormal2 : TEXCOORD4;
                float2 widthAndAO : TEXCOORD5;  // x = widthPercent, y = ao
                float3 grassRootWS : TEXCOORD6;
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord : TEXCOORD7;
                #endif
                
            };

GrassLegacyVaryings GrassVertex(GrassLegacyAttributes input, uint instance : SV_InstanceID)
{
    GrassLegacyVaryings output = (GrassLegacyVaryings)0;
    GrassData grass; float3 normalOS; float2 facing;
    if (!GrassLegacyGeometry(input, instance, grass, output.positionWS, output.normalWS, normalOS, facing))
    { output.positionCS = float4(2,2,2,1); return output; }
    float3 positionVS = TransformWorldToView(output.positionWS);
    float3 faceNormalWS = normalize(float3(facing.x,0,facing.y));
    float3 viewDirWS = GetWorldSpaceViewDir(output.positionWS);
    float viewDotNormal = saturate(dot(normalize(faceNormalWS.xz), normalize(viewDirWS.xz)));
    float thicken = easeOut(1.0 - viewDotNormal, 4.0) * smoothstep(0.0,0.2,viewDotNormal);
    positionVS.x += thicken * (input.uv.x - 0.5) * 2.0 * _BladeWidth * 0.5;
    output.positionCS = TransformWViewToHClip(positionVS);
    output.heightGradient = saturate(input.uv.y);
    output.rotatedNormal1 = GrassLegacyWorldNormal(RotateAroundY(normalOS, PI * _NormalRotationAngle), facing);
    output.rotatedNormal2 = GrassLegacyWorldNormal(RotateAroundY(normalOS, -PI * _NormalRotationAngle), facing);
    output.widthAndAO = float2(saturate(input.uv.x), saturate(input.uv.y * 2.0 + 0.3));
    output.grassRootWS = grass.position;
#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    output.shadowCoord = TransformWorldToShadowCoord(output.positionWS);
#endif
    return output;
}
struct GrassLegacyDepthVaryings { float4 positionCS : SV_POSITION; };
GrassLegacyDepthVaryings GrassDepthVertex(GrassLegacyAttributes input, uint instance : SV_InstanceID)
{
    GrassLegacyDepthVaryings output;
    GrassData grass; float3 position, normal, normalOS; float2 facing;
    bool valid = GrassLegacyGeometry(input, instance, grass, position, normal, normalOS, facing);
    output.positionCS = valid ? TransformWorldToHClip(position) : float4(2,2,2,1);
    return output;
}
float3 _LightDirection, _LightPosition;
GrassLegacyDepthVaryings GrassShadowVertex(GrassLegacyAttributes input, uint instance : SV_InstanceID)
{
    GrassLegacyDepthVaryings output;
    GrassData grass; float3 position, normal, normalOS; float2 facing;
    if (!GrassLegacyGeometry(input, instance, grass, position, normal, normalOS, facing))
    { output.positionCS = float4(2,2,2,1); return output; }
#if _CASTING_PUNCTUAL_LIGHT_SHADOW
    float3 lightDirection = normalize(_LightPosition - position);
#else
    float3 lightDirection = _LightDirection;
#endif
    if (dot(lightDirection,lightDirection) < 0.0001) lightDirection = GetMainLight().direction;
    output.positionCS = TransformWorldToHClip(ApplyShadowBias(position,normal,lightDirection));
#if UNITY_REVERSED_Z
    output.positionCS.z = min(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#else
    output.positionCS.z = max(output.positionCS.z, output.positionCS.w * UNITY_NEAR_CLIP_VALUE);
#endif
    return output;
}
half4 GrassDepthFragment(GrassLegacyDepthVaryings input) : SV_Target { return 0; }

half4 GrassFragment(GrassLegacyVaryings input) : SV_Target
            {
                
                
                float t = input.heightGradient;
                float widthPercent = input.widthAndAO.x;
                float ao = lerp(1.0, input.widthAndAO.y, _AmbientOcclusionStrength);
                
                // === ADVANCED COLOR CALCULATION ===
                // 1. Three-point gradient (root -> mid -> tip)
                half3 healthyColor;
                if (t < 0.5)
                {
                    healthyColor = lerp(_RootColor.rgb, _MidColor.rgb, t * 2.0);
                }
                else
                {
                    healthyColor = lerp(_MidColor.rgb, _TipColor.rgb, (t - 0.5) * 2.0);
                }
                
                // 2. Per-blade color noise (using grass root position)
                float2 noiseUV = input.grassRootWS.xz * _ColorNoiseScale;
                float colorNoise = Hash21(noiseUV);
                
                // 3. Mix in dry/dead color based on noise and dryness amount
                float dryMask = saturate(colorNoise * 2.0 - 1.0 + _DryAmount);
                half3 baseColor = lerp(healthyColor, _DryColor.rgb, dryMask * _DryAmount);
                
                // 4. Add subtle hue variation
                float hueShift = (colorNoise - 0.5) * _ColorNoiseStrength;
                baseColor = lerp(baseColor, baseColor * float3(1.0 + hueShift, 1.0, 1.0 - hueShift * 0.5), _ColorNoiseStrength);
                
                // === NORMAL CALCULATION ===
                float3 blendedNormal = lerp(
                    input.rotatedNormal1,
                    input.rotatedNormal2,
                    widthPercent
                );
                float3 normalWS = normalize(blendedNormal);
                
                // === SHADOW COORD ===
                #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                    float4 shadowCoord = input.shadowCoord;
                #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                    float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                #else
                    float4 shadowCoord = float4(0, 0, 0, 0);
                #endif
                
                // === LIGHTING ===
                Light mainLight = GetMainLight(shadowCoord);
                float3 lightDir = mainLight.direction;
                float3 viewDir = normalize(GetWorldSpaceViewDir(input.positionWS));
                
                // Shadow attenuation
                float shadow = mainLight.shadowAttenuation;
                shadow = lerp(shadow, 1.0, _ShadowSoftness * (1.0 - t)); // Softer shadows at base
                
                // Diffuse (wrapped for softer look)
                float NdotL = dot(normalWS, lightDir);
                float wrappedDiffuse = saturate(NdotL * 0.5 + 0.5); // Half-Lambert
                float3 diffuse = baseColor * mainLight.color * wrappedDiffuse * shadow;
                
                // === SUBSURFACE SCATTERING / TRANSLUCENCY ===
                float3 backLightDir = -lightDir + normalWS * 0.3;
                float VdotBackLight = saturate(dot(viewDir, backLightDir));
                float sss = pow(VdotBackLight, _TranslucencyPower) * _Translucency;
                sss *= t;
                float3 subsurface = sss * mainLight.color * baseColor * shadow;
                
                // === SPECULAR ===
                float3 halfDir = normalize(lightDir + viewDir);
                float NdotH = saturate(dot(normalWS, halfDir));
                float spec = pow(NdotH, _SpecularPower) * _SpecularStrength;
                float3 specular = spec * mainLight.color * shadow * t;
                
                // === AMBIENT ===
                float3 ambient = SampleSH(normalWS) * baseColor;
                
                // === FINAL COMBINE ===
                float3 finalColor = (diffuse + subsurface + specular + ambient) * ao;
                
                return half4(finalColor, 1.0);
            }
