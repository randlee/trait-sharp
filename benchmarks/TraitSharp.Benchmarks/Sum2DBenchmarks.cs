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
    [Benchmark(Baseline = true)]
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
            var rowSpan = span2d.GetRow(row);
            for (int i = 0; i < rowSpan.Length; i++)
            {
                ref readonly var coord = ref rowSpan[i];
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
