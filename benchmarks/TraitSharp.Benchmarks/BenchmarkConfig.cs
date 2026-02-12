using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Reports;

namespace TraitSharp.Benchmarks;

/// <summary>
/// Shared benchmark configuration: fast runs with memory diagnostics.
/// Use ShortRunJob for quick iteration; switch to MediumRunJob for publishable results.
/// </summary>
public class FastBenchmarkConfig : ManualConfig
{
    public FastBenchmarkConfig()
    {
        AddJob(Job.ShortRun
            .WithWarmupCount(3)
            .WithIterationCount(5)
            .WithInvocationCount(1)
            .WithUnrollFactor(1));

        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumn(StatisticColumn.Mean);
        AddColumn(StatisticColumn.StdDev);
        WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(BenchmarkDotNet.Columns.RatioStyle.Trend));
    }
}
