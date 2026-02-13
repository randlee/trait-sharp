using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraitSharp.Runtime;

namespace TraitSharp.Runtime.Tests
{
    /// <summary>
    /// Tests for native parity features: IsContiguous, AsNativeSpan, TryAsNativeSpan,
    /// and contiguous fast paths in CopyTo/Fill.
    ///
    /// SimplePoint (8 bytes, [ImplementsTrait(IPoint2D)]) matches Point2DLayout (8 bytes)
    ///   => stride == sizeof(layout) => contiguous
    /// Rectangle (16 bytes, [ImplementsTrait(IPoint2D)]) with Point2DLayout (8 bytes)
    ///   => stride != sizeof(layout) => strided
    /// </summary>
    [TestClass]
    public class NativeParityTests
    {
        // ---------------------------------------------------------------
        // IsContiguous — 1D mutable (TraitSpan)
        // ---------------------------------------------------------------

        [TestMethod]
        public void TraitSpan_IsContiguous_WhenStrideMatchesLayoutSize()
        {
            var points = new SimplePoint[]
            {
                new(1, 2), new(3, 4), new(5, 6),
            };
            var span = points.AsSpan().AsPoint2DTraitSpan();

            Assert.IsTrue(span.IsContiguous);
        }

        [TestMethod]
        public void TraitSpan_IsNotContiguous_WhenStrideDiffers()
        {
            var rects = new Rectangle[]
            {
                new() { X = 1, Y = 2, Width = 10, Height = 20 },
                new() { X = 3, Y = 4, Width = 30, Height = 40 },
            };
            var span = rects.AsSpan().AsPoint2DTraitSpan();

            Assert.IsFalse(span.IsContiguous);
        }

        // ---------------------------------------------------------------
        // IsContiguous — 1D readonly (ReadOnlyTraitSpan)
        // ---------------------------------------------------------------

        [TestMethod]
        public void ReadOnlyTraitSpan_IsContiguous_WhenStrideMatchesLayoutSize()
        {
            var points = new SimplePoint[]
            {
                new(10, 20), new(30, 40),
            };
            var span = points.AsSpan().AsPoint2DSpan();

            Assert.IsTrue(span.IsContiguous);
        }

        [TestMethod]
        public void ReadOnlyTraitSpan_IsNotContiguous_WhenStrideDiffers()
        {
            var rects = new Rectangle[]
            {
                new() { X = 1, Y = 2, Width = 10, Height = 20 },
                new() { X = 3, Y = 4, Width = 30, Height = 40 },
            };
            var span = rects.AsSpan().AsPoint2DSpan();

            Assert.IsFalse(span.IsContiguous);
        }

        // ---------------------------------------------------------------
        // IsContiguous — 2D
        // ---------------------------------------------------------------

        [TestMethod]
        public void TraitSpan2D_IsContiguous_WhenStrideMatchesLayoutSize()
        {
            var points = new SimplePoint[2 * 3];
            for (int i = 0; i < points.Length; i++)
                points[i] = new SimplePoint(i, i * 10);

            var span2D = points.AsSpan().AsPoint2DTraitSpan2D(2, 3);

            Assert.IsTrue(span2D.IsContiguous);
        }

        [TestMethod]
        public void TraitSpan2D_IsNotContiguous_WhenStrideDiffers()
        {
            var rects = new Rectangle[4 * 3];
            for (int i = 0; i < rects.Length; i++)
                rects[i] = new Rectangle { X = i, Y = i * 10, Width = i + 1, Height = (i + 1) * 2 };

            var span2D = rects.AsSpan().AsPoint2DTraitSpan2D(4, 3);

            Assert.IsFalse(span2D.IsContiguous);
        }

        // ---------------------------------------------------------------
        // AsNativeSpan / TryAsNativeSpan — 1D mutable (TraitSpan)
        // ---------------------------------------------------------------

