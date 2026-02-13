using BenchmarkDotNet.Attributes;
using TraitSharp.Runtime;

namespace TraitSharp.Benchmarks;

/// <summary>
/// 1D array sum benchmarks for composite rect trait: sum X + Y + Width + Height over BenchmarkRect[480000].
/// Compares native array access vs Span vs TraitSpan (Coordinate view) vs TraitSpan (Size view).
/// All allocations in GlobalSetup -- inner loops are identical.
/// </summary>
[Config(typeof(FastBenchmarkConfig))]
public class RectSum1DBenchmarks : RectArraySetupBase
{
    [Benchmark(Baseline = true)]
    public long NativeArray_Sum1D()
    {
        long sum = 0;
        var arr = _array;
        for (int i = 0; i < arr.Length; i++)
        {
            sum += arr[i].X + arr[i].Y + arr[i].Width + arr[i].Height;
        }
        return sum;
    }

    [Benchmark]
    public long NativeSpan_Sum1D()
    {
        long sum = 0;
        Span<BenchmarkRect> span = _array.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            sum += span[i].X + span[i].Y + span[i].Width + span[i].Height;
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan_CoordSum1D()
    {
        long sum = 0;
        var span = _array.AsCoordinateSpan();
        for (int i = 0; i < span.Length; i++)
        {
            ref readonly var coord = ref span[i];
            sum += coord.X + coord.Y;
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan_SizeSum1D()
    {
        long sum = 0;
        var span = _array.AsSizeSpan();
        for (int i = 0; i < span.Length; i++)
        {
            ref readonly var size = ref span[i];
            sum += size.Width + size.Height;
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan_BothSum1D()
    {
        long sum = 0;
        var coordSpan = _array.AsCoordinateSpan();
        var sizeSpan = _array.AsSizeSpan();
        for (int i = 0; i < coordSpan.Length; i++)
        {
            ref readonly var coord = ref coordSpan[i];
            ref readonly var size = ref sizeSpan[i];
            sum += coord.X + coord.Y + size.Width + size.Height;
        }
        return sum;
    }
}
