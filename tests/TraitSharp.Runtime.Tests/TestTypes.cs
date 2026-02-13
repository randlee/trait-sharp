using System.Runtime.InteropServices;
using TraitSharp;
using TraitSharp.Runtime.Tests;

// Register ExternalPoint for ICoordinate trait — simulates an external type we can't modify
[assembly: RegisterTraitImpl(typeof(ICoordinate), typeof(ExternalPoint))]

namespace TraitSharp.Runtime.Tests
{
    /// <summary>
    /// Simulates an external type we cannot modify (non-partial, like System.Drawing.Point).
    /// Registered for ICoordinate via assembly-level [RegisterTraitImpl] attribute above.
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct ExternalPoint
    {
        public int X;
        public int Y;
    }

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

    [ImplementsTrait(typeof(ICoordinate))]
    [ImplementsTrait(typeof(IColorValue))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct DataPoint
    {
        public int X, Y;
        public byte R, G, B, A;
    }

    [Trait(GenerateLayout = true)]
    public partial interface IPoint2D
    {
        int X { get; }
        int Y { get; }
    }

    [Trait(GenerateLayout = true)]
    public partial interface ISize2D
    {
        int Width { get; }
        int Height { get; }
    }

    [ImplementsTrait(typeof(IPoint2D))]
    [ImplementsTrait(typeof(ISize2D))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Rectangle
    {
        public int X, Y;
        public int Width, Height;
    }

    [ImplementsTrait(typeof(ICoordinate))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct Point3D
    {
        public int X, Y;
        public float Z;
    }

    /// <summary>
    /// Minimal 8-byte struct with [ImplementsTrait] for IPoint2D.
    /// sizeof(SimplePoint) == sizeof(Point2DLayout) => contiguous (stride == layoutSize).
    /// Used in native parity tests to verify AsNativeSpan and contiguous fast paths.
    /// </summary>
    [ImplementsTrait(typeof(IPoint2D))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct SimplePoint
    {
        public int X, Y;

        public SimplePoint(int x, int y) { X = x; Y = y; }
    }
}
