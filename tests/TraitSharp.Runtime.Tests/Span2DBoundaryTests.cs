using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraitSharp.Runtime;

namespace TraitSharp.Runtime.Tests
{
    [TestClass]
    public class Span2DBoundaryTests
    {
        // --- Test 11: 1x1 single-element 2D span ---

        [TestMethod]
        public void Span2D_1x1_Grid()
        {
            var data = new Rectangle[] { new() { X = 7, Y = 11, Width = 20, Height = 30 } };
            var span2D = data.AsSpan().AsPoint2DSpan2D(1, 1);

            Assert.AreEqual(1, span2D.Width);
            Assert.AreEqual(1, span2D.Height);
            Assert.AreEqual(1, span2D.Length);
            Assert.AreEqual(7, span2D[0, 0].X);
            Assert.AreEqual(11, span2D[0, 0].Y);
        }

        // --- Test 12: 1xN single row ---

        [TestMethod]
        public void Span2D_1xN_SingleRow()
        {
            const int cols = 5;
            var data = new Rectangle[cols];
            for (int c = 0; c < cols; c++)
                data[c] = new Rectangle { X = c, Y = c * 10, Width = 0, Height = 0 };

            var span2D = data.AsSpan().AsPoint2DSpan2D(cols, 1);

            Assert.AreEqual(cols, span2D.Width);
            Assert.AreEqual(1, span2D.Height);
            Assert.AreEqual(cols, span2D.Length);

            for (int c = 0; c < cols; c++)
            {
                Assert.AreEqual(c, span2D[0, c].X);
                Assert.AreEqual(c * 10, span2D[0, c].Y);
            }
        }

        // --- Test 13: Nx1 single column ---

        [TestMethod]
        public void Span2D_Nx1_SingleColumn()
        {
            const int rows = 4;
            var data = new Rectangle[rows];
            for (int r = 0; r < rows; r++)
                data[r] = new Rectangle { X = r * 5, Y = r * 15, Width = 0, Height = 0 };

            var span2D = data.AsSpan().AsPoint2DSpan2D(1, rows);

            Assert.AreEqual(1, span2D.Width);
            Assert.AreEqual(rows, span2D.Height);
            Assert.AreEqual(rows, span2D.Length);

            for (int r = 0; r < rows; r++)
            {
                Assert.AreEqual(r * 5, span2D[r, 0].X);
                Assert.AreEqual(r * 15, span2D[r, 0].Y);
            }
        }

        // --- Test 14: Access first element [0,0] ---

        [TestMethod]
        public void Span2D_Index_0_0()
        {
            const int W = 3, H = 2;
            var data = new Rectangle[W * H];
            for (int i = 0; i < data.Length; i++)
                data[i] = new Rectangle { X = i + 100, Y = i + 200, Width = 0, Height = 0 };

            var span2D = data.AsSpan().AsPoint2DSpan2D(W, H);

            Assert.AreEqual(100, span2D[0, 0].X);
            Assert.AreEqual(200, span2D[0, 0].Y);
        }

        // --- Test 15: Access last element [Height-1, Width-1] ---

        [TestMethod]
        public void Span2D_Index_LastElement()
        {
            const int W = 3, H = 2;
            var data = new Rectangle[W * H];
            for (int r = 0; r < H; r++)
                for (int c = 0; c < W; c++)
                    data[r * W + c] = new Rectangle { X = c, Y = r, Width = 0, Height = 0 };

            var span2D = data.AsSpan().AsPoint2DSpan2D(W, H);

            Assert.AreEqual(W - 1, span2D[H - 1, W - 1].X);
            Assert.AreEqual(H - 1, span2D[H - 1, W - 1].Y);
        }

        // --- Test 16: Row out of bounds throws ---

        [TestMethod]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void Span2D_Index_RowOutOfBounds_Throws()
        {
            const int W = 3, H = 2;
            var data = new Rectangle[W * H];
            var span2D = data.AsSpan().AsPoint2DSpan2D(W, H);

            _ = span2D[H, 0]; // Should throw IndexOutOfRangeException
        }

