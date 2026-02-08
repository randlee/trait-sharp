using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraitSharp.Runtime;

namespace TraitSharp.Runtime.Tests
{
    [TestClass]
    public class SpanBoundaryTests
    {
        // --- Test 1: Single element span ---

        [TestMethod]
        public void Span_SingleElement()
        {
            var data = new Rectangle[] { new() { X = 42, Y = 99, Width = 10, Height = 20 } };
            var span = data.AsSpan().AsPoint2DSpan();

            Assert.AreEqual(1, span.Length);
            Assert.AreEqual(42, span[0].X);
            Assert.AreEqual(99, span[0].Y);
        }

        // --- Test 2: Index at Length throws ---

        [TestMethod]
        [ExpectedException(typeof(IndexOutOfRangeException))]
        public void Span_IndexAtLength_Throws()
        {
            var data = new Rectangle[]
            {
                new() { X = 1, Y = 2, Width = 3, Height = 4 },
                new() { X = 5, Y = 6, Width = 7, Height = 8 },
            };
            var span = data.AsSpan().AsPoint2DSpan();

            _ = span[span.Length]; // Should throw IndexOutOfRangeException
        }

        // --- Test 3: Slice zero length ---

        [TestMethod]
        public void Span_SliceZeroLength()
        {
            var data = new Rectangle[]
            {
                new() { X = 1, Y = 2, Width = 3, Height = 4 },
                new() { X = 5, Y = 6, Width = 7, Height = 8 },
            };
            var span = data.AsSpan().AsPoint2DSpan();
            var sliced = span.Slice(0, 0);

            Assert.AreEqual(0, sliced.Length);
            Assert.IsTrue(sliced.IsEmpty);
        }

        // --- Test 4: Slice at end ---

        [TestMethod]
        public void Span_SliceAtEnd()
        {
            var data = new Rectangle[]
            {
                new() { X = 1, Y = 2, Width = 3, Height = 4 },
                new() { X = 5, Y = 6, Width = 7, Height = 8 },
                new() { X = 9, Y = 10, Width = 11, Height = 12 },
            };
            var span = data.AsSpan().AsPoint2DSpan();
            var sliced = span.Slice(span.Length);

            Assert.AreEqual(0, sliced.Length);
            Assert.IsTrue(sliced.IsEmpty);
        }

        // --- Test 5: Slice out of bounds throws ---

        [TestMethod]
        [ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void Span_SliceOutOfBounds_Throws()
        {
            var data = new Rectangle[]
            {
                new() { X = 1, Y = 2, Width = 3, Height = 4 },
                new() { X = 5, Y = 6, Width = 7, Height = 8 },
            };
            var span = data.AsSpan().AsPoint2DSpan();

            _ = span.Slice(0, span.Length + 1); // Should throw ArgumentOutOfRangeException
        }

        // --- Test 6: CopyTo exact length ---

        [TestMethod]
        public void Span_CopyTo_ExactLength()
        {
            var data = new Rectangle[]
            {
                new() { X = 10, Y = 20, Width = 30, Height = 40 },
                new() { X = 50, Y = 60, Width = 70, Height = 80 },
                new() { X = 90, Y = 100, Width = 110, Height = 120 },
            };
            var span = data.AsSpan().AsPoint2DSpan();
            var dest = new Point2DLayout[span.Length];

            span.CopyTo(dest);

            for (int i = 0; i < span.Length; i++)
            {
                Assert.AreEqual(span[i].X, dest[i].X);
                Assert.AreEqual(span[i].Y, dest[i].Y);
            }
        }

        // --- Test 7: CopyTo target too small throws ---

        [TestMethod]
        [ExpectedException(typeof(ArgumentException))]
        public void Span_CopyTo_TooSmall_Throws()
        {
            var data = new Rectangle[]
            {
                new() { X = 1, Y = 2, Width = 3, Height = 4 },
                new() { X = 5, Y = 6, Width = 7, Height = 8 },
                new() { X = 9, Y = 10, Width = 11, Height = 12 },
            };
            var span = data.AsSpan().AsPoint2DSpan();
            var dest = new Point2DLayout[span.Length - 1]; // One element too small

            span.CopyTo(dest); // Should throw ArgumentException
        }

        // --- Test 8: Manual stride construction ---

        [TestMethod]
        public void Span_ManualStride_Construction()
        {
            // Construct a TraitSpan directly using the (ref byte, int stride, int length) constructor
            var data = new Rectangle[]
            {
                new() { X = 10, Y = 20, Width = 100, Height = 200 },
                new() { X = 30, Y = 40, Width = 300, Height = 400 },
            };

            int stride = Unsafe.SizeOf<Rectangle>();
            ref byte reference = ref Unsafe.As<Rectangle, byte>(ref data[0]);

            var span = new TraitSpan<Point2DLayout>(ref reference, stride, data.Length);

            Assert.AreEqual(2, span.Length);
            Assert.AreEqual(10, span[0].X);
            Assert.AreEqual(20, span[0].Y);
            Assert.AreEqual(30, span[1].X);
            Assert.AreEqual(40, span[1].Y);
        }

        // --- Test 9: Multiple enumerations ---

        [TestMethod]
        public void Span_MultipleEnumerations()
        {
            var data = new Rectangle[]
            {
                new() { X = 1, Y = 10, Width = 100, Height = 1000 },
                new() { X = 2, Y = 20, Width = 200, Height = 2000 },
                new() { X = 3, Y = 30, Width = 300, Height = 3000 },
            };
            var span = data.AsSpan().AsPoint2DSpan();

            // First enumeration
            var firstPass = new int[span.Length];
            int idx = 0;
            foreach (ref readonly var item in span)
                firstPass[idx++] = item.X;

            // Second enumeration
            var secondPass = new int[span.Length];
            idx = 0;
            foreach (ref readonly var item in span)
                secondPass[idx++] = item.X;

            Assert.AreEqual(firstPass.Length, secondPass.Length);
            for (int i = 0; i < firstPass.Length; i++)
                Assert.AreEqual(firstPass[i], secondPass[i],
                    $"Mismatch at index {i}: first={firstPass[i]}, second={secondPass[i]}");
        }

        // --- Test 10: ReadOnlySpan IsEmpty when zero length ---

        [TestMethod]
        public void ReadOnlySpan_IsEmpty_ZeroLength()
        {
            var empty = ReadOnlyTraitSpan<Point2DLayout>.Empty;

            Assert.IsTrue(empty.IsEmpty);
            Assert.AreEqual(0, empty.Length);
        }
    }
}
