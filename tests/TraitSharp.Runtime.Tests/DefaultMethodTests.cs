using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace TraitSharp.Runtime.Tests
{
    /// <summary>
    /// Runtime tests for Phase 9: Default Method Implementations.
    /// Validates that default method bodies are correctly emitted and dispatched,
    /// and that user overrides take precedence when provided.
    /// </summary>
    [TestClass]
    public class DefaultMethodTests
    {
        // ─────────────────────────────────────────────────────────────────
        // ShapeRect: keeps default Describe, overrides Area + Perimeter
        // ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void ShapeRect_DefaultDescribe_UsesGenericFormat()
        {
            var rect = new ShapeRect { Tag = 1, Width = 10f, Height = 5f };
            // Default body: $"Shape(Tag={Tag})"
            Assert.AreEqual("Shape(Tag=1)", rect.Describe());
        }

        [TestMethod]
        public void ShapeRect_Area_Override_Correct()
        {
            var rect = new ShapeRect { Tag = 1, Width = 10f, Height = 5f };
            Assert.AreEqual(50f, rect.Area(), 0.001f);
        }

        [TestMethod]
        public void ShapeRect_Perimeter_Override_Correct()
        {
            var rect = new ShapeRect { Tag = 1, Width = 10f, Height = 5f };
            Assert.AreEqual(30f, rect.Perimeter(), 0.001f);
        }

        [TestMethod]
        public void ShapeRect_DefaultDescribe_ReflectsTagValue()
        {
            var rect = new ShapeRect { Tag = 999, Width = 1f, Height = 1f };
            Assert.AreEqual("Shape(Tag=999)", rect.Describe());
        }

        [TestMethod]
        public void ShapeRect_DefaultDescribe_ZeroTag()
        {
            var rect = new ShapeRect { Tag = 0, Width = 1f, Height = 1f };
            Assert.AreEqual("Shape(Tag=0)", rect.Describe());
        }

        [TestMethod]
        public void ShapeRect_DefaultDescribe_NegativeTag()
        {
            var rect = new ShapeRect { Tag = -5, Width = 1f, Height = 1f };
            Assert.AreEqual("Shape(Tag=-5)", rect.Describe());
        }

        // ─────────────────────────────────────────────────────────────────
        // ShapeCircle: overrides Describe + Area, keeps default Perimeter
        // ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void ShapeCircle_Describe_Override_CustomFormat()
        {
            var circle = new ShapeCircle { Tag = 2, Radius = 3.5f };
            Assert.AreEqual("Circle(r=3.5, Tag=2)", circle.Describe());
        }

        [TestMethod]
        public void ShapeCircle_Area_Override_Correct()
        {
            var circle = new ShapeCircle { Tag = 2, Radius = 5f };
            float expected = MathF.PI * 25f;
            Assert.AreEqual(expected, circle.Area(), 0.001f);
        }

        [TestMethod]
        public void ShapeCircle_DefaultPerimeter_ReturnsZero()
        {
            var circle = new ShapeCircle { Tag = 2, Radius = 100f };
            // Default body: return 0f
            Assert.AreEqual(0f, circle.Perimeter(), 0.001f);
        }

        [TestMethod]
        public void ShapeCircle_Describe_NotDefaultFormat()
        {
            var circle = new ShapeCircle { Tag = 2, Radius = 1f };
            // Override should NOT use the default "Shape(Tag=X)" format
            Assert.AreNotEqual("Shape(Tag=2)", circle.Describe());
            Assert.IsTrue(circle.Describe().StartsWith("Circle("));
        }

        // ─────────────────────────────────────────────────────────────────
        // ShapeSquare: overrides ALL methods — no defaults used
        // ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void ShapeSquare_Describe_Override_CustomFormat()
        {
            var sq = new ShapeSquare { Tag = 3, Side = 4f };
            Assert.AreEqual("Square(s=4.0, Tag=3)", sq.Describe());
        }

        [TestMethod]
        public void ShapeSquare_Area_Override_Correct()
        {
            var sq = new ShapeSquare { Tag = 3, Side = 4f };
            Assert.AreEqual(16f, sq.Area(), 0.001f);
        }

        [TestMethod]
        public void ShapeSquare_Perimeter_Override_Correct()
        {
            var sq = new ShapeSquare { Tag = 3, Side = 4f };
            Assert.AreEqual(16f, sq.Perimeter(), 0.001f);
        }

        [TestMethod]
        public void ShapeSquare_Describe_NotDefaultFormat()
        {
            var sq = new ShapeSquare { Tag = 3, Side = 1f };
            // Should use override, not default
            Assert.AreNotEqual("Shape(Tag=3)", sq.Describe());
            Assert.IsTrue(sq.Describe().StartsWith("Square("));
        }

        [TestMethod]
        public void ShapeSquare_Perimeter_NotDefaultZero()
        {
            var sq = new ShapeSquare { Tag = 3, Side = 5f };
            // Override should return actual perimeter, not default 0
            Assert.AreNotEqual(0f, sq.Perimeter());
            Assert.AreEqual(20f, sq.Perimeter(), 0.001f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Generic algorithm dispatch with mixed defaults/overrides
        // ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void ShapeSummary_Rect_UsesDefaultDescribe()
        {
            var rect = new ShapeRect { Tag = 1, Width = 3f, Height = 4f };
            string result = DefaultMethodAlgorithms.ShapeSummary(ref rect);
            Assert.IsTrue(result.StartsWith("Shape(Tag=1)"),
                $"Expected default Describe in summary but got: {result}");
            Assert.IsTrue(result.Contains("area=12.00"));
            Assert.IsTrue(result.Contains("perim=14.00"));
        }

        [TestMethod]
        public void ShapeSummary_Circle_UsesOverrideDescribe()
        {
            var circle = new ShapeCircle { Tag = 2, Radius = 1f };
            string result = DefaultMethodAlgorithms.ShapeSummary(ref circle);
            Assert.IsTrue(result.StartsWith("Circle("),
                $"Expected override Describe in summary but got: {result}");
        }

        [TestMethod]
        public void ShapeSummary_Square_AllOverrides()
        {
            var sq = new ShapeSquare { Tag = 3, Side = 2f };
            string result = DefaultMethodAlgorithms.ShapeSummary(ref sq);
            Assert.IsTrue(result.StartsWith("Square("));
            Assert.IsTrue(result.Contains("area=4.00"));
            Assert.IsTrue(result.Contains("perim=8.00"));
        }

        // ─────────────────────────────────────────────────────────────────
        // DefaultOnlyItem: ALL methods use defaults (no overrides)
        // ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void DefaultOnlyItem_Label_UsesDefault()
        {
            var item = new DefaultOnlyItem { Code = 42 };
            Assert.AreEqual("Default-42", item.Label());
        }

        [TestMethod]
        public void DefaultOnlyItem_Priority_UsesDefault()
        {
            var item = new DefaultOnlyItem { Code = 42 };
            Assert.AreEqual(0, item.Priority());
        }

        [TestMethod]
        public void DefaultOnlyItem_Label_ZeroCode()
        {
            var item = new DefaultOnlyItem { Code = 0 };
            Assert.AreEqual("Default-0", item.Label());
        }

        [TestMethod]
        public void DefaultOnlyItem_Label_NegativeCode()
        {
            var item = new DefaultOnlyItem { Code = -1 };
            Assert.AreEqual("Default--1", item.Label());
        }

        [TestMethod]
        public void DefaultOnlyItem_GenericAlgorithm()
        {
            var item = new DefaultOnlyItem { Code = 7 };
            string result = DefaultMethodAlgorithms.DefaultOnlySummary(ref item);
            Assert.AreEqual("Default-7 pri=0", result);
        }

        // ─────────────────────────────────────────────────────────────────
        // ExprDefaultItem: expression-body defaults (arrow syntax)
        // ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void ExprDefaultItem_Doubled_UsesDefault()
        {
            var item = new ExprDefaultItem { Val = 5 };
            Assert.AreEqual(10, item.Doubled());
        }

        [TestMethod]
        public void ExprDefaultItem_Display_UsesDefault()
        {
            var item = new ExprDefaultItem { Val = 5 };
            Assert.AreEqual("V=5", item.Display());
        }

        [TestMethod]
        public void ExprDefaultItem_Doubled_Zero()
        {
            var item = new ExprDefaultItem { Val = 0 };
            Assert.AreEqual(0, item.Doubled());
        }

        [TestMethod]
        public void ExprDefaultItem_Doubled_Negative()
        {
            var item = new ExprDefaultItem { Val = -3 };
            Assert.AreEqual(-6, item.Doubled());
        }

        [TestMethod]
        public void ExprDefaultItem_Display_LargeValue()
        {
            var item = new ExprDefaultItem { Val = 999999 };
            Assert.AreEqual("V=999999", item.Display());
        }

        // ─────────────────────────────────────────────────────────────────
        // Cross-type: different types of same trait dispatch differently
        // ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void IShape_SameTag_DifferentDescribe_DefaultVsOverride()
        {
            var rect = new ShapeRect { Tag = 1, Width = 1f, Height = 1f };
            var circle = new ShapeCircle { Tag = 1, Radius = 1f };
            var square = new ShapeSquare { Tag = 1, Side = 1f };

            // rect uses default Describe → generic format
            Assert.AreEqual("Shape(Tag=1)", rect.Describe());
            // circle overrides Describe → custom format
            Assert.IsTrue(circle.Describe().StartsWith("Circle("));
            // square overrides Describe → custom format
            Assert.IsTrue(square.Describe().StartsWith("Square("));

            // All three are distinct
            Assert.AreNotEqual(rect.Describe(), circle.Describe());
            Assert.AreNotEqual(rect.Describe(), square.Describe());
            Assert.AreNotEqual(circle.Describe(), square.Describe());
        }

        [TestMethod]
        public void IShape_DefaultPerimeter_VsOverridePerimeter()
        {
            var circle = new ShapeCircle { Tag = 1, Radius = 5f };
            var square = new ShapeSquare { Tag = 1, Side = 5f };

            // circle uses default Perimeter → 0
            Assert.AreEqual(0f, circle.Perimeter(), 0.001f);
            // square overrides Perimeter → actual value
            Assert.AreEqual(20f, square.Perimeter(), 0.001f);
        }

        // ─────────────────────────────────────────────────────────────────
        // Tag property: verify layout access works with default methods
        // ─────────────────────────────────────────────────────────────────

        [TestMethod]
        public void ShapeRect_Tag_PropertyAccess()
        {
            var rect = new ShapeRect { Tag = 42 };
            Assert.AreEqual(42, rect.GetTag());
        }

        [TestMethod]
        public void ShapeCircle_Tag_PropertyAccess()
        {
            var circle = new ShapeCircle { Tag = 77 };
            Assert.AreEqual(77, circle.GetTag());
        }
    }
}
