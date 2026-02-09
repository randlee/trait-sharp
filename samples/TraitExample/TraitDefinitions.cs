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
// 2b. Default method implementations (Phase 9)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// A trait demonstrating default method implementations.
/// - Describe() has a default body → auto-emitted for types that don't override
/// - Area() is required → each type MUST provide its own _Impl
/// - Perimeter() has a default body → can be overridden selectively
/// </summary>
[Trait(GenerateLayout = true)]
public partial interface IShape
{
    int Tag { get; }

    /// <summary>Default method: returns a generic description using the Tag property.</summary>
    string Describe() { return $"Shape(Tag={Tag})"; }

    /// <summary>Required method: no default body, each implementer must provide Area_Impl.</summary>
    float Area();

    /// <summary>Default method: returns 0 by default, can be overridden.</summary>
    float Perimeter() { return 0f; }
}

/// <summary>
/// Rectangle: overrides Area (required) and Perimeter (default), keeps Describe default.
/// </summary>
[ImplementsTrait(typeof(IShape))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Rectangle
{
    public int Tag;
    public float Width, Height;

    // Required: must provide Area
    public static float Area_Impl(in Rectangle self)
        => self.Width * self.Height;

    // Override default Perimeter
    public static float Perimeter_Impl(in Rectangle self)
        => 2f * (self.Width + self.Height);

    // Describe: uses the default from IShape (not overridden)
}

/// <summary>
/// Circle: overrides Area (required) and Describe (default), keeps Perimeter default.
/// </summary>
[ImplementsTrait(typeof(IShape))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Circle
{
    public int Tag;
    public float Radius;

    // Required: must provide Area
    public static float Area_Impl(in Circle self)
        => MathF.PI * self.Radius * self.Radius;

    // Override default Describe
    public static string Describe_Impl(in Circle self)
        => $"Circle(r={self.Radius:F1}, Tag={self.Tag})";

    // Perimeter: uses the default from IShape (returns 0f)
}

/// <summary>
/// Square: overrides everything (Area required, Describe + Perimeter defaults).
/// Corner case: no default methods used at all.
/// </summary>
[ImplementsTrait(typeof(IShape))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Square
{
    public int Tag;
    public float Side;

    public static float Area_Impl(in Square self)
        => self.Side * self.Side;

    public static string Describe_Impl(in Square self)
        => $"Square(s={self.Side:F1}, Tag={self.Tag})";

    public static float Perimeter_Impl(in Square self)
        => 4f * self.Side;
}

// ─────────────────────────────────────────────────────────────────────────────
// 2c. Trait inheritance with method dispatch (Phase 11)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Base trait: defines a property and a required method.
/// </summary>
[Trait(GenerateLayout = true)]
public partial interface IAnimal
{
    int Legs { get; }

    /// <summary>Required: return the animal's sound.</summary>
    string Sound();
}

/// <summary>
/// Derived trait: inherits Sound() from IAnimal, adds a default Introduce() method.
/// Demonstrates: default body calling an inherited method.
/// </summary>
[Trait(GenerateLayout = true)]
public partial interface IPet : IAnimal
{
    int Affection { get; }

    /// <summary>Default: builds introduction using inherited Sound().</summary>
    string Introduce() => $"I have {Legs} legs, go '{Sound()}', and my affection is {Affection}";
}

/// <summary>
/// Implements base IAnimal only — simple case.
/// </summary>
[ImplementsTrait(typeof(IAnimal))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Snake
{
    public int Legs; // always 0

    public static string Sound_Impl(in Snake self) => "hiss";
}

/// <summary>
/// Implements derived IPet — provides Sound_Impl (inherited required method),
/// uses the default Introduce().
/// </summary>
[ImplementsTrait(typeof(IPet))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Dog
{
    public int Legs;
    public int Affection;

    public static string Sound_Impl(in Dog self) => "woof";
}

/// <summary>
/// Implements derived IPet and overrides the default Introduce().
/// </summary>
[ImplementsTrait(typeof(IPet))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Cat
{
    public int Legs;
    public int Affection;

    public static string Sound_Impl(in Cat self) => "meow";

    public static string Introduce_Impl(in Cat self)
        => $"I'm a cat. I go '{Sound_Impl(in self)}' and my affection is... complicated ({self.Affection})";
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

    /// <summary>
    /// Generic algorithm using IShape trait: works with default and overridden methods.
    /// Describe(), Area(), and Perimeter() all dispatch via the trait constraint.
    /// </summary>
    public static string ShapeSummary<T>(ref T shape)
        where T : unmanaged, IShapeTrait<T>
    {
        return $"{shape.Describe()} → area={shape.Area():F2}, perim={shape.Perimeter():F2}";
    }

    /// <summary>
    /// Generic algorithm using IAnimal base constraint.
    /// Works with any animal, whether it's a base IAnimal or derived IPet.
    /// </summary>
    public static string AnimalInfo<T>(ref T animal)
        where T : unmanaged, IAnimalTrait<T>
    {
        return $"{animal.Sound()} ({animal.GetLegs()} legs)";
    }

    /// <summary>
    /// Generic algorithm using IPet derived constraint.
    /// Can call both inherited Sound() and own Introduce().
    /// </summary>
    public static string PetProfile<T>(ref T pet)
        where T : unmanaged, IPetTrait<T>
    {
        return $"Sound: {pet.Sound()} | Intro: {pet.Introduce()}";
    }
}
