using System.Runtime.InteropServices;
using TraitEmulation;

namespace TraitEmulation.Runtime.Tests
{
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
}
