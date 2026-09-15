using System;
using GrassFlow.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GrassFlow.Tests
{
    public sealed class GrassAlgorithmTests
    {
        [TestCase(GrassNormal.AnalyticFragment, GrassStabilization.Terrain)]
        [TestCase(GrassNormal.AnalyticFragment, GrassStabilization.Off)]
        [TestCase(GrassNormal.DualNormalFragment, GrassStabilization.Patch)]
        public void CombinedAlgorithmsCompileForEveryLodAndDrawPath(GrassNormal normal, GrassStabilization stabilization)
        {
            const string path = "Assets/Grass/Tests/AlgorithmValidation.asset";
            var profile = ScriptableObject.CreateInstance<GrassExperimentProfile>();
            try
            {
                profile.ApplyRecipe(GrassShaderPreset.Optimized);
                profile.algorithms.bending = GrassBending.CubicBezier;
                profile.algorithms.wind = GrassWind.Off;
                profile.algorithms.normals = normal;
                profile.algorithms.stabilization = stabilization;
                profile.algorithms.view = GrassView.PerFragment;
                profile.algorithms.colorVariation = GrassVariation.LegacyFragmentHash;
                profile.algorithms.dryness = GrassVariation.PerBlade;
                profile.algorithms.specular = GrassSpecular.Power;
                profile.algorithms.farLighting = GrassFarLighting.Fragment;
                profile.algorithms.precision = GrassPrecision.Float;
                profile.algorithms.viewWidening = true;
                AssetDatabase.CreateAsset(profile, path);
                GrassProfileCompiler.Compile(profile);
                Assert.That(profile.algorithmsReady, Is.True);
                foreach (var shader in new[] { profile.lod0Shader, profile.lod1Shader, profile.lod2Shader })
                    foreach (bool direct in new[] { false, true })
                    {
                        var material = new Material(shader);
                        try
                        {
                            if (direct) material.EnableKeyword("GRASS_DIRECT_GRID");
                            Assert.That(material.SetPass(0), Is.True, shader.name);
                            Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, shader.name);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(material); }
                    }
            }
            finally { AssetDatabase.DeleteAsset(path); if (profile) UnityEngine.Object.DestroyImmediate(profile); }
        }

        [Test]
        public void AllMigratedRecipesHaveSpecializedShaders()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:GrassExperimentProfile", new[] { "Assets/Grass/Benchmark/Profiles" }))
            {
                var profile = AssetDatabase.LoadAssetAtPath<GrassExperimentProfile>(AssetDatabase.GUIDToAssetPath(guid));
                GrassProfileCompiler.Compile(profile);
                Assert.That(profile.algorithmsReady, Is.True, profile.name);
                foreach (var shader in new[] { profile.lod0Shader, profile.lod1Shader, profile.lod2Shader })
                    Assert.That(ShaderUtil.ShaderHasError(shader), Is.False, profile.name);
            }
        }

        [Test]
        public void NumericAndAlgorithmEditsKeepResidentFieldBuffers()
        {
            var field = AssetDatabase.LoadAssetAtPath<GrassFieldData>("Assets/Grass/Benchmark/RepresentativeField.asset");
            var profile = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GrassExperimentProfile>("Assets/Grass/Benchmark/Profiles/CompactedIds_Optimized.asset"));
            try
            {
                using (var renderer = new GrassFieldRenderer(field, profile))
                {
                    profile.windStrength += 0.1f; profile.bladeWidth += 0.01f; profile.NotifyChanged();
                    Assert.That(renderer.NeedsRebuild(field, profile), Is.False, "Numeric edits must preserve occupancy/visibility buffers.");
                    profile.lod0Segments = 7;
                    profile.lod0Shader = AssetDatabase.LoadAssetAtPath<GrassExperimentProfile>("Assets/Grass/Benchmark/Profiles/CompactedIds_Bezier.asset").lod0Shader;
                    Assert.That(renderer.NeedsRebuild(field, profile), Is.False, "Mesh/material edits must preserve field buffers.");
                    profile.maxDrawDistance = 180;
                    Assert.That(renderer.NeedsRebuild(field, profile), Is.True, "Growing visibility capacity requires allocation.");
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(profile); }
        }
        [TestCase(GrassDrawPath.CompactedIds)]
        [TestCase(GrassDrawPath.DirectGrid)]
        public void EveryLodSegmentEditUpdatesMeshAndGpuArguments(GrassDrawPath path)
        {
            var field = AssetDatabase.LoadAssetAtPath<GrassFieldData>("Assets/Grass/Benchmark/RepresentativeField.asset");
            var profile = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GrassExperimentProfile>("Assets/Grass/Benchmark/Profiles/CompactedIds_Optimized.asset"));
            var cameraObject = new GameObject("LOD mutation test camera");
            try
            {
                profile.drawPath = path;
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.transform.position = new Vector3(40, 12, 0); camera.transform.rotation = Quaternion.Euler(21,0,0);
                using (var renderer = new GrassFieldRenderer(field, profile))
                {
                    foreach (int[] counts in new[] { new[] {1,1,1}, new[] {8,5,3}, new[] {15,7,2} })
                    {
                        // Deliberately no OnValidate/NotifyChanged: topology must be observed directly.
                        profile.lod0Segments = counts[0]; profile.lod1Segments = counts[1]; profile.lod2Segments = counts[2];
                        renderer.Render(camera, Vector3.zero, null, 0);
                        for (int lod = 0; lod < 3; lod++)
                        {
                            var d = renderer.GetLodDiagnostics(lod, true);
                            Assert.That(d.segments, Is.EqualTo(counts[lod]), "LOD " + lod);
                            Assert.That(d.vertices, Is.EqualTo(2 * counts[lod] + 1), "LOD " + lod);
                            Assert.That(d.meshIndices, Is.EqualTo((2 * counts[lod] - 1) * 3), "LOD " + lod);
                            Assert.That(d.argumentIndices, Is.EqualTo(d.meshIndices), "GPU indirect draw must use new mesh topology, LOD " + lod);
                        }
                    }
                }
            }
            finally { UnityEngine.Object.DestroyImmediate(cameraObject); UnityEngine.Object.DestroyImmediate(profile); }
        }

    }
}
