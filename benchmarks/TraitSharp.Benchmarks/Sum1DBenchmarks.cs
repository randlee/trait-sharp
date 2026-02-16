using BenchmarkDotNet.Attributes;
using TraitSharp.Runtime;

namespace TraitSharp.Benchmarks;

/// <summary>
/// 1D array sum benchmarks: sum X + Y over BenchmarkPoint[480000].
/// Compares native Span foreach vs TraitSpan foreach (pointer-increment enumerators).
/// Indexer variants included as secondary reference.
/// All allocations in GlobalSetup — inner loops are zero-alloc.
/// </summary>
[Config(typeof(FastBenchmarkConfig))]
public class Sum1DBenchmarks : ArraySetupBase
{
    // ── Primary: foreach (ref) — the idiomatic high-perf pattern ──

    [Benchmark(Baseline = true)]
    public long NativeSpan_Foreach_Sum1D()
    {
        long sum = 0;
        foreach (ref var pt in _array.AsSpan())
        {
            sum += pt.X + pt.Y;
        }
        return sum;
    }

    [Benchmark]
    public long TraitSpan_Foreach_Sum1D()
    {
        long sum = 0;
        foreach (ref readonly var coord in _array.AsCoordinateSpan())
        {
            sum += coord.X + coord.Y;
        }
        return sum;
    }

    // ── Secondary: indexer access — for reference only ──

    [Benchmark]
    public long NativeSpan_Indexer_Sum1D()
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
    public long TraitSpan_Indexer_Sum1D()
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

    // ── NativeSpan layout (zero-stride) — best-case baseline ──

    [Benchmark]
    public long NativeLayoutSpan_Foreach_Sum1D()
    {
        long sum = 0;
        foreach (ref readonly var coord in _array.AsCoordinateNativeSpan())
        {
            sum += coord.X + coord.Y;
        }
        return sum;
    }
}
