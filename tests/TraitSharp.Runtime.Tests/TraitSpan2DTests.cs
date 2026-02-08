using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraitSharp.Runtime;

namespace TraitSharp.Runtime.Tests
{
    [TestClass]
    public class TraitSpan2DTests
    {
        private const int W = 4, H = 3;
        private Rectangle[] _grid = null!;

        [TestInitialize]
        public void Setup()
        {
            _grid = new Rectangle[W * H];
            for (int r = 0; r < H; r++)
                for (int c = 0; c < W; c++)
                    _grid[r * W + c] = new Rectangle
                    {
                        X = c, Y = r, Width = c * 10, Height = r * 10
                    };
        }

        [TestMethod]
        public void ReadOnlyTraitSpan2D_Dimensions()
        {
            var span2D = _grid.AsSpan().AsPoint2DSpan2D(W, H);
            Assert.AreEqual(W, span2D.Width);
            Assert.AreEqual(H, span2D.Height);
            Assert.AreEqual(W * H, span2D.Length);
        }

        [TestMethod]
        public void ReadOnlyTraitSpan2D_RowColIndexing()
        {
            var span2D = _grid.AsSpan().AsPoint2DSpan2D(W, H);
            for (int r = 0; r < H; r++)
                for (int c = 0; c < W; c++)
                {
                    Assert.AreEqual(c, span2D[r, c].X);
                    Assert.AreEqual(r, span2D[r, c].Y);
                }
        }

        [TestMethod]
        public void ReadOnlyTraitSpan2D_GetRow_CorrectLength()
        {
            var span2D = _grid.AsSpan().AsSize2DSpan2D(W, H);
            var row = span2D.GetRow(1);
            Assert.AreEqual(W, row.Length);
            for (int c = 0; c < W; c++)
                Assert.AreEqual(c * 10, row[c].Width);
        }

        [TestMethod]
        public void ReadOnlyTraitSpan2D_Slice_SubRegion()
        {
            var span2D = _grid.AsSpan().AsPoint2DSpan2D(W, H);
            var sub = span2D.Slice(1, 1, 2, 2);  // rows 1-2, cols 1-2
            Assert.AreEqual(2, sub.Width);
            Assert.AreEqual(2, sub.Height);
            Assert.AreEqual(1, sub[0, 0].X);
            Assert.AreEqual(1, sub[0, 0].Y);
            Assert.AreEqual(2, sub[1, 1].X);
            Assert.AreEqual(2, sub[1, 1].Y);
        }

        [TestMethod]
        public void ReadOnlyTraitSpan2D_AsSpan_Flattens()
        {
            var span2D = _grid.AsSpan().AsPoint2DSpan2D(W, H);
            var flat = span2D.AsSpan();
            Assert.AreEqual(W * H, flat.Length);
        }

        [TestMethod]
        public void ReadOnlyTraitSpan2D_EnumerateRows_AllRows()
        {
            var span2D = _grid.AsSpan().AsPoint2DSpan2D(W, H);
            int rowCount = 0;
            foreach (var row in span2D.EnumerateRows())
            {
                Assert.AreEqual(W, row.Length);
                rowCount++;
            }
            Assert.AreEqual(H, rowCount);
        }

        [TestMethod]
        public void TraitSpan2D_MutableAccess_WritesBack()
        {
            var span2D = _grid.AsSpan().AsPoint2DTraitSpan2D(W, H);
            span2D[0, 0].X = 999;
            span2D[0, 0].Y = 888;
            Assert.AreEqual(999, _grid[0].X);
            Assert.AreEqual(888, _grid[0].Y);
        }

        [TestMethod]
        public void TraitSpan2D_Fill_SetsAll()
        {
            var span2D = _grid.AsSpan().AsSize2DTraitSpan2D(W, H);
            span2D.Fill(new Size2DLayout { Width = 7, Height = 3 });
            for (int i = 0; i < _grid.Length; i++)
            {
                Assert.AreEqual(7, _grid[i].Width);
                Assert.AreEqual(3, _grid[i].Height);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void TraitSpan2D_DimensionMismatch_Throws()
        {
            // W*H != source.Length
            var bad = _grid.AsSpan().AsPoint2DSpan2D(W + 1, H);
        }

        [TestMethod]
        public void TraitSpan2D_ImplicitConversion_ToReadOnly()
        {
            TraitSpan2D<Point2DLayout> mutable = _grid.AsSpan().AsPoint2DTraitSpan2D(W, H);
            ReadOnlyTraitSpan2D<Point2DLayout> readOnly = mutable;
            Assert.AreEqual(mutable.Width, readOnly.Width);
            Assert.AreEqual(mutable.Height, readOnly.Height);
        }
    }
}
