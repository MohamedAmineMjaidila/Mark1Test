using System;
using UnityEditor;
using UnityEngine;

namespace GrassFlow.Editor
{
    public static class GrassFieldBaker
    {
        public static void Bake(GrassFieldData destination, TerrainData terrain, Texture2D density,
            float spacing, int seed, float threshold, bool mathematical = false, Vector4 model = default)
        {
            if (destination == null || terrain == null) throw new ArgumentNullException(nameof(terrain));
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(destination)))
                throw new ArgumentException("Save the field asset before baking.");
            if (Application.isPlaying) throw new InvalidOperationException("Bake fields outside Play Mode.");
            int columns = GrassGrid.CellCount(terrain.size.x, spacing);
            int rows = GrassGrid.CellCount(terrain.size.z, spacing);
            _ = checked(columns * rows);
            int[] words = new int[checked(GrassGrid.WordsPerRow(columns) * rows)];
            int tileColumns = (columns + 31) / 32;
            GrassTile[] tiles = new GrassTile[tileColumns * ((rows + 31) / 32)];
            Color32[] densityPixels = density ? density.GetPixels32() : null;
            int count = 0;
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    Vector2 center = GrassGrid.CellCenter(row, column, spacing);
                    float coverage = density ? SampleDensity(densityPixels, density.width, density.height,
                        center.x / terrain.size.x, center.y / terrain.size.z) : 1f;
                    if (coverage < threshold || GrassGrid.Random01(GrassGrid.CellId(row, column, columns), (uint)seed) >= coverage) continue;
                    GrassGrid.SetOccupied(words, row, column, columns);
                    tiles[(row / 32) * tileColumns + column / 32].occupiedCount++;
                    count++;
                }
            }

            int resolution = terrain.heightmapResolution;
            float[,] heights = terrain.GetHeights(0, 0, resolution, resolution);
            Texture2D texture = destination.HeightTexture;
            if (texture == null)
            {
                texture = new Texture2D(resolution, resolution, TextureFormat.RFloat, false, true);
                texture.name = "Grass normalized terrain heights";
                AssetDatabase.AddObjectToAsset(texture, destination);
            }
            else if (texture.width != resolution || texture.height != resolution)
                texture.Reinitialize(resolution, resolution, TextureFormat.RFloat, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            float[] flattened = new float[resolution * resolution];
            for (int y = 0; y < resolution; y++)
                for (int x = 0; x < resolution; x++)
                    flattened[y * resolution + x] = heights[y, x];
            texture.SetPixelData(flattened, 0);
            texture.Apply(false, false);

            for (int index = 0; index < tiles.Length; index++)
            {
                int tx = index % tileColumns, tz = index / tileColumns;
                float x0 = tx * 32 * spacing, z0 = tz * 32 * spacing;
                float x1 = Math.Min((tx + 1) * 32, columns) * spacing;
                float z1 = Math.Min((tz + 1) * 32, rows) * spacing;
                int ix0 = Mathf.Clamp(Mathf.FloorToInt(x0 / terrain.size.x * (resolution - 1)), 0, resolution - 1);
                int iz0 = Mathf.Clamp(Mathf.FloorToInt(z0 / terrain.size.z * (resolution - 1)), 0, resolution - 1);
                int ix1 = Mathf.Clamp(Mathf.CeilToInt(x1 / terrain.size.x * (resolution - 1)), 0, resolution - 1);
                int iz1 = Mathf.Clamp(Mathf.CeilToInt(z1 / terrain.size.z * (resolution - 1)), 0, resolution - 1);
                float low = float.PositiveInfinity, high = float.NegativeInfinity;
                for (int z = iz0; z <= iz1; z++)
                    for (int x = ix0; x <= ix1; x++)
                    {
                        float h = heights[z, x] * terrain.size.y;
                        low = Mathf.Min(low, h); high = Mathf.Max(high, h);
                    }
                // The analytic fixture is evaluated between terrain samples; bound its full amplitude too.
                if (mathematical) { low = Mathf.Min(low, model.x - Mathf.Abs(model.y)); high = Mathf.Max(high, model.x + Mathf.Abs(model.y)); }
                tiles[index].id = index;
                tiles[index].bounds = new Bounds(new Vector3((x0 + x1) * 0.5f, (low + high) * 0.5f, (z0 + z1) * 0.5f),
                    new Vector3(x1 - x0, high - low, z1 - z0));
            }

            Undo.RecordObject(destination, "Bake grass field");
            destination.SetBakedData(terrain.size, spacing, seed, words, tiles, count, texture, mathematical, model,
                $"Terrain={AssetDatabase.GetAssetPath(terrain)}; density={AssetDatabase.GetAssetPath(density)}; threshold={threshold:R}; format=1");
            EditorUtility.SetDirty(destination);
            EditorUtility.SetDirty(texture);
            AssetDatabase.SaveAssets();
        }

        private static float SampleDensity(Color32[] pixels, int width, int height, float u, float v)
        {
            float x = Mathf.Clamp01(u) * (width - 1), y = Mathf.Clamp01(v) * (height - 1);
            int x0 = (int)x, y0 = (int)y, x1 = Math.Min(x0 + 1, width - 1), y1 = Math.Min(y0 + 1, height - 1);
            float bottom = Mathf.Lerp(pixels[y0 * width + x0].r, pixels[y0 * width + x1].r, x - x0);
            float top = Mathf.Lerp(pixels[y1 * width + x0].r, pixels[y1 * width + x1].r, x - x0);
            return Mathf.Lerp(bottom, top, y - y0) / 255f;
        }
    }
}
