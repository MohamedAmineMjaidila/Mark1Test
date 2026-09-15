using System;
using GrassFlow.Editor;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GrassFlow.Tests
{
    public sealed class GrassLegacyParityTests
    {
        [TestCase(GrassSelection.StableField)]
        [TestCase(GrassSelection.LegacyDistanceAndDensity)]
        public void ModularLegacyTextureAndSnapshotCompileForBothPaths(GrassSelection selection)
        {
            const string path = "Assets/Grass/Tests/LegacyWindValidation.asset";
            var configuration = AssetDatabase.LoadAssetAtPath<GrassConfiguration>("Assets/Grass/Scenes/SampleScene/Legacy/Legacy Configuration.asset");
            var profile = UnityEngine.Object.Instantiate(configuration.profileTemplate);
            try
            {
                profile.algorithms.model = GrassRendererModel.Modular;
                profile.algorithms.selection = selection;
                profile.lod0Distance = 15; profile.lod1Distance = 40;
                AssetDatabase.CreateAsset(profile,path); GrassProfileCompiler.Compile(profile);
                Assert.That(profile.IsValid(configuration.field,out string error),Is.True,error);
                foreach (var shader in new[] {profile.lod0Shader,profile.lod1Shader,profile.lod2Shader})
                    foreach (bool direct in new[] {false,true})
                    {
                        var material = new Material(shader);
                        try
                        {
                            if(direct) material.EnableKeyword("GRASS_DIRECT_GRID");
                            Assert.That(material.SetPass(0),Is.True); Assert.That(ShaderUtil.ShaderHasError(shader),Is.False,shader.name);
                        }
                        finally { UnityEngine.Object.DestroyImmediate(material); }
                    }
            }
            finally { AssetDatabase.DeleteAsset(path); if(profile) UnityEngine.Object.DestroyImmediate(profile); }
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ImmutableSelectionMatchesOriginalCullCounts(bool orderedLods)
        {
            var config = AssetDatabase.LoadAssetAtPath<GrassConfiguration>("Assets/Grass/Scenes/SampleScene/Legacy/Legacy Configuration.asset");
            Assert.That(config, Is.Not.Null, "Capture the provided scene's legacy configuration first.");
            var field = config.field;
            var profile = UnityEngine.Object.Instantiate(config.profileTemplate);
            var reference = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Grass/Assets/Shaders/GrassCompute.compute"));
            var cameraObject = new GameObject("Legacy parity camera");
            var outputs = new ComputeBuffer[3];
            try
            {
                profile.drawPath = GrassDrawPath.CompactedIds;
                if (orderedLods) { profile.lod0Distance = 15; profile.lod1Distance = 40; }
                var camera = cameraObject.AddComponent<Camera>(); camera.enabled = false;
                camera.transform.position = new Vector3(35.91f,12.642f,-1.81f); camera.transform.rotation = Quaternion.Euler(21.2f,0,0);
                camera.fieldOfView = 50; camera.aspect = 1440f / 2560f;
                var planes = GeometryUtility.CalculateFrustumPlanes(camera); var vectors = new Vector4[6];
                for (int i=0;i<6;i++) vectors[i]=new Vector4(planes[i].normal.x,planes[i].normal.y,planes[i].normal.z,planes[i].distance);
                using (var source = new ComputeBuffer(field.CellCount, GrassLegacyBlade.Stride))
                using (var counter = new ComputeBuffer(3, sizeof(uint), ComputeBufferType.Raw))
                using (var renderer = new GrassFieldRenderer(field, profile))
                {
                    source.SetData(field.LegacySnapshot.GetData<GrassLegacyBlade>());
                    int kernel = reference.FindKernel("CSCull");
                    reference.SetBuffer(kernel,"_SourceGrassBuffer",source); reference.SetInt("_GrassCount",field.CellCount);
                    reference.SetVector("_CameraPosition",camera.transform.position); reference.SetVectorArray("_FrustumPlanes",vectors);
                    reference.SetFloat("_MaxDrawDistance",profile.maxDrawDistance); reference.SetFloat("_MaxDrawDistanceSq",profile.maxDrawDistance*profile.maxDrawDistance);
                    reference.SetFloat("_LOD0DistanceSq",profile.lod0Distance*profile.lod0Distance); reference.SetFloat("_LOD1DistanceSq",profile.lod1Distance*profile.lod1Distance);
                    reference.SetBool("_EnableDensityScaling",true); reference.SetFloat("_MinDensity",profile.minimumDensity);
                    reference.SetFloat("_DensityFalloffStart",profile.densityFalloffStart); reference.SetFloat("_WidthCompensation",profile.widthCompensation);
                    reference.SetBool("_UseOcclusionCulling",false); reference.SetTexture(kernel,"_HiZTexture",Texture2D.blackTexture);
                    for (int lod=0;lod<3;lod++)
                    {
                        outputs[lod]=new ComputeBuffer(field.CellCount,GrassLegacyBlade.Stride,ComputeBufferType.Append);
                        outputs[lod].SetCounterValue(0); reference.SetBuffer(kernel,"_CulledGrassBufferLOD"+lod,outputs[lod]);
                    }
                    reference.Dispatch(kernel,Mathf.CeilToInt(field.CellCount/256f),1,1);
                    for(int lod=0;lod<3;lod++) ComputeBuffer.CopyCount(outputs[lod],counter,lod*sizeof(uint));
                    var expected=new uint[3]; counter.GetData(expected);
                    renderer.Render(camera,field.LegacyBakeOrigin,null,0);
                    for(int lod=0;lod<3;lod++) Assert.That(renderer.GetLodDiagnostics(lod,true).instances,Is.EqualTo(expected[lod]),"Legacy visible LOD "+lod);
                }
                foreach (var shader in new[] {profile.lod0Shader,profile.lod1Shader,profile.lod2Shader})
                {
                    var material=new Material(shader);
                    try { for(int pass=0;pass<material.passCount;pass++) Assert.That(material.SetPass(pass),Is.True,shader.name+" pass "+pass); Assert.That(ShaderUtil.ShaderHasError(shader),Is.False); }
                    finally { UnityEngine.Object.DestroyImmediate(material); }
                }
            }
            finally
            {
                foreach(var buffer in outputs) buffer?.Dispose();
                UnityEngine.Object.DestroyImmediate(reference); UnityEngine.Object.DestroyImmediate(profile); UnityEngine.Object.DestroyImmediate(cameraObject);
            }
        }
    }
}
