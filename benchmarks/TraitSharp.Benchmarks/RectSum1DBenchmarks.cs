using BenchmarkDotNet.Attributes;
using TraitSharp.Runtime;

namespace TraitSharp.Benchmarks;

/// <summary>
/// 1D sum benchmarks for BenchmarkRect[480000] — strided access (layout smaller than source).
/// Each benchmark pair does identical work: NativeSpan baseline sums the same fields as the TraitSpan variant.
/// </summary>
[Config(typeof(FastBenchmarkConfig))]
public class RectSum1DBenchmarks : RectArraySetupBase
{
    // ── Coordinate view: sum X + Y (2 fields at offset 0) ──

    [Benchmark(Baseline = true)]
    public long NativeSpan_CoordSum1D()
    {
        long sum = 0;
        Span<BenchmarkRect> span = _array.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            sum += span[i].X + span[i].Y;
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

    // ── Size view: sum Width + Height (2 fields at offset 8) ──

    [Benchmark]
    public long NativeSpan_SizeSum1D()
    {
        long sum = 0;
        Span<BenchmarkRect> span = _array.AsSpan();
        for (int i = 0; i < span.Length; i++)
        {
            sum += span[i].Width + span[i].Height;
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

    // ── Both views: sum all 4 fields via two trait spans ──

    [Benchmark]
    public long NativeSpan_AllFieldsSum1D()
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

    [Benchmark]
    public long TraitSpan_ZipForeach1D()
    {
        long sum = 0;
        var coordSpan = _array.AsCoordinateSpan();
        var sizeSpan = _array.AsSizeSpan();
        foreach (var pair in coordSpan.Zip(sizeSpan))
        {
            sum += pair.First.X + pair.First.Y + pair.Second.Width + pair.Second.Height;
        }
        return sum;
    }
}
