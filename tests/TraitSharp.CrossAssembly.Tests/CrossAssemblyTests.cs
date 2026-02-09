using System;
using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraitSharp;
using TraitSharp.CrossAssembly.Traits;

namespace TraitSharp.CrossAssembly.Tests;

// ═══════════════════════════════════════════════════════════════════════════════
// Cross-Assembly Trait Runtime Tests
// Validates that traits defined in an external assembly work correctly
// when the source generator runs in the consuming assembly.
// ═══════════════════════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────────────────────────────
// Generic algorithms using cross-assembly trait constraints
// ─────────────────────────────────────────────────────────────────────────────

public static class CrossAssemblyAlgorithms
{
    /// <summary>Generic coordinate sum using cross-assembly trait constraint.</summary>
    public static int CoordinateSum<T>(ref T item)
        where T : unmanaged, IExternalCoordinateTrait<T>
    {
        return item.GetX() + item.GetY();
    }

    /// <summary>Generic label using cross-assembly trait constraint.</summary>
    public static string GetLabel<T>(ref T item)
        where T : unmanaged, IExternalLabeledTrait<T>
    {
        return item.Label();
    }

    /// <summary>Generic shape info using cross-assembly trait constraint.</summary>
    public static string ShapeInfo<T>(ref T item)
        where T : unmanaged, IExternalShapeTrait<T>
    {
        return $"area={item.Area():F1}, peri={item.Perimeter():F1}";
    }

    /// <summary>Generic describe using base trait constraint (cross-assembly).</summary>
    public static string DescribeAny<T>(ref T item)
        where T : unmanaged, IExternalBaseTrait<T>
    {
        return item.Describe();
    }

