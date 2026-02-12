using BenchmarkDotNet.Attributes;
using TraitSharp.Runtime;

namespace TraitSharp.Benchmarks;

/// <summary>
/// 2D array sum benchmarks for composite rect trait: sum all fields over BenchmarkRect[800*600].
/// Compares native flat-array indexing vs TraitSpan2D element access vs row iteration.
/// All allocations in GlobalSetup -- inner loops are identical.
/// </summary>
[Config(typeof(FastBenchmarkConfig))]
public class RectSum2DBenchmarks : RectArraySetupBase
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
                ref var r = ref arr[row * Width + col];
                sum += r.X + r.Y + r.Width + r.Height;
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
}
