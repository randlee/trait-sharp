using BenchmarkDotNet.Attributes;
using TraitSharp.Runtime;

namespace TraitSharp.Benchmarks;

/// <summary>
/// 2D array sum benchmarks: sum X + Y over BenchmarkPoint[800*600] with 2D access patterns.
/// Compares native flat-array indexing vs TraitSpan2D element access vs row iteration.
/// All allocations in GlobalSetup — inner loops are identical.
/// </summary>
[Config(typeof(FastBenchmarkConfig))]
public class Sum2DBenchmarks : ArraySetupBase
{
    [Benchmark]
    public long NativeArray_Sum2D()
    {
        long sum = 0;
        var arr = _array;
        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
            {
                ref var pt = ref arr[row * Width + col];
                sum += pt.X + pt.Y;
            }
        }
        return sum;
    }

    [Benchmark(Baseline = true)]
    public long NativeSpan_Sum2D()
    {
        long sum = 0;
        Span<BenchmarkPoint> flat = _array.AsSpan();
        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
            {
                ref var pt = ref flat[row * Width + col];
                sum += pt.X + pt.Y;
            }
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan2D_Sum2D()
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
    public long TraitSpan2D_RowSum()
    {
        long sum = 0;
        var span2d = _array.AsCoordinateSpan2D(Width, Height);
        for (int row = 0; row < span2d.Height; row++)
        {
            foreach (ref readonly var coord in span2d.GetRow(row))
            {
                sum += coord.X + coord.Y;
            }
        }
        return sum;
    }

    [Benchmark]
    public long TraitNativeSpan_Sum2D()
    {
        long sum = 0;
        ReadOnlySpan<CoordinateLayout> flat = _array.AsCoordinateNativeSpan();
        for (int row = 0; row < Height; row++)
        {
            for (int col = 0; col < Width; col++)
            {
                ref readonly var coord = ref flat[row * Width + col];
                sum += coord.X + coord.Y;
            }
        }
        return sum;
    }
}
