using BenchmarkDotNet.Attributes;
using TraitSharp.Runtime;

namespace TraitSharp.Benchmarks;

/// <summary>
/// 2D array sum benchmarks: sum X + Y over BenchmarkPoint[800*600] with 2D access patterns.
/// Primary comparisons use foreach (ref) and row iteration — the idiomatic high-perf patterns.
/// Indexer variants included as secondary reference.
/// All allocations in GlobalSetup — inner loops are zero-alloc.
/// </summary>
[Config(typeof(FastBenchmarkConfig))]
public class Sum2DBenchmarks : ArraySetupBase
{
    // ── Primary: row-slice foreach — idiomatic 2D high-perf pattern ──

    [Benchmark(Baseline = true)]
    public long NativeSpan_RowSlice_Sum2D()
    {
        long sum = 0;
        Span<BenchmarkPoint> flat = _array.AsSpan();
        for (int row = 0; row < Height; row++)
        {
            foreach (ref var pt in flat.Slice(row * Width, Width))
            {
                sum += pt.X + pt.Y;
            }
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan2D_RowForeach_Sum2D()
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

    // ── Secondary: flat foreach — simpler but loses 2D structure ──

    [Benchmark]
    public long NativeSpan_Foreach_Sum2D()
    {
        long sum = 0;
        foreach (ref var pt in _array.AsSpan())
        {
            sum += pt.X + pt.Y;
        }
        return sum;
    }

    // ── Secondary: indexer access — for reference only ──

    [Benchmark]
    public long NativeSpan_Indexer_Sum2D()
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
    public long TraitSpan2D_Indexer_Sum2D()
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

    // ── NativeSpan layout (zero-stride) — best-case baseline ──

    [Benchmark]
    public long NativeLayoutSpan_RowSlice_Sum2D()
    {
        long sum = 0;
        ReadOnlySpan<CoordinateLayout> flat = _array.AsCoordinateNativeSpan();
        for (int row = 0; row < Height; row++)
        {
            foreach (ref readonly var coord in flat.Slice(row * Width, Width))
            {
                sum += coord.X + coord.Y;
            }
        }
        return sum;
    }
}
