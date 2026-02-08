using System;
using System.Runtime.InteropServices;
using TraitSharp;

namespace TraitExample;

// ─────────────────────────────────────────────────────────────────────────────
// 1. Define traits - mark interfaces with [Trait] to define contracts
// ─────────────────────────────────────────────────────────────────────────────

[Trait(GenerateLayout = true)]
public partial interface ICoordinate
{
    int X { get; }
    int Y { get; }
}

[Trait(GenerateLayout = true)]
public partial interface IColorValue
{
    byte R { get; }
    byte G { get; }
    byte B { get; }
}

/// <summary>
/// A trait with both a property and a method: demonstrates method trait dispatch.
/// The Tag property provides layout, and Describe() is a method dispatched via the trait.
/// </summary>
[Trait(GenerateLayout = true)]
public partial interface ILabeled
{
    int Tag { get; }
    string Describe();
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. Implement traits on structs - layout must be compatible
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A data point with both coordinate and color traits.
/// The generator verifies {X, Y} and {R, G, B} are layout-compatible.
/// </summary>
[ImplementsTrait(typeof(ICoordinate))]
[ImplementsTrait(typeof(IColorValue))]
[StructLayout(LayoutKind.Sequential)]
public partial struct DataPoint
{
    public int X, Y;
    public byte R, G, B, A;
}

/// <summary>
/// A 3D point that also implements ICoordinate (prefix match).
/// Only X and Y are part of the trait; Z is additional data.
/// </summary>
[ImplementsTrait(typeof(ICoordinate))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Point3D
{
    public int X, Y;
    public float Z;
}

/// <summary>
/// A labeled entity that implements the ILabeled method trait.
/// Tag is the layout property; Describe() is the user-provided method.
/// </summary>
[ImplementsTrait(typeof(ILabeled))]
[StructLayout(LayoutKind.Sequential)]
public partial struct LabeledItem
{
    public int Tag;
    public float Value;

    // Method trait: user provides the implementation body
    public static string Describe_Impl(in LabeledItem self)
    {
        return $"Item(Tag={self.Tag}, Value={self.Value:F1})";
    }
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. Generic algorithms using trait constraints
// ─────────────────────────────────────────────────────────────────────────────

public static class Algorithms
{
    /// <summary>
    /// Compute Euclidean distance between any two coordinate-bearing types.
    /// Works with DataPoint, Point3D, or any type implementing ICoordinate.
    /// Zero boxing, zero allocation - static dispatch via trait constraint.
    /// </summary>
    public static float Distance<T1, T2>(ref T1 p1, ref T2 p2)
        where T1 : unmanaged, ICoordinateTrait<T1>
        where T2 : unmanaged, ICoordinateTrait<T2>
    {
        // Zero-copy: AsCoordinate() returns a ref to the original memory
        ref readonly var c1 = ref p1.AsCoordinate();
        ref readonly var c2 = ref p2.AsCoordinate();

        int dx = c1.X - c2.X;
        int dy = c1.Y - c2.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Compute the luminance (weighted average) of a color value.
    /// Uses the ITU-R BT.601 luma formula.
    /// </summary>
    public static byte ToLuminance<T>(ref T value)
        where T : unmanaged, IColorValueTrait<T>
    {
        ref readonly var rgb = ref value.AsColorValue();
        return (byte)(0.299f * rgb.R + 0.587f * rgb.G + 0.114f * rgb.B);
    }

    /// <summary>
    /// Generic algorithm using method trait: describe any labeled item.
    /// The Describe() extension method is dispatched via the trait constraint.
    /// </summary>
    public static string DescribeItem<T>(ref T item)
        where T : unmanaged, ILabeledTrait<T>
    {
        return item.Describe();
    }
}
