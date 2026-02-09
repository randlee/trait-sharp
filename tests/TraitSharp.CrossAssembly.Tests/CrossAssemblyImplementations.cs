using System;
using System.Runtime.InteropServices;
using TraitSharp;
using TraitSharp.CrossAssembly.Traits;

namespace TraitSharp.CrossAssembly.Tests;

// ═══════════════════════════════════════════════════════════════════════════════
// Cross-Assembly Trait Implementations
// These structs implement traits defined in a SEPARATE assembly
// (TraitSharp.CrossAssembly.Traits). The source generator runs HERE,
// discovering trait metadata across the compilation boundary.
// ═══════════════════════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────────────────────────────
// 1. Property-only: IExternalCoordinate
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Implements IExternalCoordinate from external assembly.
/// Validates basic cross-assembly property access and layout generation.
/// </summary>
[ImplementsTrait(typeof(IExternalCoordinate))]
[StructLayout(LayoutKind.Sequential)]
public partial struct ExternalPoint
{
    public int X, Y;
    public float Extra; // additional field beyond trait properties
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. Property + required method: IExternalLabeled
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Implements IExternalLabeled from external assembly.
/// Validates cross-assembly required method dispatch via _Impl pattern.
/// </summary>
[ImplementsTrait(typeof(IExternalLabeled))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Widget
{
    public int Code;
    public float Weight;

    public static string Label_Impl(in Widget self)
        => $"Widget#{self.Code} ({self.Weight:F1}kg)";
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. Default methods: IExternalShape
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Implements IExternalShape: provides required Area, uses default Perimeter and ScaledArea.
/// </summary>
[ImplementsTrait(typeof(IExternalShape))]
[StructLayout(LayoutKind.Sequential)]
public partial struct ExternalRect
{
    public float Width, Height;

    public static float Area_Impl(in ExternalRect self)
        => self.Width * self.Height;

    // Perimeter: uses default => 2f * (Width + Height)
    // ScaledArea: uses default => Area() * factor
}

/// <summary>
/// Implements IExternalShape: provides required Area AND overrides default Perimeter.
/// </summary>
[ImplementsTrait(typeof(IExternalShape))]
[StructLayout(LayoutKind.Sequential)]
public partial struct ExternalCircle
{
    public float Width, Height; // Width = Height = diameter for circle

    public static float Area_Impl(in ExternalCircle self)
    {
        float radius = self.Width / 2f;
        return MathF.PI * radius * radius;
    }

    public static float Perimeter_Impl(in ExternalCircle self)
    {
        float radius = self.Width / 2f;
        return 2f * MathF.PI * radius;
    }

    // ScaledArea: uses default => Area() * factor
}

// ─────────────────────────────────────────────────────────────────────────────
// 4. Trait inheritance: IExternalDerived : IExternalBase
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Implements IExternalDerived (which inherits IExternalBase) from external assembly.
/// Only marks [ImplementsTrait] for the most-derived trait.
/// Must provide Describe_Impl for the inherited required method.
/// Uses default DetailedDescribe.
/// </summary>
[ImplementsTrait(typeof(IExternalDerived))]
[StructLayout(LayoutKind.Sequential)]
public partial struct BasicItem
{
    public int Id;
    public int CategoryId;

    public static string Describe_Impl(in BasicItem self)
        => $"BasicItem({self.Id})";

    // DetailedDescribe: uses default => $"{Describe()} [cat={CategoryId}]"
}

/// <summary>
/// Implements IExternalDerived and overrides the default DetailedDescribe.
/// </summary>
[ImplementsTrait(typeof(IExternalDerived))]
[StructLayout(LayoutKind.Sequential)]
public partial struct DetailedWidget
{
    public int Id;
    public int CategoryId;

    public static string Describe_Impl(in DetailedWidget self)
        => $"DWidget({self.Id})";

    public static string DetailedDescribe_Impl(in DetailedWidget self)
        => $"[CUSTOM: {self.Id}/cat{self.CategoryId}]";
}

// ─────────────────────────────────────────────────────────────────────────────
// 5. Three-level chain: IExternalLeaf : IExternalDerived : IExternalBase
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Implements IExternalLeaf (three-level chain) from external assembly.
/// Only marks [ImplementsTrait] for IExternalLeaf (most-derived).
/// Must provide Describe_Impl for inherited required method.
/// Uses defaults for DetailedDescribe and FullSummary.
/// </summary>
[ImplementsTrait(typeof(IExternalLeaf))]
[StructLayout(LayoutKind.Sequential)]
public partial struct LeafWidget
{
    public int Id;
    public int CategoryId;
    public int Priority;

    public static string Describe_Impl(in LeafWidget self)
        => $"Leaf({self.Id})";

    // DetailedDescribe: default => $"{Describe()} [cat={CategoryId}]"
    // FullSummary: default => $"[P{Priority}] {DetailedDescribe()}"
}

/// <summary>
/// Implements IExternalLeaf and overrides the middle-level default.
/// Tests that the leaf-level default chains through the override.
/// </summary>
[ImplementsTrait(typeof(IExternalLeaf))]
[StructLayout(LayoutKind.Sequential)]
public partial struct LeafWidgetOverrideMiddle
{
    public int Id;
    public int CategoryId;
    public int Priority;

    public static string Describe_Impl(in LeafWidgetOverrideMiddle self)
        => $"LeafOvr({self.Id})";

    public static string DetailedDescribe_Impl(in LeafWidgetOverrideMiddle self)
        => $"CUSTOM-DETAIL({self.Id})";

    // FullSummary: default => $"[P{Priority}] {DetailedDescribe()}"
    // Should use overridden DetailedDescribe
}
