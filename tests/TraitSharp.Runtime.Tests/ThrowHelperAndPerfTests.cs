using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraitSharp.Runtime;

namespace TraitSharp.Runtime.Tests
{
    [TestClass]
    public class ThrowHelperAndPerfTests
    {
        // ---------------------------------------------------------------
        // ThrowHelper direct tests
        // ---------------------------------------------------------------

        [TestMethod]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void ThrowHelper_IndexOutOfRange_Type()
        {
            ThrowHelper.ThrowIndexOutOfRangeException();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void ThrowHelper_ArgumentOutOfRange_Type()
        {
            ThrowHelper.ThrowArgumentOutOfRangeException();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowHelper_DestinationTooShort_Type()
        {
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        }

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void ThrowHelper_InvalidDimensions_Type()
        {
            ThrowHelper.ThrowArgumentException_InvalidDimensions();
        }

        // ---------------------------------------------------------------
        // Performance regression tests (zero-allocation)
        // ---------------------------------------------------------------

        [TestMethod]
        public void Span_Slice_ZeroAllocation()
        {
            var rects = new Rectangle[20];
            for (int i = 0; i < rects.Length; i++)
                rects[i] = new Rectangle { X = i, Y = i * 2, Width = i + 1, Height = (i + 1) * 3 };

            // Warm up: create the span once before measuring
            var span = rects.AsSpan().AsPoint2DSpan();

            long before = GC.GetAllocatedBytesForCurrentThread();

            // Slice multiple times -- all stack-based, no heap allocation expected
            var s1 = span.Slice(2, 5);
            var s2 = span.Slice(0, 10);
            var s3 = s2.Slice(3);
            var s4 = s3.Slice(1, 2);
            // Access elements to prevent dead-code elimination
            int sum = 0;
            for (int i = 0; i < s4.Length; i++)
                sum += s4[i].X;

            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, after - before, "Span.Slice should not allocate");
        }

        [TestMethod]
        public void Span_Fill_ZeroAllocation()
        {
            var rects = new Rectangle[50];
            var span = rects.AsSpan().AsSize2DTraitSpan();
            var fillValue = new Size2DLayout { Width = 42, Height = 99 };

            long before = GC.GetAllocatedBytesForCurrentThread();

            span.Fill(fillValue);

            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, after - before, "Span.Fill should not allocate");

            // Verify the fill actually worked (correctness sanity check)
            Assert.AreEqual(42, rects[0].Width);
            Assert.AreEqual(99, rects[rects.Length - 1].Height);
        }

        [TestMethod]
        public void Span_Clear_ZeroAllocation()
        {
            var rects = new Rectangle[50];
            for (int i = 0; i < rects.Length; i++)
                rects[i] = new Rectangle { X = i, Y = i, Width = i + 1, Height = i + 2 };

            var span = rects.AsSpan().AsSize2DTraitSpan();

            long before = GC.GetAllocatedBytesForCurrentThread();

            span.Clear();

            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, after - before, "Span.Clear should not allocate");

            // Verify clear worked and non-trait fields preserved
            Assert.AreEqual(0, rects[0].Width);
            Assert.AreEqual(0, rects[0].Height);
            Assert.AreEqual(0, rects[0].X, "X (non-Size2D trait field) should be unchanged");
        }

        [TestMethod]
        public void Span2D_Operations_ZeroAllocation()
        {
            const int W = 4, H = 3;
            var grid = new Rectangle[W * H];
            for (int i = 0; i < grid.Length; i++)
                grid[i] = new Rectangle { X = i, Y = i * 10, Width = i + 1, Height = (i + 1) * 2 };

            // Create the 2D span before measuring
            var span2D = grid.AsSpan().AsPoint2DTraitSpan2D(W, H);

            long before = GC.GetAllocatedBytesForCurrentThread();

            // Indexing
            int sum = 0;
            for (int r = 0; r < H; r++)
                for (int c = 0; c < W; c++)
                    sum += span2D[r, c].X;

            // GetRow
            var row0 = span2D.GetRow(0);
            var row1 = span2D.GetRow(1);
            sum += row0[0].X + row1[0].Y;

            // Slice sub-region
            var sub = span2D.Slice(1, 1, 2, 2);
            sum += sub[0, 0].X;

            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, after - before,
                "TraitSpan2D indexing, GetRow, and Slice should not allocate");
        }

        [TestMethod]
        public void MultiTrait_Access_ZeroAllocation()
        {
            // DataPoint implements both ICoordinate and IColorValue
            var points = new DataPoint[100];
            for (int i = 0; i < points.Length; i++)
                points[i] = new DataPoint
                {
                    X = i,
                    Y = i * 2,
                    R = (byte)(i % 256),
                    G = (byte)((i * 3) % 256),
                    B = (byte)((i * 7) % 256),
                    A = 255
                };

            long before = GC.GetAllocatedBytesForCurrentThread();

            // Access two different trait views on the same elements in a loop
            int coordSum = 0;
            int colorSum = 0;
            for (int i = 0; i < points.Length; i++)
            {
                ref readonly var coord = ref points[i].AsCoordinate();
                ref readonly var color = ref points[i].AsColorValue();
                coordSum += coord.X + coord.Y;
                colorSum += color.R + color.G + color.B;
            }

            long after = GC.GetAllocatedBytesForCurrentThread();
            Assert.AreEqual(0L, after - before,
                "Multi-trait access on the same element should not allocate");

            // Ensure the compiler does not optimize away the loop
            Assert.IsTrue(coordSum >= 0, "coordSum should be non-negative");
            Assert.IsTrue(colorSum >= 0, "colorSum should be non-negative");
        }

        [TestMethod]
        public void ToArray_Allocates_ExactSize()
        {
            var rects = new Rectangle[8];
            for (int i = 0; i < rects.Length; i++)
                rects[i] = new Rectangle { X = i, Y = i * 10, Width = i + 1, Height = (i + 1) * 2 };

            var span = rects.AsSpan().AsPoint2DSpan();

            long before = GC.GetAllocatedBytesForCurrentThread();

            var array = span.ToArray();

            long after = GC.GetAllocatedBytesForCurrentThread();

            // ToArray must allocate (it creates a new array)
            Assert.IsTrue(after - before > 0, "ToArray should allocate memory for the new array");

            // Result array must have the expected length
            Assert.AreEqual(rects.Length, array.Length,
                "ToArray result length should match source span length");

            // Verify correctness of copied values
            for (int i = 0; i < array.Length; i++)
            {
                Assert.AreEqual(i, array[i].X, $"array[{i}].X mismatch");
                Assert.AreEqual(i * 10, array[i].Y, $"array[{i}].Y mismatch");
            }
        }
    }
}
