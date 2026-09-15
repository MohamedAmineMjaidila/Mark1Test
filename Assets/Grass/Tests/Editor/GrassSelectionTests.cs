using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace GrassFlow.Tests
{
    public sealed class GrassSelectionTests
    {
        [TestCase(false)]
        [TestCase(true)]
        public void GpuSelectionMatchesCpuAndGuardsOverflow(bool forceOverflow)
        {
            if (!SystemInfo.supportsComputeShaders) Assert.Ignore("Compute shaders unavailable on this test device.");
            const int columns = 35, rows = 43, tileColumns = 2, seed = 1729;
            const float spacing = 0.067f;
            var resources = new List<GraphicsBuffer>();
            var shader = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/Grass/Assets/Shaders/Experimental/GrassSelect.compute"));
            Texture2D height = new Texture2D(2, 2, TextureFormat.RFloat, false, true) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            height.SetPixelData(new float[4], 0); height.Apply();
            GraphicsBuffer Buffer(GraphicsBuffer.Target target, int count, int stride)
            {
                var buffer = new GraphicsBuffer(target, count, stride); resources.Add(buffer); return buffer;
            }
            try
            {
                int[] words = new int[GrassGrid.WordsPerRow(columns) * rows];
                for (int row = 0; row < rows; row++)
                    for (int column = 0; column < columns; column++)
                        if ((row * 7 + column * 3) % 5 != 0) GrassGrid.SetOccupied(words, row, column, columns);
                GraphicsBuffer occupancy = Buffer(GraphicsBuffer.Target.Structured, words.Length, 4); occupancy.SetData(words);
                GraphicsBuffer tiles = Buffer(GraphicsBuffer.Target.Structured, 4, 4); tiles.SetData(new uint[] { 0, 1, 2, 3 });
                GraphicsBuffer counters = Buffer(GraphicsBuffer.Target.Structured, 4, 4); counters.SetData(new uint[4]);
                var visible = new GraphicsBuffer[3]; var args = new GraphicsBuffer[3];
                int select = shader.FindKernel("SelectTiles"), finalize = shader.FindKernel("BuildArguments");
                int capacity = forceOverflow ? 2 : columns * rows;
                for (int lod = 0; lod < 3; lod++)
                {
                    visible[lod] = Buffer(GraphicsBuffer.Target.Structured, capacity, 4);
                    args[lod] = Buffer(GraphicsBuffer.Target.IndirectArguments | GraphicsBuffer.Target.Raw, GraphicsBuffer.IndirectDrawIndexedArgs.size / 4, 4);
                    args[lod].SetData(new[] { new GraphicsBuffer.IndirectDrawIndexedArgs { indexCountPerInstance = 3 } });
                    shader.SetBuffer(select, "_GrassVisible" + lod, visible[lod]);
                    shader.SetBuffer(finalize, "_GrassArgs" + lod, args[lod]);
                    shader.SetInt("_GrassCapacity" + lod, capacity);
                }
                shader.SetInt("_GrassArgumentInstanceOffset", GrassIndirectLayout.InstanceCountOffset);
                shader.SetBuffer(select, "_GrassOccupancy", occupancy); shader.SetBuffer(select, "_GrassTiles", tiles);
                shader.SetBuffer(select, "_GrassCounters", counters); shader.SetBuffer(finalize, "_GrassCounters", counters);
                shader.SetTexture(select, "_GrassHeights", height);
                shader.SetInt("_GrassColumns", columns); shader.SetInt("_GrassRows", rows); shader.SetInt("_GrassWordsPerRow", GrassGrid.WordsPerRow(columns));
                shader.SetInt("_GrassTileColumns", tileColumns); shader.SetInt("_GrassSeed", seed);
                shader.SetVector("_GrassTerrainSize", new Vector4(columns * spacing, 1, rows * spacing, spacing));
                shader.SetVector("_GrassHeightTextureSize", new Vector4(2, 2, 0.5f, 0.5f));
                shader.SetVector("_GrassOrigin", Vector4.zero);
                Vector2 camera = new Vector2(0.42f, 0.57f);
                shader.SetVector("_GrassCamera", new Vector4(camera.x, 1, camera.y, 0));
                shader.SetVector("_GrassDistance", new Vector4(0.5f, 1, 2, 0));
                shader.SetVector("_GrassDensity", new Vector4(1, 0, 1, 0));
                shader.SetVector("_GrassBlade", new Vector4(0.3f, 1, 0.05f, 0.3f));
                shader.SetVectorArray("_GrassFrustum", new[] { new Vector4(0, 0, 0, 1), new Vector4(0, 0, 0, 1), new Vector4(0, 0, 0, 1), new Vector4(0, 0, 0, 1), new Vector4(0, 0, 0, 1), new Vector4(0, 0, 0, 1) });
                shader.Dispatch(select, 4, 1, 1); shader.Dispatch(finalize, 1, 1, 1);
                uint[] counts = new uint[4]; counters.GetData(counts); // Intentionally synchronous: correctness test, never a performance sample.
                var expected = new[] { new HashSet<uint>(), new HashSet<uint>(), new HashSet<uint>() };
                for (int row = 0; row < rows; row++)
                    for (int column = 0; column < columns; column++)
                    {
                        if (!GrassGrid.Occupied(words, row, column, columns, rows)) continue;
                        float d2 = (GrassGrid.CellCenter(row, column, spacing) - camera).sqrMagnitude;
                        int lod = GrassGrid.Lod(d2, 0.5f, 1, 2);
                        if (lod >= 0) expected[lod].Add(GrassGrid.CellId(row, column, columns));
                    }
                for (int lod = 0; lod < 3; lod++)
                {
                    Assert.That(counts[lod], Is.EqualTo(expected[lod].Count));
                    uint[] argumentValues = new uint[GraphicsBuffer.IndirectDrawIndexedArgs.size / 4]; args[lod].GetData(argumentValues);
                    Assert.That(argumentValues[0], Is.EqualTo(3));
                    Assert.That(argumentValues[GrassIndirectLayout.InstanceCountOffset / sizeof(uint)], Is.EqualTo(Math.Min(capacity, expected[lod].Count)));
                    if (!forceOverflow)
                    {
                        uint[] ids = new uint[counts[lod]]; visible[lod].GetData(ids, 0, 0, ids.Length);
                        CollectionAssert.AreEquivalent(expected[lod], ids);
                    }
                }
                Assert.That(counts[3], forceOverflow ? Is.GreaterThan(0u) : Is.EqualTo(0u));
            }
            finally
            {
                foreach (GraphicsBuffer buffer in resources) buffer.Dispose();
                UnityEngine.Object.DestroyImmediate(shader); UnityEngine.Object.DestroyImmediate(height);
            }
        }
    }
}