        // --- Test 17: Column out of bounds throws ---

        [TestMethod]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void Span2D_Index_ColOutOfBounds_Throws()
        {
            const int W = 3, H = 2;
            var data = new Rectangle[W * H];
            var span2D = data.AsSpan().AsPoint2DSpan2D(W, H);

            _ = span2D[0, W]; // Should throw IndexOutOfRangeException
        }

        // --- Test 18: GetRow first row ---

        [TestMethod]
        public void Span2D_GetRow_First()
        {
            const int W = 4, H = 3;
            var data = new Rectangle[W * H];
            for (int r = 0; r < H; r++)
                for (int c = 0; c < W; c++)
                    data[r * W + c] = new Rectangle { X = c, Y = r, Width = c * 10, Height = r * 10 };

            var span2D = data.AsSpan().AsPoint2DSpan2D(W, H);
            var row = span2D.GetRow(0);

            Assert.AreEqual(W, row.Length);
            for (int c = 0; c < W; c++)
            {
                Assert.AreEqual(c, row[c].X);
                Assert.AreEqual(0, row[c].Y);
            }
        }

        // --- Test 19: GetRow last row ---

        [TestMethod]
        public void Span2D_GetRow_Last()
        {
            const int W = 4, H = 3;
            var data = new Rectangle[W * H];
            for (int r = 0; r < H; r++)
                for (int c = 0; c < W; c++)
                    data[r * W + c] = new Rectangle { X = c, Y = r, Width = c * 10, Height = r * 10 };

            var span2D = data.AsSpan().AsPoint2DSpan2D(W, H);
            var row = span2D.GetRow(H - 1);

            Assert.AreEqual(W, row.Length);
            for (int c = 0; c < W; c++)
            {
                Assert.AreEqual(c, row[c].X);
                Assert.AreEqual(H - 1, row[c].Y);
            }
        }

        // --- Test 20: GetRow out of bounds throws ---

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Span2D_GetRow_OutOfBounds_Throws()
        {
            const int W = 4, H = 3;
            var data = new Rectangle[W * H];
            var span2D = data.AsSpan().AsPoint2DSpan2D(W, H);

            _ = span2D.GetRow(H); // Should throw ArgumentOutOfRangeException
        }

        // --- Test 21: Slice full region ---

        [TestMethod]
        public void Span2D_SliceFull()
        {
            const int W = 3, H = 2;
            var data = new Rectangle[W * H];
            for (int r = 0; r < H; r++)
                for (int c = 0; c < W; c++)
                    data[r * W + c] = new Rectangle { X = c, Y = r, Width = 0, Height = 0 };

            var span2D = data.AsSpan().AsPoint2DSpan2D(W, H);
            var full = span2D.Slice(0, 0, H, W);

            Assert.AreEqual(W, full.Width);
            Assert.AreEqual(H, full.Height);
            Assert.AreEqual(W * H, full.Length);

            for (int r = 0; r < H; r++)
                for (int c = 0; c < W; c++)
                {
                    Assert.AreEqual(span2D[r, c].X, full[r, c].X);
                    Assert.AreEqual(span2D[r, c].Y, full[r, c].Y);
                }
        }

        // --- Test 22: Slice zero dimension ---

        [TestMethod]
        public void Span2D_SliceZeroDimension()
        {
            const int W = 3, H = 2;
            var data = new Rectangle[W * H];
            var span2D = data.AsSpan().AsPoint2DSpan2D(W, H);

            var empty = span2D.Slice(0, 0, 0, W);

            Assert.AreEqual(W, empty.Width);
            Assert.AreEqual(0, empty.Height);
            Assert.IsTrue(empty.IsEmpty);
            Assert.AreEqual(0, empty.Length);
        }

        // --- Test 23: Slice out of bounds throws ---

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Span2D_SliceOutOfBounds_Throws()
        {
            const int W = 3, H = 2;
            var data = new Rectangle[W * H];
            var span2D = data.AsSpan().AsPoint2DSpan2D(W, H);

            _ = span2D.Slice(0, 0, H + 1, W); // Should throw ArgumentOutOfRangeException
        }

