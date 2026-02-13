using BenchmarkDotNet.Attributes;
using TraitSharp.Runtime;

namespace TraitSharp.Benchmarks;

/// <summary>
/// 1D array sum benchmarks: sum X + Y over BenchmarkPoint[480000].
/// Compares native array access vs Span vs TraitSpan.
/// All allocations in GlobalSetup — inner loops are identical.
/// </summary>
[Config(typeof(FastBenchmarkConfig))]
public class Sum1DBenchmarks : ArraySetupBase
{
    [Benchmark]
    public long NativeArray_Sum1D()
    {
        long sum = 0;
        var arr = _array;
        for (int i = 0; i < arr.Length; i++)
        {
            sum += arr[i].X + arr[i].Y;
        }
        return sum;
    }

    [Benchmark(Baseline = true)]
    public long NativeSpan_Sum1D()
    {
        long sum = 0;
        Span<BenchmarkPoint> span = _array.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            sum += span[i].X + span[i].Y;
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan_Sum1D()
    {
        long sum = 0;
        foreach (ref readonly var coord in _array.AsCoordinateSpan())
        {
            sum += coord.X + coord.Y;
        }
        return sum;
    }

    [Benchmark]
    public long TraitNativeSpan_Sum1D()
    {
        long sum = 0;
        ReadOnlySpan<CoordinateLayout> span = _array.AsCoordinateNativeSpan();
        for (int i = 0; i < span.Length; i++)
        {
            sum += span[i].X + span[i].Y;
        }
        return sum;
    }
}
