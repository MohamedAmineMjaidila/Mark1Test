using System;
using System.Collections.Generic;

namespace GrassFlow
{
    public enum GrassBending { NormalizedCurve, CubicBezier }
    public enum GrassWind { TwoWaves, OneWave, Off, LegacyTexture }
    public enum GrassRendererModel { Modular, LegacyReference }
    public enum GrassPlacement { CellGrid, LegacySnapshot }
    public enum GrassSelection { StableField, LegacyDistanceAndDensity }
    public enum GrassHeight { BakedTexture, MathematicalFixture }
    public enum GrassNormal { RoundedVertex, Flat, DualNormalFragment, AnalyticFragment }
    public enum GrassVariation { SmoothPatchAndBlade, PerBlade, LegacyFragmentHash }
    public enum GrassView { SharedCameraDirection, PerFragment }
    public enum GrassDiffuse { Wrapped, Lambert, Unlit }
    public enum GrassAmbient { Hemisphere, SphericalHarmonics, Off }
    public enum GrassTransmission { CheapBacklight, Power, Off }
    public enum GrassSpecular { FixedPower8, Power, Off }
    public enum GrassStabilization { Up, Terrain, Patch, Off }
    public enum GrassFarLighting { Vertex, Fragment }
    public enum GrassPrecision { Half, Float }
    public enum GrassAO { WeightedCurves, Legacy }

    /// <summary>Independent compile-time choices. The Editor specializes only configurations actually used by assets.</summary>
    [Serializable]
    public sealed class GrassAlgorithms
    {
        public GrassRendererModel model;
        public GrassPlacement placement;
        public GrassSelection selection;
        public GrassBending bending;
        public GrassWind wind;
        public GrassHeight height;
        public GrassNormal normals;
        public GrassVariation colorVariation;
        public GrassVariation dryness;
        public GrassView view;
        public GrassDiffuse diffuse;
        public GrassAmbient ambient;
        public GrassTransmission transmission;
        public GrassSpecular specular;
        public GrassStabilization stabilization;
        public GrassFarLighting farLighting;
        public GrassPrecision precision;
        public GrassAO ambientOcclusion;
        public bool viewWidening;

        public string[] Defines()
        {
            if (model == GrassRendererModel.LegacyReference)
                return new[] { "GRASS_TEST_LEGACYMODEL", "GRASS_TEST_LEGACYFIELD", "GRASS_TEST_LEGACYSELECTION" };
            var d = new List<string>();
            void Add(bool condition, string name) { if (condition) d.Add("GRASS_TEST_" + name); }
            Add(placement == GrassPlacement.LegacySnapshot, "LEGACYFIELD");
            Add(selection == GrassSelection.LegacyDistanceAndDensity, "LEGACYSELECTION");
            Add(wind == GrassWind.LegacyTexture, "LEGACYWIND");
            Add(bending == GrassBending.CubicBezier, "BEZIER");
            Add(wind == GrassWind.OneWave, "ONEWAVE"); Add(wind == GrassWind.Off, "WINDOFF");
            Add(height == GrassHeight.MathematicalFixture, "MATHEMATICALHEIGHT");
            Add(normals == GrassNormal.Flat, "FLATNORMAL"); Add(normals == GrassNormal.DualNormalFragment, "LEGACYROUNDNESS");
            Add(normals == GrassNormal.AnalyticFragment, "ANALYTICROUNDNESS");
            Add(colorVariation == GrassVariation.PerBlade, "BLADECOLOR"); Add(colorVariation == GrassVariation.LegacyFragmentHash, "LEGACYCOLOR");
            Add(dryness == GrassVariation.PerBlade, "BLADEDRYNESS"); Add(dryness == GrassVariation.LegacyFragmentHash, "LEGACYDRYNESS");
            Add(view == GrassView.PerFragment, "EXACTVIEW"); Add(diffuse == GrassDiffuse.Lambert, "LAMBERT"); Add(diffuse == GrassDiffuse.Unlit, "MINIMALLIGHTING");
            Add(ambient == GrassAmbient.SphericalHarmonics, "AMBIENTSH"); Add(ambient == GrassAmbient.Off, "AMBIENTOFF");
            Add(transmission == GrassTransmission.Power, "TRANSMISSIONLEGACY"); Add(transmission == GrassTransmission.Off, "TRANSMISSIONOFF");
            Add(specular == GrassSpecular.Power, "SPECULARLEGACY"); Add(specular == GrassSpecular.Off, "SPECULAROFF");
            Add(stabilization == GrassStabilization.Off, "STABILIZATIONOFF"); Add(stabilization == GrassStabilization.Terrain, "STABILIZATIONTERRAIN"); Add(stabilization == GrassStabilization.Patch, "STABILIZATIONPATCH");
            Add(farLighting == GrassFarLighting.Fragment, "LOD2FRAGMENT"); Add(precision == GrassPrecision.Float, "FLOATPRECISION");
            Add(ambientOcclusion == GrassAO.Legacy, "LEGACYAO"); Add(viewWidening, "WIDENING");
            return d.ToArray();
        }
        public string Key => string.Join(";", Defines());

