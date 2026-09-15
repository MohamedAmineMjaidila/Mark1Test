using System;
using UnityEngine;

namespace GrassFlow
{
    /// <summary>Canonical field addressing. Keep Hash and occupancy addressing identical to GrassField.hlsl.</summary>
    public static class GrassGrid
    {
        public const int TileSide = 32;

        public static int CellCount(float length, float spacing)
        {
            if (!float.IsFinite(length) || !float.IsFinite(spacing) || length <= 0 || spacing <= 0)
                throw new ArgumentOutOfRangeException(nameof(spacing), "Dimensions and spacing must be finite and positive.");
            double count = Math.Floor(length / (double)spacing);
            if (count < 1 || count > int.MaxValue) throw new ArgumentOutOfRangeException(nameof(length));
            return (int)count;
        }

        public static int WordsPerRow(int columns) => checked((columns + 31) / 32);
        public static uint CellId(int row, int column, int columns) => checked((uint)(row * columns + column));
        public static int WordIndex(int row, int column, int columns) => checked(row * WordsPerRow(columns) + (column >> 5));

        public static bool Occupied(int[] words, int row, int column, int columns, int rows)
        {
            return row >= 0 && row < rows && column >= 0 && column < columns &&
                ((uint)words[WordIndex(row, column, columns)] & (1u << (column & 31))) != 0;
        }

        public static void SetOccupied(int[] words, int row, int column, int columns)
        {
            words[WordIndex(row, column, columns)] |= unchecked((int)(1u << (column & 31)));
        }

        public static uint Hash(uint value)
        {
            unchecked
            {
                value ^= value >> 16; value *= 0x7feb352du;
                value ^= value >> 15; value *= 0x846ca68bu;
                return value ^ (value >> 16);
            }
        }

        public static float Random01(uint id, uint seed) => (Hash(id ^ seed) & 0x00ffffffu) * (1f / 16777216f);

        public static Vector2 CellCenter(int row, int column, float spacing) => new Vector2((column + 0.5f) * spacing, (row + 0.5f) * spacing);

        public static float AnalyticHeight(Vector2 position, Vector4 model)
        {
            return model.x + model.y * Mathf.Sin(position.x * model.z) * Mathf.Cos(position.y * model.w);
        }

        public static int Lod(float distanceSquared, float near, float middle, float far)
        {
            if (distanceSquared >= far * far) return -1;
            return distanceSquared < near * near ? 0 : distanceSquared < middle * middle ? 1 : 2;
        }

        public static int Capacity(int columns, int rows, float spacing, float radius)
        {
            // Any circle of radius R fits inside this many cell centers on either grid axis.
            int side = checked((int)Math.Ceiling(2.0 * radius / spacing) + 2);
            return checked(Math.Min(columns, side) * Math.Min(rows, side));
        }
    }
}
