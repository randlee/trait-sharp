using System.Runtime.InteropServices;
using TraitSharp;

namespace TraitSharp.Benchmarks;

/// <summary>
/// Minimal coordinate trait -- matches BenchmarkPoint's X/Y layout.
/// </summary>
[Trait(GenerateLayout = true)]
public partial interface ICoordinate
{
    int X { get; }
    int Y { get; }
}

/// <summary>
/// Size trait -- Width/Height layout.
/// Used by BenchmarkRect for composite trait benchmarks.
/// </summary>
[Trait(GenerateLayout = true)]
public partial interface ISize
{
    int Width { get; }
    int Height { get; }
}

/// <summary>
/// A plain struct with the same layout as System.Drawing.Point.
/// We use our own type because the source generator needs to see [StructLayout]
/// via Roslyn's GetAttributes(), which doesn't work for metadata-only framework types.
/// Uses [ImplementsTrait] (not [RegisterTraitImpl]) so the generator emits
/// the ICoordinateTrait&lt;BenchmarkPoint&gt; implementation needed by TraitSpan extensions.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[ImplementsTrait(typeof(ICoordinate))]
public partial struct BenchmarkPoint
{
    public int X;
    public int Y;

    public BenchmarkPoint(int x, int y)
    {
        X = x;
        Y = y;
    }
}

/// <summary>
/// A plain struct with sequential layout: X, Y, Width, Height.
/// Implements both ICoordinate (offset 0: X,Y) and ISize (offset 8: Width,Height).
/// This benchmarks composite trait access -- two traits projected from one struct.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
[ImplementsTrait(typeof(ICoordinate))]
[ImplementsTrait(typeof(ISize))]
public partial struct BenchmarkRect
{
    public int X;
    public int Y;
    public int Width;
    public int Height;

    public BenchmarkRect(int x, int y, int width, int height)
    {
        X = x;
        Y = y;
        Width = width;
        Height = height;
    }
}