        public static GrassAlgorithms FromPreset(GrassShaderPreset preset)
        {
            var a = new GrassAlgorithms();
            switch (preset)
            {
                case GrassShaderPreset.Bezier: a.bending = GrassBending.CubicBezier; break;
                case GrassShaderPreset.OneWave: a.wind = GrassWind.OneWave; break;
                case GrassShaderPreset.MathematicalHeight: a.height = GrassHeight.MathematicalFixture; break;
                case GrassShaderPreset.FlatNormal: a.normals = GrassNormal.Flat; break;
                case GrassShaderPreset.LegacyRoundness: a.normals = GrassNormal.DualNormalFragment; break;
                case GrassShaderPreset.AnalyticRoundness: a.normals = GrassNormal.AnalyticFragment; break;
                case GrassShaderPreset.BladeColor: a.colorVariation = GrassVariation.PerBlade; break;
                case GrassShaderPreset.LegacyColor: a.colorVariation = GrassVariation.LegacyFragmentHash; break;
                case GrassShaderPreset.BladeDryness: a.dryness = GrassVariation.PerBlade; break;
                case GrassShaderPreset.LegacyDryness: a.dryness = GrassVariation.LegacyFragmentHash; break;
                case GrassShaderPreset.ExactView: a.view = GrassView.PerFragment; break;
                case GrassShaderPreset.Lambert: a.diffuse = GrassDiffuse.Lambert; break;
                case GrassShaderPreset.AmbientSH: a.ambient = GrassAmbient.SphericalHarmonics; break;
                case GrassShaderPreset.AmbientOff: a.ambient = GrassAmbient.Off; break;
                case GrassShaderPreset.TransmissionOff: a.transmission = GrassTransmission.Off; break;
                case GrassShaderPreset.TransmissionLegacy: a.transmission = GrassTransmission.Power; break;
                case GrassShaderPreset.SpecularOff: a.specular = GrassSpecular.Off; break;
                case GrassShaderPreset.SpecularLegacy: a.specular = GrassSpecular.Power; break;
                case GrassShaderPreset.StabilizationOff: a.stabilization = GrassStabilization.Off; break;
                case GrassShaderPreset.StabilizationTerrain: a.stabilization = GrassStabilization.Terrain; break;
                case GrassShaderPreset.StabilizationPatch: a.stabilization = GrassStabilization.Patch; break;
                case GrassShaderPreset.Widening: a.viewWidening = true; break;
                case GrassShaderPreset.FloatPrecision: a.precision = GrassPrecision.Float; break;
                case GrassShaderPreset.Lod2Fragment: a.farLighting = GrassFarLighting.Fragment; break;
                case GrassShaderPreset.MinimalLighting: a.diffuse = GrassDiffuse.Unlit; break;
                case GrassShaderPreset.ReferenceShading:
                    a.normals = GrassNormal.DualNormalFragment; a.view = GrassView.PerFragment;
                    a.colorVariation = a.dryness = GrassVariation.LegacyFragmentHash;
                    a.ambient = GrassAmbient.SphericalHarmonics; a.transmission = GrassTransmission.Power;
                    a.specular = GrassSpecular.Power; a.farLighting = GrassFarLighting.Fragment; a.ambientOcclusion = GrassAO.Legacy;
                    break;
            }
            return a;
        }
    }
}
