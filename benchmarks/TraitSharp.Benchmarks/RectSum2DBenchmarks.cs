using BenchmarkDotNet.Attributes;
using TraitSharp.Runtime;

namespace TraitSharp.Benchmarks;

/// <summary>
/// 2D sum benchmarks for BenchmarkRect[800*600] — strided access (layout smaller than source).
/// Primary comparisons use foreach (ref) and row iteration — the idiomatic high-perf patterns.
/// Indexer variants included as secondary reference.
/// </summary>
[Config(typeof(FastBenchmarkConfig))]
public class RectSum2DBenchmarks : RectArraySetupBase
{
    // ══════════════════════════════════════════════════════════════
    //  Coordinate view: sum X + Y (2 fields at offset 0)
    // ══════════════════════════════════════════════════════════════

    [Benchmark(Baseline = true)]
    public long NativeSpan_RowSlice_CoordSum2D()
    {
        long sum = 0;
        Span<BenchmarkRect> flat = _array.AsSpan();
        for (int row = 0; row < Height; row++)
        {
            foreach (ref var r in flat.Slice(row * Width, Width))
            {
                sum += r.X + r.Y;
            }
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan2D_RowForeach_CoordSum2D()
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
    public long TraitSpan2D_Indexer_CoordSum2D()
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

    // ══════════════════════════════════════════════════════════════
    //  Both views: sum all 4 fields — dual trait access patterns
    // ══════════════════════════════════════════════════════════════

    [Benchmark]
    public long NativeSpan_RowSlice_AllFieldsSum2D()
    {
        long sum = 0;
        Span<BenchmarkRect> flat = _array.AsSpan();
        for (int row = 0; row < Height; row++)
        {
            foreach (ref var r in flat.Slice(row * Width, Width))
            {
                sum += r.X + r.Y + r.Width + r.Height;
            }
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan2D_RowForeach_AllFieldsSum2D()
    {
        long sum = 0;
        var coordSpan = _array.AsCoordinateSpan2D(Width, Height);
        var sizeSpan = _array.AsSizeSpan2D(Width, Height);
        for (int row = 0; row < Height; row++)
        {
            // Walk both row spans with foreach — pointer-increment cursors
            var coordRow = coordSpan.GetRow(row);
            var sizeRow = sizeSpan.GetRow(row);
            foreach (var pair in coordRow.Zip(sizeRow))
            {
                sum += pair.First.X + pair.First.Y + pair.Second.Width + pair.Second.Height;
            }
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan2D_DualIndexer_AllFieldsSum2D()
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
