using System;
using NUnit.Framework;
using UnityEngine;

namespace GrassFlow.Tests
{
    public sealed class GrassGridTests
    {
        [Test]
        public void FullFieldUsesExpectedDimensionsAndStorage()
        {
            int columns = GrassGrid.CellCount(80, 0.067f), rows = GrassGrid.CellCount(400, 0.067f);
            Assert.That(columns, Is.EqualTo(1194));
            Assert.That(rows, Is.EqualTo(5970));
            Assert.That(columns * rows, Is.EqualTo(7128180));
            Assert.That(GrassGrid.WordsPerRow(columns) * rows * 4, Is.EqualTo(907440));
        }

        [Test]
        public void WordBoundariesAndPaddingCannotLeakIntoNeighbourRows()
        {
            const int columns = 35, rows = 3;
            int[] words = new int[GrassGrid.WordsPerRow(columns) * rows];
            GrassGrid.SetOccupied(words, 0, 31, columns);
            GrassGrid.SetOccupied(words, 1, 32, columns);
            GrassGrid.SetOccupied(words, 2, 34, columns);
            for (int row = 0; row < rows; row++)
                for (int col = 0; col < columns; col++)
                    Assert.That(GrassGrid.Occupied(words, row, col, columns, rows),
                        Is.EqualTo((row == 0 && col == 31) || (row == 1 && col == 32) || (row == 2 && col == 34)));
            Assert.That(GrassGrid.Occupied(words, 0, 35, columns, rows), Is.False);
            Assert.That(GrassGrid.Occupied(words, -1, 0, columns, rows), Is.False);
        }

        [Test]
        public void IdentityRoundTripsAndDoesNotDependOnDrawOrder()
        {
            const int columns = 1194;
            uint id = GrassGrid.CellId(4321, 1193, columns);
            Assert.That(id / columns, Is.EqualTo(4321));
            Assert.That(id % columns, Is.EqualTo(1193));
            Assert.That(GrassGrid.Hash(0), Is.Zero);
            Assert.That(GrassGrid.Hash(1), Is.EqualTo(1753845952u));
            Assert.That(GrassGrid.Random01(id, 1729), Is.InRange(0f, 0.99999999f));
        }

        [Test]
        public void LodBoundariesAreDisjointAndFarBoundaryIsExcluded()
        {
            Assert.That(GrassGrid.Lod(224.99f, 15, 40, 100), Is.Zero);
            Assert.That(GrassGrid.Lod(225, 15, 40, 100), Is.EqualTo(1));
            Assert.That(GrassGrid.Lod(1600, 15, 40, 100), Is.EqualTo(2));
            Assert.That(GrassGrid.Lod(10000, 15, 40, 100), Is.EqualTo(-1));
        }

        [Test]
        public void CapacityBoundsOffsetCameraCircle()
        {
            const int columns = 120, rows = 700;
            const float spacing = 0.067f, radius = 2.1f;
            foreach (Vector2 camera in new[] { Vector2.zero, new Vector2(3.013f, 5.427f), new Vector2(7.95f, 45) })
            {
                int actual = 0;
                for (int row = 0; row < rows; row++)
                    for (int col = 0; col < columns; col++)
                        if ((GrassGrid.CellCenter(row, col, spacing) - camera).sqrMagnitude < radius * radius) actual++;
                Assert.That(actual, Is.LessThanOrEqualTo(GrassGrid.Capacity(columns, rows, spacing, radius)));
            }
        }

        [Test]
        public void InvalidDimensionsAreRejected()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => GrassGrid.CellCount(80, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => GrassGrid.CellCount(float.NaN, 1));
        }
    }
}