    /// <summary>Generic leaf summary using most-derived constraint (cross-assembly).</summary>
    public static string LeafSummary<T>(ref T item)
        where T : unmanaged, IExternalLeafTrait<T>
    {
        return item.FullSummary();
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// Test Class
// ─────────────────────────────────────────────────────────────────────────────

[TestClass]
public class CrossAssemblyTests
{
    // ═════════════════════════════════════════════════════════════════════════
    // 1. Property-only cross-assembly trait
    // ═════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void ExternalCoordinate_PropertyAccess_ReturnsCorrectValues()
    {
        var p = new ExternalPoint { X = 10, Y = 20, Extra = 3.14f };
        Assert.AreEqual(10, p.GetX());
        Assert.AreEqual(20, p.GetY());
    }

    [TestMethod]
    public void ExternalCoordinate_LayoutCast_ZeroCopy()
    {
        var p = new ExternalPoint { X = 42, Y = 99, Extra = 1.0f };
        ref readonly var layout = ref p.AsExternalCoordinate();

        Assert.AreEqual(42, layout.X);
        Assert.AreEqual(99, layout.Y);

        // Verify zero-copy: same memory location
        Assert.IsTrue(Unsafe.AreSame(
            ref Unsafe.AsRef(in p.X),
            ref Unsafe.AsRef(in layout.X)),
            "Cross-assembly layout cast is not zero-copy!");
    }

    [TestMethod]
    public void ExternalCoordinate_GenericAlgorithm_Works()
    {
        var p = new ExternalPoint { X = 3, Y = 7 };
        Assert.AreEqual(10, CrossAssemblyAlgorithms.CoordinateSum(ref p));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 2. Required method dispatch across assembly boundary
    // ═════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void ExternalLabeled_RequiredMethod_DispatchesCorrectly()
    {
        var w = new Widget { Code = 42, Weight = 1.5f };
        Assert.AreEqual("Widget#42 (1.5kg)", w.Label());
    }

    [TestMethod]
    public void ExternalLabeled_PropertyAccess_Works()
    {
        var w = new Widget { Code = 7, Weight = 2.0f };
        Assert.AreEqual(7, w.GetCode());
    }

    [TestMethod]
    public void ExternalLabeled_GenericAlgorithm_MatchesDirect()
    {
        var w = new Widget { Code = 99, Weight = 0.5f };
        string direct = w.Label();
        string generic = CrossAssemblyAlgorithms.GetLabel(ref w);
        Assert.AreEqual(direct, generic);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 3. Default methods from external trait
    // ═════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void ExternalShape_RequiredArea_DispatchesCorrectly()
    {
        var rect = new ExternalRect { Width = 10f, Height = 5f };
        Assert.AreEqual(50f, rect.Area(), 0.001f);
    }

    [TestMethod]
    public void ExternalShape_DefaultPerimeter_UsesTraitBody()
    {
        var rect = new ExternalRect { Width = 10f, Height = 5f };
        // Default: 2f * (Width + Height) = 2 * 15 = 30
        Assert.AreEqual(30f, rect.Perimeter(), 0.001f);
    }

    [TestMethod]
    public void ExternalShape_OverriddenPerimeter_UsesCustom()
    {
        var circle = new ExternalCircle { Width = 10f, Height = 10f };
        // Custom: 2 * PI * radius = 2 * PI * 5
        float expected = 2f * MathF.PI * 5f;
        Assert.AreEqual(expected, circle.Perimeter(), 0.001f);
    }

    [TestMethod]
    public void ExternalShape_ParameterizedDefault_CrossAssembly()
    {
        var rect = new ExternalRect { Width = 4f, Height = 3f };
        // Area = 12, ScaledArea(2.5) = 12 * 2.5 = 30
        Assert.AreEqual(30f, rect.ScaledArea(2.5f), 0.001f);
    }

    [TestMethod]
    public void ExternalShape_GenericAlgorithm_BothTypes()
    {
        var rect = new ExternalRect { Width = 10f, Height = 5f };
        var circle = new ExternalCircle { Width = 10f, Height = 10f };

        string rectInfo = CrossAssemblyAlgorithms.ShapeInfo(ref rect);
        string circleInfo = CrossAssemblyAlgorithms.ShapeInfo(ref circle);

        Assert.AreEqual("area=50.0, peri=30.0", rectInfo);

        float circleArea = MathF.PI * 25f;
        float circlePeri = 2f * MathF.PI * 5f;
        Assert.AreEqual($"area={circleArea:F1}, peri={circlePeri:F1}", circleInfo);
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 4. Inherited trait dispatch across assembly boundary
    // ═════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void ExternalDerived_InheritedDescribe_Works()
    {
        var item = new BasicItem { Id = 7, CategoryId = 1 };
        Assert.AreEqual("BasicItem(7)", item.Describe());
    }

    [TestMethod]
    public void ExternalDerived_DefaultDetailedDescribe_CallsInherited()
    {
        var item = new BasicItem { Id = 7, CategoryId = 1 };
        // Default: $"{Describe()} [cat={CategoryId}]"
        Assert.AreEqual("BasicItem(7) [cat=1]", item.DetailedDescribe());
    }

    [TestMethod]
    public void ExternalDerived_OverrideDetailedDescribe_UsesCustom()
    {
        var item = new DetailedWidget { Id = 5, CategoryId = 2 };
        Assert.AreEqual("[CUSTOM: 5/cat2]", item.DetailedDescribe());
    }

    [TestMethod]
    public void ExternalDerived_OverrideStillHasInheritedDescribe()
    {
        var item = new DetailedWidget { Id = 5, CategoryId = 2 };
        Assert.AreEqual("DWidget(5)", item.Describe());
    }

    [TestMethod]
    public void ExternalBase_GenericAlgorithm_WorksWithDerived()
    {
        var item = new BasicItem { Id = 3, CategoryId = 1 };
        Assert.AreEqual("BasicItem(3)", CrossAssemblyAlgorithms.DescribeAny(ref item));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 5. Three-level inheritance chain across assembly boundary
    // ═════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void ExternalLeaf_InheritedDescribe_Works()
    {
        var item = new LeafWidget { Id = 1, CategoryId = 3, Priority = 5 };
        Assert.AreEqual("Leaf(1)", item.Describe());
    }

    [TestMethod]
    public void ExternalLeaf_DefaultDetailedDescribe_ChainsThrough()
    {
        var item = new LeafWidget { Id = 1, CategoryId = 3, Priority = 5 };
        // Default: $"{Describe()} [cat={CategoryId}]"
        Assert.AreEqual("Leaf(1) [cat=3]", item.DetailedDescribe());
    }

    [TestMethod]
    public void ExternalLeaf_DefaultFullSummary_ChainsAllLevels()
    {
        var item = new LeafWidget { Id = 1, CategoryId = 3, Priority = 5 };
        // Default: $"[P{Priority}] {DetailedDescribe()}"
        // DetailedDescribe default: $"{Describe()} [{Category}]"
        Assert.AreEqual("[P5] Leaf(1) [cat=3]", item.FullSummary());
    }

    [TestMethod]
    public void ExternalLeaf_OverrideMiddle_LeafUsesOverridden()
    {
        var item = new LeafWidgetOverrideMiddle { Id = 2, CategoryId = 4, Priority = 9 };
        Assert.AreEqual("LeafOvr(2)", item.Describe());
        Assert.AreEqual("CUSTOM-DETAIL(2)", item.DetailedDescribe());
        // FullSummary default calls overridden DetailedDescribe
        Assert.AreEqual("[P9] CUSTOM-DETAIL(2)", item.FullSummary());
    }

    [TestMethod]
    public void ExternalLeaf_GenericAlgorithm_FullChain()
    {
        var item = new LeafWidget { Id = 3, CategoryId = 5, Priority = 1 };
        Assert.AreEqual("[P1] Leaf(3) [cat=5]", CrossAssemblyAlgorithms.LeafSummary(ref item));
    }

    // ═════════════════════════════════════════════════════════════════════════
    // 6. Zero-allocation verification (cross-assembly)
    // ═════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void ExternalCoordinate_PropertyAccess_ZeroAllocation()
    {
        var p = new ExternalPoint { X = 1, Y = 2, Extra = 0f };
        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 10_000; i++)
        {
            _ = p.GetX();
            _ = p.GetY();
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.AreEqual(0, after - before,
            "Cross-assembly property access via trait layout cast should allocate 0 bytes");
    }
}