        // --- Test 24: IsEmpty with zero height ---

        [TestMethod]
        public void Span2D_IsEmpty_ZeroHeight()
        {
            var data = Array.Empty<Rectangle>();
            var span2D = data.AsSpan().AsPoint2DSpan2D(0, 0);

            // Verify IsEmpty when height is zero (Width*Height == 0)
            Assert.IsTrue(span2D.IsEmpty);
            Assert.AreEqual(0, span2D.Length);

            // Also verify via the static Empty property
            var empty = ReadOnlyTraitSpan2D<Point2DLayout>.Empty;
            Assert.IsTrue(empty.IsEmpty);
            Assert.AreEqual(0, empty.Height);
        }

        // --- Test 25: IsEmpty with zero width ---

        [TestMethod]
        public void Span2D_IsEmpty_ZeroWidth()
        {
            var data = Array.Empty<Rectangle>();
            var span2D = data.AsSpan().AsPoint2DSpan2D(0, 0);

            // Verify IsEmpty when width is zero
            Assert.IsTrue(span2D.IsEmpty);
            Assert.AreEqual(0, span2D.Width);

            // Also verify via the static Empty property which has Width==0
            var empty = TraitSpan2D<Point2DLayout>.Empty;
            Assert.IsTrue(empty.IsEmpty);
            Assert.AreEqual(0, empty.Width);
        }

        // --- Test 26: Clear zeroes all trait fields ---

        [TestMethod]
        public void Span2D_Clear()
        {
            const int W = 2, H = 2;
            var data = new Rectangle[]
            {
                new() { X = 1, Y = 2, Width = 10, Height = 20 },
                new() { X = 3, Y = 4, Width = 30, Height = 40 },
                new() { X = 5, Y = 6, Width = 50, Height = 60 },
                new() { X = 7, Y = 8, Width = 70, Height = 80 },
            };

            var span2D = data.AsSpan().AsPoint2DTraitSpan2D(W, H);
            span2D.Clear();

            // Trait fields (X, Y via Point2D) should be zeroed
            for (int i = 0; i < data.Length; i++)
            {
                Assert.AreEqual(0, data[i].X, $"X not cleared at index {i}");
                Assert.AreEqual(0, data[i].Y, $"Y not cleared at index {i}");
            }

            // Non-trait fields (Width, Height) should be preserved
            Assert.AreEqual(10, data[0].Width);
            Assert.AreEqual(20, data[0].Height);
            Assert.AreEqual(30, data[1].Width);
            Assert.AreEqual(40, data[1].Height);
            Assert.AreEqual(50, data[2].Width);
            Assert.AreEqual(60, data[2].Height);
            Assert.AreEqual(70, data[3].Width);
            Assert.AreEqual(80, data[3].Height);
        }

        // --- Test 27: EnumerateRows on empty span ---

        [TestMethod]
        public void Span2D_EnumerateRows_Empty()
        {
            var data = Array.Empty<Rectangle>();
            var span2D = data.AsSpan().AsPoint2DSpan2D(0, 0);

            int rowCount = 0;
            foreach (var row in span2D.EnumerateRows())
            {
                rowCount++;
            }

            Assert.AreEqual(0, rowCount);
        }

        // --- Test 28: EnumerateRows on single-row span ---

        [TestMethod]
        public void Span2D_EnumerateRows_SingleRow()
        {
            const int W = 3;
            var data = new Rectangle[W];
            for (int c = 0; c < W; c++)
                data[c] = new Rectangle { X = c, Y = 0, Width = 0, Height = 0 };

            var span2D = data.AsSpan().AsPoint2DSpan2D(W, 1);

            int rowCount = 0;
            foreach (var row in span2D.EnumerateRows())
            {
                Assert.AreEqual(W, row.Length);
                for (int c = 0; c < W; c++)
                    Assert.AreEqual(c, row[c].X);
                rowCount++;
            }

            Assert.AreEqual(1, rowCount);
        }
    }
}
