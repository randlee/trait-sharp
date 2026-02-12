using BenchmarkDotNet.Attributes;

namespace TraitSharp.Benchmarks;

/// <summary>
/// Base class providing pre-allocated BenchmarkPoint arrays for benchmarks.
/// All allocations happen in GlobalSetup, never inside benchmark methods.
/// </summary>
public abstract class ArraySetupBase
{
    protected const int Width = 800;
    protected const int Height = 600;
    protected const int ArrayLength = Width * Height; // 480,000 elements

    protected BenchmarkPoint[] _array = null!;

    [GlobalSetup]
    public virtual void Setup()
    {
        _array = new BenchmarkPoint[ArrayLength];
        for (int i = 0; i < ArrayLength; i++)
        {
            _array[i] = new BenchmarkPoint(i % Width, i / Width);
        }
    }
}
