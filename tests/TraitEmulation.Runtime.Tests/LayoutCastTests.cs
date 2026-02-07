using System;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TraitEmulation.Runtime.Tests
{
    [TestClass]
    public class LayoutCastTests
    {
        [TestMethod]
        public void PrefixLayout_ZeroCopy_PointerIdentity()
        {
            var dp = new DataPoint { X = 10, Y = 20, R = 255 };
            ref readonly var coord = ref dp.AsCoordinate();

            Assert.AreEqual(10, coord.X);
            Assert.AreEqual(20, coord.Y);

            // Pointer identity proves zero-copy
            Assert.IsTrue(Unsafe.AreSame(
                ref Unsafe.AsRef(in dp.X),
                ref Unsafe.AsRef(in coord.X)),
                "Not zero-copy - references differ!");
        }

        [TestMethod]
        public void OffsetLayout_ZeroCopy_CorrectValues()
        {
            var rect = new Rectangle { X = 1, Y = 2, Width = 100, Height = 50 };

            ref readonly var pos = ref rect.AsPoint2D();
            ref readonly var size = ref rect.AsSize2D();

            Assert.AreEqual(1, pos.X);
            Assert.AreEqual(2, pos.Y);
            Assert.AreEqual(100, size.Width);
            Assert.AreEqual(50, size.Height);
        }

        [TestMethod]
        public void OffsetLayout_ZeroCopy_PointerIdentity()
        {
            var rect = new Rectangle { X = 1, Y = 2, Width = 100, Height = 50 };
            ref readonly var size = ref rect.AsSize2D();

            Assert.IsTrue(Unsafe.AreSame(
                ref Unsafe.AsRef(in rect.Width),
                ref Unsafe.AsRef(in size.Width)),
                "Offset trait is not zero-copy - references differ!");
        }

        [TestMethod]
        public void ExtensionMethod_MatchesLayoutCast()
        {
            var dp = new DataPoint { X = 42, Y = 99 };
            ref readonly var layout = ref dp.AsCoordinate();

            Assert.AreEqual(layout.X, dp.GetX());
            Assert.AreEqual(layout.Y, dp.GetY());
        }

        [TestMethod]
        public void MultipleTrait_SameStruct_IndependentViews()
        {
            var dp = new DataPoint { X = 5, Y = 10, R = 200, G = 100, B = 25, A = 255 };

            ref readonly var coord = ref dp.AsCoordinate();
            ref readonly var color = ref dp.AsColorValue();

            Assert.AreEqual(5, coord.X);
            Assert.AreEqual(10, coord.Y);
            Assert.AreEqual(200, color.R);
            Assert.AreEqual(100, color.G);
            Assert.AreEqual(25, color.B);
        }

        [TestMethod]
        public void ExternalType_ExtensionMethod_ZeroCopy()
        {
            var sysPoint = new ExternalPoint { X = 10, Y = 20 };
            ref readonly var coord = ref sysPoint.AsCoordinate();

            Assert.AreEqual(10, coord.X);
            Assert.AreEqual(20, coord.Y);
        }

        [TestMethod]
        public void ExternalType_ZeroCopy_PointerIdentity()
        {
            var sysPoint = new ExternalPoint { X = 10, Y = 20 };
            ref readonly var coord = ref sysPoint.AsCoordinate();

            // Pointer identity proves zero-copy — the layout overlays the original struct
            Assert.IsTrue(Unsafe.AreSame(
                ref Unsafe.AsRef(in sysPoint.X),
                ref Unsafe.AsRef(in coord.X)),
                "External type adapter is not zero-copy - references differ!");
        }

        [TestMethod]
        public void MutableTraitSpan_WritesBack_ToSourceFields()
        {
            var rects = new Rectangle[]
            {
                new() { X = 0, Y = 0, Width = 10, Height = 10 },
                new() { X = 1, Y = 1, Width = 20, Height = 20 },
            };

            foreach (ref var pos in rects.AsSpan().AsPoint2DTraitSpan())
            {
                pos.X += 100;
                pos.Y += 200;
            }

            Assert.AreEqual(100, rects[0].X);
            Assert.AreEqual(200, rects[0].Y);
            Assert.AreEqual(101, rects[1].X);
            Assert.AreEqual(201, rects[1].Y);
            // Size fields untouched
            Assert.AreEqual(10, rects[0].Width);
            Assert.AreEqual(20, rects[1].Width);
        }
    }
}
