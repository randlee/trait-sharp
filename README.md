# TraitSharp

[![CI](https://github.com/randlee/trait-sharp/actions/workflows/ci.yml/badge.svg)](https://github.com/randlee/trait-sharp/actions/workflows/ci.yml)
[![NuGet](https://img.shields.io/nuget/v/TraitSharp.svg)](https://www.nuget.org/packages/TraitSharp)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

**Rust-like trait semantics for C#.** Zero-cost polymorphism over value types via source generation.

TraitSharp solves the struct-interface boxing problem by generating layout-compatible structs and
providing zero-copy reinterpret casts, extension methods, and strided span types — all verified
at compile time with no runtime reflection.

## Features

- **Zero Boxing** — Value types never allocated on the heap
- **Zero Copy** — Direct field access via `Unsafe.As` layout casts (no marshalling)
- **Type Safe** — Compile-time layout verification by the source generator
- **External Types** — Register trait implementations for types you don't own (e.g., `System.Drawing.PointF`)
- **Method Traits** — Trait interfaces can define methods dispatched via static `_Impl` pattern
- **Default Methods** — Trait methods with bodies auto-emit as defaults; implementers can selectively override
- **Trait Inheritance** — Derived traits inherit fields and methods from base traits
- **TraitSpan** — Strided span types for iterating trait views over arrays without copying
- **Source Generated** — All code generated at compile time by a Roslyn analyzer; zero runtime cost

## Quick Start

```csharp
using TraitSharp;
using System.Runtime.InteropServices;

// 1. Define a trait
[Trait(GenerateLayout = true)]
public partial interface ICoordinate
{
    int X { get; }
    int Y { get; }
}

// 2. Implement on your types
[ImplementsTrait(typeof(ICoordinate))]
[StructLayout(LayoutKind.Sequential)]
public partial struct DataPoint
{
    public int X, Y;
    public byte R, G, B, A;
}

[ImplementsTrait(typeof(ICoordinate))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Point3D
{
    public int X, Y;
    public float Z;
}

// 3. Write generic algorithms — zero boxing, zero allocation
public static float Distance<T1, T2>(ref T1 p1, ref T2 p2)
    where T1 : unmanaged, ICoordinateTrait<T1>
    where T2 : unmanaged, ICoordinateTrait<T2>
{
    ref readonly var c1 = ref p1.AsCoordinate();
    ref readonly var c2 = ref p2.AsCoordinate();

    int dx = c1.X - c2.X;
    int dy = c1.Y - c2.Y;
    return MathF.Sqrt(dx * dx + dy * dy);
}

// 4. Use it — works across different struct types
var dp = new DataPoint { X = 10, Y = 20, R = 255, G = 128, B = 64, A = 255 };
var p3 = new Point3D { X = 30, Y = 40, Z = 1.5f };
float dist = Distance(ref dp, ref p3); // 28.28 — no boxing, no copies
```

## Method Traits

Traits can define methods alongside properties. Implementers provide the body via a static `_Impl` method:

```csharp
[Trait(GenerateLayout = true)]
public partial interface ILabeled
{
    int Tag { get; }
    string Describe();  // method trait — no default body
}

[ImplementsTrait(typeof(ILabeled))]
[StructLayout(LayoutKind.Sequential)]
public partial struct LabeledItem
{
    public int Tag;
    public float Value;

    // The source generator wires this up as the implementation of Describe()
    public static string Describe_Impl(in LabeledItem self)
        => $"Item(Tag={self.Tag}, Value={self.Value:F1})";
}

// Call via extension method — dispatched at compile time
var item = new LabeledItem { Tag = 42, Value = 3.14f };
string desc = item.Describe(); // "Item(Tag=42, Value=3.1)"
```

## Default Methods

Trait methods with bodies become defaults — implementers only override what they need:

```csharp
[Trait(GenerateLayout = true)]
public partial interface IShape
{
    int Tag { get; }
    string Describe() { return $"Shape(Tag={Tag})"; }  // default
    float Area();                                        // required
    float Perimeter() { return 0f; }                     // default
}

[ImplementsTrait(typeof(IShape))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Rectangle
{
    public int Tag;
    public float Width, Height;

    public static float Area_Impl(in Rectangle self)       // required
        => self.Width * self.Height;
    public static float Perimeter_Impl(in Rectangle self)  // overrides default
        => 2f * (self.Width + self.Height);
    // Describe — uses the default from IShape
}
```

## Trait Inheritance

Derived traits inherit all fields and methods from base traits:

```csharp
[Trait(GenerateLayout = true)]
public partial interface IAnimal
{
    int Legs { get; }
    string Sound();
}

[Trait(GenerateLayout = true)]
public partial interface IPet : IAnimal
{
    int Affection { get; }
    string Introduce() => $"I have {Legs} legs, go '{Sound()}', affection={Affection}";
}

[ImplementsTrait(typeof(IPet))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Dog
{
    public int Legs;
    public int Affection;
    public static string Sound_Impl(in Dog self) => "woof";
    // Introduce — uses default from IPet, which calls inherited Sound()
}

// Generic algorithm works with base trait constraint
public static string AnimalInfo<T>(ref T animal) where T : unmanaged, IAnimalTrait<T>
    => $"{animal.Sound()} ({animal.GetLegs()} legs)";

// Or derived trait constraint — can call both inherited and own methods
public static string PetProfile<T>(ref T pet) where T : unmanaged, IPetTrait<T>
    => $"Sound: {pet.Sound()} | Intro: {pet.Introduce()}";
```

## TraitSpan

Iterate over trait views of struct arrays without copying. TraitSpan uses strided access to
read/write only the trait-relevant fields:

```csharp
var points = new DataPoint[]
{
    new() { X = 0, Y = 0, R = 255, G = 0, B = 0, A = 255 },
    new() { X = 10, Y = 20, R = 0, G = 255, B = 0, A = 255 },
    new() { X = 30, Y = 40, R = 0, G = 0, B = 255, A = 255 },
};

// Read-only iteration — zero-copy views into the original array
var coordSpan = points.AsSpan().AsCoordinateSpan();
foreach (ref readonly var c in coordSpan)
    Console.WriteLine($"({c.X}, {c.Y})");

// Mutable iteration — modify coordinates in-place
var mutable = points.AsSpan().AsCoordinateTraitSpan();
foreach (ref var c in mutable)
{
    c.X += 100;
    c.Y += 200;
}
// Original array is modified — no copies were made
```

## Field Mapping

Trait fields match by name and type. The struct's fields must appear in the same
order as the trait, but the struct can have additional fields after the trait fields:

```csharp
[Trait(GenerateLayout = true)]
public partial interface ICoordinate { int X { get; } int Y { get; } }

// ✅ Prefix match — X, Y first, then extra fields
[ImplementsTrait(typeof(ICoordinate))]
[StructLayout(LayoutKind.Sequential)]
public partial struct DataPoint
{
    public int X, Y;         // matches ICoordinate
    public byte R, G, B, A;  // extra fields — ignored by the trait
}
```

For types you don't own, register implementations at the assembly level:

```csharp
[assembly: RegisterTraitImpl(typeof(ICoordinate), typeof(System.Drawing.Point))]
```

## How It Works

TraitSharp uses a Roslyn source generator to bring Rust-like trait semantics to C#:

1. **Trait Definition** — Mark an interface with `[Trait]` to define a field/method contract
2. **Layout Struct** — The generator creates a `[StructLayout(Sequential)]` struct matching the trait's field layout
3. **Compile-Time Verification** — The generator verifies implementing types have layout-compatible fields at the correct offsets
4. **Zero-Copy Access** — Uses `Unsafe.As<TStruct, TLayout>` reinterpret casts for direct field access
5. **Method Dispatch** — Extension methods route to static `_Impl` methods or default bodies
6. **TraitSpan Types** — Strided span types (`TraitSpan<T>`, `ReadOnlyTraitSpan<T>`, 2D variants) enable iteration over trait views

## Performance

All benchmarks run on **Apple M4 Max / .NET 8.0 / Arm64 RyuJIT AdvSIMD** with
**480,000 elements** (800 x 600). Full source in [`benchmarks/`](benchmarks/).

### 1D Iteration — `BenchmarkPoint` (8 bytes: X, Y as int)

Trait view of the same struct type. TraitSpan accesses only the trait fields via strided pointer arithmetic.

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| NativeArray_Sum1D | 171.9 us | 1.05x slower | 0 B |
| NativeSpan_Sum1D | 163.4 us | baseline | 0 B |
| **TraitSpan_Sum1D** | **143.8 us** | **1.14x faster** | **0 B** |
| TraitNativeSpan_Sum1D | 163.3 us | 1.00x | 0 B |

> TraitSpan is _faster_ than native `Span<T>` because it strides over only the trait fields, improving cache utilization when the struct has extra data.

### 2D Iteration — `BenchmarkPoint` (800 x 600 grid)

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| NativeArray_Sum2D | 193.1 us | 1.11x slower | 0 B |
| NativeSpan_Sum2D | 173.5 us | baseline | 0 B |
| TraitSpan2D_Sum2D | 237.8 us | 1.37x slower | 0 B |
| **TraitSpan2D_RowSum** | **147.3 us** | **1.18x faster** | **0 B** |
| TraitNativeSpan_Sum2D | 173.9 us | 1.00x | 0 B |

> Row-based iteration (`GetRow()`) matches or beats native span by leveraging contiguous row access.

### 1D Multi-Trait — `BenchmarkRect` (16 bytes: X, Y, Width, Height)

Accessing _different trait views_ of the same struct. Each TraitSpan reads only its trait's fields.

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| NativeArray_Sum1D | 245.2 us | baseline | 0 B |
| NativeSpan_Sum1D | 197.8 us | 1.24x faster | 0 B |
| **TraitSpan_CoordSum1D** | **172.5 us** | **1.42x faster** | **0 B** |
| **TraitSpan_SizeSum1D** | **171.0 us** | **1.43x faster** | **0 B** |
| TraitSpan_BothSum1D | 244.0 us | 1.00x | 0 B |

> Individual trait views (Coord-only or Size-only) are faster than reading the full struct because they access fewer bytes per element.

### 2D Multi-Trait — `BenchmarkRect` (800 x 600 grid)

| Method | Mean | Ratio | Allocated |
|---|---:|---:|---:|
| NativeArray_Sum2D | 246.5 us | baseline | 0 B |
| TraitSpan2D_CoordSum2D | 235.3 us | 1.05x faster | 0 B |
| **TraitSpan2D_CoordRowSum** | **175.9 us** | **1.40x faster** | **0 B** |

> Row iteration with multi-trait views gives 40% speedup over native array access.

### Key Takeaways

- **Zero allocations** across all access patterns — no boxing, no copying
- **TraitSpan 1D** often _faster_ than native span when the struct has extra fields
- **Row-based 2D** iteration matches or exceeds native span performance
- **Multi-trait views** let you access only the fields you need, improving cache efficiency

## Packages

| Package | Description |
|---------|-------------|
| [`TraitSharp`](https://www.nuget.org/packages/TraitSharp) | Meta-package: references Attributes + SourceGenerator |
| [`TraitSharp.Attributes`](https://www.nuget.org/packages/TraitSharp.Attributes) | `[Trait]`, `[ImplementsTrait]`, `[RegisterTraitImpl]` |
| [`TraitSharp.SourceGenerator`](https://www.nuget.org/packages/TraitSharp.SourceGenerator) | Roslyn source generator (analyzer DLL) |
| [`TraitSharp.Runtime`](https://www.nuget.org/packages/TraitSharp.Runtime) | `TraitSpan<T>`, `ReadOnlyTraitSpan<T>`, 2D variants, `ThrowHelper` |

## Requirements

- .NET 8.0+
- C# 12+
- `[StructLayout(LayoutKind.Sequential)]` on implementing structs

## Limitations

- Trait fields must appear as a contiguous prefix in the implementing struct
- Only `unmanaged` types are supported (no reference-type fields in trait structs)
- Field matching is by name and type; reordering trait fields is a breaking change
- Method traits require the `static TReturn MethodName_Impl(in TSelf self, ...)` pattern

## Documentation

See [Design Document](docs/trait-emulation-design.md) for the complete specification.

## License

MIT — see [LICENSE](LICENSE) for details.
