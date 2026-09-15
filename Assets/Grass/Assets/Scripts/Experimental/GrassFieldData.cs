using System;
using UnityEngine;

namespace GrassFlow
{
    [Serializable]
    public struct GrassTile
    {
        public int id;
        public Bounds bounds;
        public int occupiedCount;
    }

    [CreateAssetMenu(menuName = "GrassFlow/Field Data")]
    public sealed class GrassFieldData : ScriptableObject
    {
        [SerializeField] private int formatVersion = 1;
        [SerializeField] private Vector3 terrainSize;
        [SerializeField] private float cellSpacing = 0.067f;
        [SerializeField] private int seed = 1729;
        [SerializeField] private int columns;
        [SerializeField] private int rows;
        [SerializeField] private int occupiedCount;
        [SerializeField, HideInInspector] private int[] occupancyWords = Array.Empty<int>();
        [SerializeField, HideInInspector] private GrassTile[] tiles = Array.Empty<GrassTile>();
        [SerializeField] private Texture2D heightTexture;
        [SerializeField] private bool analyticCompatible;
        [SerializeField] private Vector4 analyticModel;
        [SerializeField] private string bakeDescription;
        [SerializeField, HideInInspector] private int revision;

        [SerializeField, HideInInspector] private TextAsset legacySnapshot;
        [SerializeField, HideInInspector] private Vector3 legacyBakeOrigin;
        [SerializeField, HideInInspector] private float placementPadding, maximumLegacyHeight;
        public TextAsset LegacySnapshot => legacySnapshot;
        public bool HasLegacySnapshot => legacySnapshot;
        public Vector3 LegacyBakeOrigin => legacyBakeOrigin;
        public float PlacementPadding => placementPadding;
        public float MaximumLegacyHeight => maximumLegacyHeight;
        public void SetLegacySnapshot(TextAsset data, Vector3 origin, float padding, float maxHeight)
        {
            legacySnapshot = data; legacyBakeOrigin = origin; placementPadding = padding; maximumLegacyHeight = maxHeight; revision++;
        }
        public Vector3 TerrainSize => terrainSize;
        public float CellSpacing => cellSpacing;
        public int Seed => seed;
        public int Columns => columns;
        public int Rows => rows;
        public int CellCount => checked(columns * rows);
        public int OccupiedCount => occupiedCount;
        public int TileColumns => (columns + GrassGrid.TileSide - 1) / GrassGrid.TileSide;
        public int TileCount => tiles.Length;
        public long OccupancyBytes => occupancyWords.LongLength * sizeof(uint);
        public Texture2D HeightTexture => heightTexture;
        public bool AnalyticCompatible => analyticCompatible;
        public Vector4 AnalyticModel => analyticModel;
        public string BakeDescription => bakeDescription;
        public int Revision => revision;
        internal int[] Words => occupancyWords;
        internal GrassTile[] Tiles => tiles;

        public bool IsOccupied(int row, int column) => GrassGrid.Occupied(occupancyWords, row, column, columns, rows);

        public bool IsValid(out string error)
        {
            error = null;
            if (formatVersion != 1 || columns <= 0 || rows <= 0 || cellSpacing <= 0 || terrainSize.y <= 0)
                error = "Field dimensions/version are invalid. Bake the field again.";
            else if ((long)columns * rows > int.MaxValue || occupancyWords.LongLength != (long)GrassGrid.WordsPerRow(columns) * rows)
                error = "Field occupancy layout is invalid.";
            else if (tiles.Length != TileColumns * ((rows + 31) / 32) || heightTexture == null)
                error = "Field tile metadata or height texture is missing.";
            return error == null;
        }

        // Editor baking is the only writer. Runtime code only uploads/reads the resulting asset.
        public void SetBakedData(Vector3 size, float spacing, int fieldSeed, int[] words,
            GrassTile[] bakedTiles, int count, Texture2D heights, bool mathematical, Vector4 model, string description, int exactColumns = 0, int exactRows = 0)
        {
            terrainSize = size; cellSpacing = spacing; seed = fieldSeed;
            columns = exactColumns > 0 ? exactColumns : GrassGrid.CellCount(size.x, spacing);
            rows = exactRows > 0 ? exactRows : GrassGrid.CellCount(size.z, spacing);
            legacySnapshot = null; placementPadding = 0; maximumLegacyHeight = 0;
            occupancyWords = words; tiles = bakedTiles; occupiedCount = count;
            heightTexture = heights; analyticCompatible = mathematical; analyticModel = model;
            bakeDescription = description; formatVersion = 1;
            revision++;
            if (!IsValid(out string error)) throw new ArgumentException(error);
        }
    }
}
