using BenchmarkDotNet.Attributes;
using TraitSharp.Runtime;

namespace TraitSharp.Benchmarks;

/// <summary>
/// 2D sum benchmarks for BenchmarkRect[800*600] — strided access (layout smaller than source).
/// Each benchmark pair does identical work: NativeSpan baseline sums the same fields as the TraitSpan2D variant.
/// </summary>
[Config(typeof(FastBenchmarkConfig))]
public class RectSum2DBenchmarks : RectArraySetupBase
{
    // ── Coordinate view: sum X + Y (2 fields at offset 0) ──

    [Benchmark(Baseline = true)]
    public long NativeSpan_CoordSum2D()
    {
        long sum = 0;
        Span<BenchmarkRect> flat = _array.AsSpan();
        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
            {
                ref var r = ref flat[row * Width + col];
                sum += r.X + r.Y;
            }
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan2D_CoordSum2D()
    {
        long sum = 0;
        var span2d = _array.AsCoordinateSpan2D(Width, Height);
        for (int row = 0; row < span2d.Height; row++)
        {
            for (int col = 0; col < span2d.Width; col++)
            {
                ref readonly var coord = ref span2d[row, col];
                sum += coord.X + coord.Y;
            }
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan2D_CoordRowSum()
    {
        long sum = 0;
        var span2d = _array.AsCoordinateSpan2D(Width, Height);
        for (int row = 0; row < span2d.Height; row++)
        {
            var rowSpan = span2d.GetRow(row);
            for (int i = 0; i < rowSpan.Length; i++)
            {
                ref readonly var coord = ref rowSpan[i];
                sum += coord.X + coord.Y;
            }
        }
        return sum;
    }

    // ── Both views: sum all 4 fields via two trait spans ──

    [Benchmark]
    public long NativeSpan_AllFieldsSum2D()
    {
        long sum = 0;
        Span<BenchmarkRect> flat = _array.AsSpan();
        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
            {
                ref var r = ref flat[row * Width + col];
                sum += r.X + r.Y + r.Width + r.Height;
            }
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan2D_BothSum2D()
    {
        long sum = 0;
        var coordSpan = _array.AsCoordinateSpan2D(Width, Height);
        var sizeSpan = _array.AsSizeSpan2D(Width, Height);
        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
            {
                ref readonly var coord = ref coordSpan[row, col];
                ref readonly var size = ref sizeSpan[row, col];
                sum += coord.X + coord.Y + size.Width + size.Height;
            }
        }
        return sum;
    }
}
