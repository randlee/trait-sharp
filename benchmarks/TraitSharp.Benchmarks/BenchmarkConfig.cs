using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Reports;
using BenchmarkDotNet.Running;

namespace TraitSharp.Benchmarks;

/// <summary>
/// Shared benchmark configuration: fast runs with memory diagnostics.
/// Use ShortRunJob for quick iteration; switch to MediumRunJob for publishable results.
/// </summary>
public class FastBenchmarkConfig : ManualConfig
{
    public FastBenchmarkConfig()
    {
        AddJob(Job.MediumRun
            .WithWarmupCount(5)
            .WithIterationCount(15));

        AddDiagnoser(MemoryDiagnoser.Default);
        AddColumn(StatisticColumn.Mean);
        AddColumn(StatisticColumn.StdDev);
        AddColumn(new ThroughputColumn());
        WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(BenchmarkDotNet.Columns.RatioStyle.Trend));
    }
}

/// <summary>
/// Custom BenchmarkDotNet column that computes data throughput in GB/s.
/// Calculates throughput as: totalBytes / meanNanoseconds * 1e9 / (1024^3).
/// The total bytes processed is determined by the benchmark class name.
/// </summary>
public class ThroughputColumn : IColumn
{
    /// <inheritdoc />
    public string Id => "Throughput";

    /// <inheritdoc />
    public string ColumnName => "GB/s";

    /// <inheritdoc />
    public bool AlwaysShow => true;

    /// <inheritdoc />
    public ColumnCategory Category => ColumnCategory.Custom;

    /// <inheritdoc />
    public int PriorityInCategory => 0;

    /// <inheritdoc />
    public bool IsNumeric => true;

    /// <inheritdoc />
    public UnitType UnitType => UnitType.Dimensionless;

    /// <inheritdoc />
    public string Legend => "Data throughput in GB/s";

    /// <inheritdoc />
    public bool IsDefault(Summary summary, BenchmarkCase benchmarkCase) => false;

    /// <inheritdoc />
    public bool IsAvailable(Summary summary) => true;

    /// <inheritdoc />
    public string GetValue(Summary summary, BenchmarkCase benchmarkCase)
    {
        return GetValue(summary, benchmarkCase, SummaryStyle.Default);
    }

    /// <inheritdoc />
    public string GetValue(Summary summary, BenchmarkCase benchmarkCase, SummaryStyle style)
    {
        var report = summary[benchmarkCase];
        if (report?.ResultStatistics == null) return "N/A";

        var meanNs = report.ResultStatistics.Mean;
        var totalBytes = GetTotalBytes(benchmarkCase);
        if (totalBytes == 0 || meanNs == 0) return "N/A";

        double gbPerSec = (totalBytes / meanNs) * 1_000_000_000.0 / (1024.0 * 1024.0 * 1024.0);
        return gbPerSec.ToString("F2");
    }

    /// <summary>
    /// Returns the total bytes processed per benchmark invocation based on the benchmark class.
    /// Point benchmarks: 480,000 elements * sizeof(BenchmarkPoint=8) = 3,840,000 bytes.
    /// Rect benchmarks: 480,000 elements * sizeof(BenchmarkRect=16) = 7,680,000 bytes.
    /// </summary>
    private static long GetTotalBytes(BenchmarkCase benchmarkCase)
    {
        var typeName = benchmarkCase.Descriptor.Type.Name;
        return typeName switch
        {
            "Sum1DBenchmarks" => 480_000L * 8,
            "Sum2DBenchmarks" => 480_000L * 8,
            "RectSum1DBenchmarks" => 480_000L * 16,
            "RectSum2DBenchmarks" => 480_000L * 16,
            _ => 0
        };
    }

    /// <inheritdoc />
    public override string ToString() => ColumnName;
}