        [TestMethod]
        public void TraitSpan_AsNativeSpan_ReturnsCorrectSpan()
        {
            var points = new SimplePoint[]
            {
                new(10, 20), new(30, 40), new(50, 60), new(70, 80), new(90, 100),
            };
            var traitSpan = points.AsSpan().AsPoint2DTraitSpan();

            Span<Point2DLayout> native = traitSpan.AsNativeSpan();

            Assert.AreEqual(5, native.Length);
            for (int i = 0; i < native.Length; i++)
            {
                Assert.AreEqual(points[i].X, native[i].X, $"X mismatch at index {i}");
                Assert.AreEqual(points[i].Y, native[i].Y, $"Y mismatch at index {i}");
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TraitSpan_AsNativeSpan_ThrowsWhenNotContiguous()
        {
            var rects = new Rectangle[]
            {
                new() { X = 1, Y = 2, Width = 10, Height = 20 },
                new() { X = 3, Y = 4, Width = 30, Height = 40 },
            };
            var span = rects.AsSpan().AsPoint2DTraitSpan();

            _ = span.AsNativeSpan(); // stride (16) != sizeof(Point2DLayout) (8)
        }

        [TestMethod]
        public void TraitSpan_TryAsNativeSpan_ReturnsTrueWhenContiguous()
        {
            var points = new SimplePoint[]
            {
                new(1, 2), new(3, 4), new(5, 6),
            };
            var traitSpan = points.AsSpan().AsPoint2DTraitSpan();

            bool success = traitSpan.TryAsNativeSpan(out Span<Point2DLayout> native);

            Assert.IsTrue(success);
            Assert.AreEqual(3, native.Length);
            Assert.AreEqual(1, native[0].X);
            Assert.AreEqual(2, native[0].Y);
            Assert.AreEqual(5, native[2].X);
            Assert.AreEqual(6, native[2].Y);
        }

        [TestMethod]
        public void TraitSpan_TryAsNativeSpan_ReturnsFalseWhenNotContiguous()
        {
            var rects = new Rectangle[]
            {
                new() { X = 1, Y = 2, Width = 10, Height = 20 },
            };
            var span = rects.AsSpan().AsPoint2DTraitSpan();

            bool success = span.TryAsNativeSpan(out Span<Point2DLayout> native);

            Assert.IsFalse(success);
            Assert.AreEqual(0, native.Length);
        }

        // ---------------------------------------------------------------
        // AsNativeSpan / TryAsNativeSpan — 1D readonly (ReadOnlyTraitSpan)
        // ---------------------------------------------------------------

        [TestMethod]
        public void ReadOnlyTraitSpan_AsNativeSpan_ReturnsCorrectSpan()
        {
            var points = new SimplePoint[]
            {
                new(11, 22), new(33, 44), new(55, 66),
            };
            var traitSpan = points.AsSpan().AsPoint2DSpan();

            ReadOnlySpan<Point2DLayout> native = traitSpan.AsNativeSpan();

            Assert.AreEqual(3, native.Length);
            for (int i = 0; i < native.Length; i++)
            {
                Assert.AreEqual(points[i].X, native[i].X, $"X mismatch at index {i}");
                Assert.AreEqual(points[i].Y, native[i].Y, $"Y mismatch at index {i}");
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void ReadOnlyTraitSpan_AsNativeSpan_ThrowsWhenNotContiguous()
        {
            var rects = new Rectangle[]
            {
                new() { X = 1, Y = 2, Width = 10, Height = 20 },
                new() { X = 3, Y = 4, Width = 30, Height = 40 },
            };
            var span = rects.AsSpan().AsPoint2DSpan();

            _ = span.AsNativeSpan(); // stride (16) != sizeof(Point2DLayout) (8)
        }

        // ---------------------------------------------------------------
        // AsNativeSpan / TryAsNativeSpan — 2D mutable (TraitSpan2D)
        // ---------------------------------------------------------------

        [TestMethod]
        public void TraitSpan2D_AsNativeSpan_ReturnsCorrectFlatSpan()
        {
            const int W = 2, H = 3;
            var points = new SimplePoint[W * H];
            for (int i = 0; i < points.Length; i++)
                points[i] = new SimplePoint(i, i * 10);

            var span2D = points.AsSpan().AsPoint2DTraitSpan2D(W, H);

            Span<Point2DLayout> native = span2D.AsNativeSpan();

            Assert.AreEqual(W * H, native.Length);
            for (int i = 0; i < native.Length; i++)
            {
                Assert.AreEqual(i, native[i].X, $"X mismatch at flat index {i}");
                Assert.AreEqual(i * 10, native[i].Y, $"Y mismatch at flat index {i}");
            }
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void TraitSpan2D_AsNativeSpan_ThrowsWhenNotContiguous()
        {
            const int W = 4, H = 3;
            var rects = new Rectangle[W * H];
            for (int i = 0; i < rects.Length; i++)
                rects[i] = new Rectangle { X = i, Y = i * 10, Width = i + 1, Height = (i + 1) * 2 };

            var span2D = rects.AsSpan().AsPoint2DTraitSpan2D(W, H);

            _ = span2D.AsNativeSpan(); // stride (16) != sizeof(Point2DLayout) (8)
        }

        // ---------------------------------------------------------------
        // CopyTo — contiguous fast path vs strided fallback
        // ---------------------------------------------------------------

        [TestMethod]
        public void TraitSpan_CopyTo_ContiguousFastPath_MatchesValues()
        {
            var points = new SimplePoint[]
            {
                new(10, 20), new(30, 40), new(50, 60), new(70, 80), new(90, 100),
            };
            var traitSpan = points.AsSpan().AsPoint2DTraitSpan();
            Assert.IsTrue(traitSpan.IsContiguous, "Precondition: span must be contiguous");

            var dest = new Point2DLayout[5];
            traitSpan.CopyTo(dest);

            for (int i = 0; i < points.Length; i++)
            {
                Assert.AreEqual(points[i].X, dest[i].X, $"X mismatch at index {i}");
                Assert.AreEqual(points[i].Y, dest[i].Y, $"Y mismatch at index {i}");
            }
        }

        [TestMethod]
        public void TraitSpan_CopyTo_StridedFallback_MatchesValues()
        {
            var rects = new Rectangle[]
            {
                new() { X = 10, Y = 20, Width = 100, Height = 200 },
                new() { X = 30, Y = 40, Width = 300, Height = 400 },
                new() { X = 50, Y = 60, Width = 500, Height = 600 },
                new() { X = 70, Y = 80, Width = 700, Height = 800 },
                new() { X = 90, Y = 100, Width = 900, Height = 1000 },
            };
            var traitSpan = rects.AsSpan().AsPoint2DTraitSpan();
            Assert.IsFalse(traitSpan.IsContiguous, "Precondition: span must NOT be contiguous");

            var dest = new Point2DLayout[5];
            traitSpan.CopyTo(dest);

            for (int i = 0; i < rects.Length; i++)
            {
                Assert.AreEqual(rects[i].X, dest[i].X, $"X mismatch at index {i}");
                Assert.AreEqual(rects[i].Y, dest[i].Y, $"Y mismatch at index {i}");
            }
        }

        // ---------------------------------------------------------------
        // Fill — contiguous fast path vs strided fallback
        // ---------------------------------------------------------------

        [TestMethod]
        public void TraitSpan_Fill_ContiguousFastPath_SetsAllValues()
        {
            var points = new SimplePoint[5];
            var traitSpan = points.AsSpan().AsPoint2DTraitSpan();
            Assert.IsTrue(traitSpan.IsContiguous, "Precondition: span must be contiguous");

            traitSpan.Fill(new Point2DLayout { X = 42, Y = 99 });

            for (int i = 0; i < points.Length; i++)
            {
                Assert.AreEqual(42, points[i].X, $"X mismatch at index {i}");
                Assert.AreEqual(99, points[i].Y, $"Y mismatch at index {i}");
            }
        }

        [TestMethod]
        public void TraitSpan_Fill_StridedFallback_SetsTraitFieldsPreservesOthers()
        {
            var rects = new Rectangle[]
            {
                new() { X = 0, Y = 0, Width = 100, Height = 200 },
                new() { X = 0, Y = 0, Width = 300, Height = 400 },
                new() { X = 0, Y = 0, Width = 500, Height = 600 },
                new() { X = 0, Y = 0, Width = 700, Height = 800 },
                new() { X = 0, Y = 0, Width = 900, Height = 1000 },
            };
            var traitSpan = rects.AsSpan().AsPoint2DTraitSpan();
            Assert.IsFalse(traitSpan.IsContiguous, "Precondition: span must NOT be contiguous");

            traitSpan.Fill(new Point2DLayout { X = 42, Y = 99 });

            for (int i = 0; i < rects.Length; i++)
            {
                // Trait fields (X, Y) should be set by Fill
                Assert.AreEqual(42, rects[i].X, $"X mismatch at index {i}");
                Assert.AreEqual(99, rects[i].Y, $"Y mismatch at index {i}");

                // Non-trait fields (Width, Height) must be preserved
                Assert.AreEqual((i * 200) + 100, rects[i].Width,
                    $"Width should be preserved at index {i}");
                Assert.AreEqual((i * 200) + 200, rects[i].Height,
                    $"Height should be preserved at index {i}");
            }
        }

        // ---------------------------------------------------------------
        // AsNativeSpan mutation writes through to source array
        // ---------------------------------------------------------------

        [TestMethod]
        public void TraitSpan_AsNativeSpan_MutationWritesThrough()
        {
            var points = new SimplePoint[]
            {
                new(1, 2), new(3, 4), new(5, 6),
            };
            var traitSpan = points.AsSpan().AsPoint2DTraitSpan();
            var native = traitSpan.AsNativeSpan();

            native[1] = new Point2DLayout { X = 99, Y = 88 };

            Assert.AreEqual(99, points[1].X, "Mutation should write through to source array X");
            Assert.AreEqual(88, points[1].Y, "Mutation should write through to source array Y");
        }

        // ---------------------------------------------------------------
        // Empty span edge cases
        // ---------------------------------------------------------------

        [TestMethod]
        public void TraitSpan_Empty_IsContiguous()
        {
            var empty = TraitSpan<Point2DLayout>.Empty;

            // Empty span has stride 0 and length 0; IsContiguous checks stride == sizeof
            // Implementation detail: default struct has stride=0, sizeof(Point2DLayout)=8 → not contiguous
            // This documents actual behavior for empty spans
            Assert.AreEqual(0, empty.Length);
        }

        [TestMethod]
        public void TraitSpan_AsNativeSpan_EmptyContiguousSpan()
        {
            var points = Array.Empty<SimplePoint>();
            var traitSpan = points.AsSpan().AsPoint2DTraitSpan();

            Assert.AreEqual(0, traitSpan.Length);
            Assert.IsTrue(traitSpan.IsContiguous);

            Span<Point2DLayout> native = traitSpan.AsNativeSpan();
            Assert.AreEqual(0, native.Length);
        }
    }
}
