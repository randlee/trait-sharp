using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraitSharp.Runtime;

namespace TraitSharp.Runtime.Tests
{
    [TestClass]
    public class TraitSpanTests
    {
        private Rectangle[] _data = null!;

        [TestInitialize]
        public void Setup()
        {
            _data = new Rectangle[10];
            for (int i = 0; i < _data.Length; i++)
                _data[i] = new Rectangle { X = i, Y = i * 10, Width = i + 1, Height = (i + 1) * 2 };
        }

        [TestMethod]
        public void ReadOnlyTraitSpan_Length_MatchesSource()
        {
            var span = _data.AsSpan().AsSize2DSpan();
            Assert.AreEqual(_data.Length, span.Length);
        }

        [TestMethod]
        public void ReadOnlyTraitSpan_Indexer_ReturnsCorrectValues()
        {
            var span = _data.AsSpan().AsSize2DSpan();
            for (int i = 0; i < span.Length; i++)
            {
                Assert.AreEqual(i + 1, span[i].Width);
                Assert.AreEqual((i + 1) * 2, span[i].Height);
            }
        }

        [TestMethod]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void ReadOnlyTraitSpan_Indexer_BoundsCheck()
        {
            var span = _data.AsSpan().AsSize2DSpan();
            _ = span[span.Length]; // Should throw
        }

        [TestMethod]
        public void ReadOnlyTraitSpan_Slice_SubsetCorrect()
        {
            var span = _data.AsSpan().AsPoint2DSpan();
            var sliced = span.Slice(3, 4);
            Assert.AreEqual(4, sliced.Length);
            Assert.AreEqual(3, sliced[0].X);
            Assert.AreEqual(6, sliced[3].X);
        }

        [TestMethod]
        public void ReadOnlyTraitSpan_ToArray_Copies()
        {
            var span = _data.AsSpan().AsPoint2DSpan();
            var array = span.ToArray();
            Assert.AreEqual(span.Length, array.Length);
            for (int i = 0; i < array.Length; i++)
            {
                Assert.AreEqual(span[i].X, array[i].X);
                Assert.AreEqual(span[i].Y, array[i].Y);
            }
        }

        [TestMethod]
        public void ReadOnlyTraitSpan_Enumeration_AllElements()
        {
            var span = _data.AsSpan().AsPoint2DSpan();
            int count = 0;
            foreach (ref readonly var pos in span)
            {
                Assert.AreEqual(count, pos.X);
                count++;
            }
            Assert.AreEqual(_data.Length, count);
        }

        [TestMethod]
        public void ReadOnlyTraitSpan_Empty_IsEmpty()
        {
            var empty = ReadOnlyTraitSpan<Point2DLayout>.Empty;
            Assert.IsTrue(empty.IsEmpty);
            Assert.AreEqual(0, empty.Length);
        }

        [TestMethod]
        public void TraitSpan_Fill_SetsAllElements()
        {
            var rects = new Rectangle[5];
            var span = rects.AsSpan().AsSize2DTraitSpan();
            span.Fill(new Size2DLayout { Width = 42, Height = 99 });

            for (int i = 0; i < rects.Length; i++)
            {
                Assert.AreEqual(42, rects[i].Width);
                Assert.AreEqual(99, rects[i].Height);
            }
        }

        [TestMethod]
        public void TraitSpan_Clear_ZeroesTraitFields()
        {
            var rects = new Rectangle[]
            {
                new() { X = 1, Y = 2, Width = 3, Height = 4 }
            };
            rects.AsSpan().AsSize2DTraitSpan().Clear();

            Assert.AreEqual(0, rects[0].Width);
            Assert.AreEqual(0, rects[0].Height);
            // Non-trait fields preserved
            Assert.AreEqual(1, rects[0].X);
            Assert.AreEqual(2, rects[0].Y);
        }

        [TestMethod]
        public void TraitSpan_ImplicitConversion_ToReadOnly()
        {
            TraitSpan<Point2DLayout> mutable = _data.AsSpan().AsPoint2DTraitSpan();
            ReadOnlyTraitSpan<Point2DLayout> readOnly = mutable; // Implicit
            Assert.AreEqual(mutable.Length, readOnly.Length);
            Assert.AreEqual(mutable[0].X, readOnly[0].X);
        }
    }
}
