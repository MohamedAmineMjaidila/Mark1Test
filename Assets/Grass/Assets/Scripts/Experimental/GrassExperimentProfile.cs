using System;
using UnityEngine;

namespace GrassFlow
{
    public enum GrassDrawPath { CompactedIds, DirectGrid }

    // Serialized compatibility and optional starting recipes; actual choices are independent.
    public enum GrassShaderPreset
    {
        Optimized, Bezier, OneWave, MathematicalHeight, FlatNormal, LegacyRoundness,
        AnalyticRoundness, BladeColor, LegacyColor, BladeDryness, LegacyDryness,
        ExactView, Lambert, AmbientSH, AmbientOff, TransmissionOff, TransmissionLegacy,
        SpecularOff, SpecularLegacy, StabilizationOff, StabilizationTerrain,
        StabilizationPatch, Widening, FloatPrecision, Lod2Fragment, ReferenceShading, MinimalLighting
    }

    [CreateAssetMenu(menuName = "GrassFlow/Experiment Profile")]
    public sealed class GrassExperimentProfile : ScriptableObject
    {
        public GrassDrawPath drawPath;
        [HideInInspector] public GrassShaderPreset preset;
        public GrassAlgorithms algorithms;
        [SerializeField, HideInInspector] private int algorithmVersion;
        [HideInInspector] public string compiledAlgorithmKey;
        [HideInInspector] public bool algorithmsReady;
        public static event Action<GrassExperimentProfile> Validated;
        public void EnsureAlgorithms() { if (algorithmVersion == 0 || algorithms == null) { algorithms = GrassAlgorithms.FromPreset(preset); algorithmVersion = 1; } }
        public void ApplyRecipe(GrassShaderPreset recipe) { preset = recipe; algorithmVersion = 1; algorithms = GrassAlgorithms.FromPreset(recipe); NotifyChanged(); }
        public void NotifyChanged() { algorithmsReady = algorithms != null && compiledAlgorithmKey == algorithms.Key; revision++; Validated?.Invoke(this); }
        [HideInInspector] public Shader lod0Shader;
        [HideInInspector] public Shader lod1Shader;
        [HideInInspector] public Shader lod2Shader;
        [HideInInspector] public ComputeShader selectionShader;

        [Header("Geometry and coverage (freeze during shader A/B)")]
        [Min(0.001f)] public float lod0Distance = 15;
        [Min(0.002f)] public float lod1Distance = 40;
        [Min(0.003f)] public float maxDrawDistance = 100;
        [Range(1, 15)] public int lod0Segments = 5;
        [Range(1, 15)] public int lod1Segments = 3;
        [Range(1, 15)] public int lod2Segments = 1;
        public Vector2 bladeHeights = new Vector2(0.3f, 1f);
        [Min(0.001f)] public float bladeWidth = 0.05f;
        [Range(0, 1)] public float naturalBend = 0.3f;
        [Min(0)] public float densityFalloffStart = 30;
        [Range(0.001f, 1)] public float minimumDensity = 0.1f;
        [Range(0, 1)] public float widthCompensation = 0.3f;
        [Range(1, 3)] public float maximumWidthScale = 1.5f;
        [Min(0)] public float transitionWidth = 3;

        [Header("Wind")]
        public Vector2 windDirection = Vector2.right;
        [Range(0, 2)] public float windStrength = 0.35f;
        [Min(0)] public float windSpeed = 1;
        [Min(0)] public float gustStrength = 0.35f;
        [Min(0)] public float flutterStrength = 0.04f;

        [Tooltip("Wave direction/frequency in world space (radians per metre), speed multiplier, phase offset.")]
        public Vector4 waveA = new Vector4(0.11f, 0.07f, 1.3f, 0);
        public Vector4 waveB = new Vector4(-0.055f, 0.093f, 0.79f, 1.8f);
        [Range(0, 1)] public float secondWaveWeight = 0.35f;
        public Vector4 gustWave = new Vector4(0.017f, 0.023f, 0.37f, 15);
        public Vector2 stiffnessRange = new Vector2(0.8f, 1);
        [Min(1)] public float patchSizeCells = 32;
        [Range(0, 1)] public float colorPatchWeight = 0.8f;
        [Range(0, 1)] public float drynessPatchWeight = 0.85f;
        [Min(0)] public float legacyNoiseFrequency = 0.3f;

        [Header("Shading")]
        public Color rootColor = new Color(0.04f, 0.08f, 0.02f);
        public Color midColor = new Color(0.15f, 0.35f, 0.08f);
        public Color tipColor = new Color(0.45f, 0.55f, 0.15f);
        public Color dryColor = new Color(0.4f, 0.35f, 0.15f);
        public Color skyAmbient = new Color(0.18f, 0.23f, 0.3f);
        public Color groundAmbient = new Color(0.055f, 0.07f, 0.025f);
        [Range(0, 1)] public float colorVariation = 0.2f;
        [Range(0, 1)] public float dryness = 0.15f;
        [Range(0, 1.5f)] public float roundnessRadians = 0.65f;
        public Vector3 normalStabilization = new Vector3(0, 0.3f, 0.75f);
        [Range(0, 1)] public float rootDarkness = 0.4f;
        [Tooltip("Weights for linear, squared and smoothstep AO. Normalized when uploaded.")]
        public Vector3 aoCurveWeights = new Vector3(0, 1, 0);
        [Range(0, 1)] public float transmission = 0.5f;
        [Range(1, 8)] public float transmissionPower = 3;
        [Range(0, 1)] public float specular = 0.1f;
        [Range(8, 128)] public float specularPower = 32;
        [Range(0, 0.5f)] public float widening = 0.15f;

        [Header("Diagnostics")]
        public bool collectCounters;
        [Min(0.25f)] public float counterInterval = 1;
        [NonSerialized] public int revision;

        private void OnValidate() { NotifyChanged(); }

        public string Keyword => preset == GrassShaderPreset.Optimized ? null : "GRASS_TEST_" + preset.ToString().ToUpperInvariant();

        public bool IsValid(GrassFieldData field, out string error)
        {
            error = null;
            if (!lod0Shader || !lod1Shader || !lod2Shader || !selectionShader) error = "Assign all three LOD shaders and the selection compute shader.";
            else if (!(0 < lod0Distance && lod0Distance < lod1Distance && lod1Distance < maxDrawDistance) || !float.IsFinite(maxDrawDistance)) error = "LOD distances must satisfy 0 < near < middle < far.";
            else if (bladeHeights.x <= 0 || bladeHeights.y < bladeHeights.x || bladeWidth <= 0 || !float.IsFinite(bladeHeights.y)) error = "Blade dimensions are invalid.";
            else if (minimumDensity <= 0 || minimumDensity > 1 || densityFalloffStart < 0 || densityFalloffStart >= maxDrawDistance) error = "Density range/falloff is invalid.";
            else if (lod0Segments < 1 || lod1Segments < 1 || lod2Segments < 1 || lod0Segments > 15 || lod1Segments > 15 || lod2Segments > 15) error = "LOD segments must be between 1 and 15.";
            else if (algorithms == null || !algorithmsReady) error = "Algorithm shaders are pending. Select this profile in the Inspector to compile them.";
            else if (algorithms.height == GrassHeight.MathematicalFixture && !field.AnalyticCompatible) error = "Mathematical height requires the matching benchmark terrain fixture. Re-bake sculpted terrain as texture height.";
            return error == null;
        }
    }
}
