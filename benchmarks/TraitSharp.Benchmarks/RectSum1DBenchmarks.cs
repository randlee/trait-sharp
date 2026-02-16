using BenchmarkDotNet.Attributes;
using TraitSharp.Runtime;

namespace TraitSharp.Benchmarks;

/// <summary>
/// 1D sum benchmarks for BenchmarkRect[480000] — strided access (layout smaller than source).
/// Primary comparisons use foreach (ref) patterns — the idiomatic high-perf path.
/// Indexer variants included as secondary reference.
/// </summary>
[Config(typeof(FastBenchmarkConfig))]
public class RectSum1DBenchmarks : RectArraySetupBase
{
    // ══════════════════════════════════════════════════════════════
    //  Coordinate view: sum X + Y (2 fields at offset 0)
    // ══════════════════════════════════════════════════════════════

    [Benchmark(Baseline = true)]
    public long NativeSpan_Foreach_CoordSum1D()
    {
        long sum = 0;
        foreach (ref var r in _array.AsSpan())
        {
            sum += r.X + r.Y;
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan_Foreach_CoordSum1D()
    {
        long sum = 0;
        foreach (ref readonly var coord in _array.AsCoordinateSpan())
        {
            sum += coord.X + coord.Y;
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan_Indexer_CoordSum1D()
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

    // ══════════════════════════════════════════════════════════════
    //  Size view: sum Width + Height (2 fields at offset 8)
    // ══════════════════════════════════════════════════════════════

    [Benchmark]
    public long NativeSpan_Foreach_SizeSum1D()
    {
        long sum = 0;
        foreach (ref var r in _array.AsSpan())
        {
            sum += r.Width + r.Height;
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan_Foreach_SizeSum1D()
    {
        long sum = 0;
        foreach (ref readonly var size in _array.AsSizeSpan())
        {
            sum += size.Width + size.Height;
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan_Indexer_SizeSum1D()
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

    // ══════════════════════════════════════════════════════════════
    //  Both views: sum all 4 fields via two trait spans
    // ══════════════════════════════════════════════════════════════

    [Benchmark]
    public long NativeSpan_Foreach_AllFieldsSum1D()
    {
        long sum = 0;
        foreach (ref var r in _array.AsSpan())
        {
            sum += r.X + r.Y + r.Width + r.Height;
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan_ZipForeach_AllFieldsSum1D()
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

    [Benchmark]
    public long TraitSpan_DualIndexer_AllFieldsSum1D()
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
