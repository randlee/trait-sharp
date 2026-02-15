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

Benchmarked on Apple M4 Max, .NET 8.0.20, Arm64 RyuJIT AdvSIMD. All tests use 1M elements (1000×1000 for 2D).

### 1D Span — Same-Size Layout

| Method | Mean | vs Baseline | Alloc |
|--------|------|-------------|-------|
| NativeSpan (baseline) | 167.0 μs | — | — |
| TraitSpan foreach | 167.2 μs | **1.00x (parity)** | — |
| TraitSpan indexer | 180.2 μs | 1.08x slower | — |
| AsNativeSpan escape | 174.0 μs | 1.04x slower | — |

### 2D Span — Same-Size Layout

| Method | Mean | vs Baseline | Alloc |
|--------|------|-------------|-------|
| NativeSpan (baseline) | 178.3 μs | — | — |
| TraitSpan2D indexer | 232.6 μs | 1.30x slower | — |
| GetRow + foreach | 163.7 μs | **1.09x faster** | — |
| AsNativeSpan escape | 178.3 μs | **1.00x (parity)** | — |

### 1D Span — Strided Layout (larger source struct)

| Method | Mean | vs Baseline | Alloc |
|--------|------|-------------|-------|
| NativeSpan Coord (baseline) | 175.0 μs | — | — |
| TraitSpan Coord | 180.5 μs | 1.03x slower | — |
| TraitSpan Both (dual spans) | 280.2 μs | 1.60x slower | — |

### 2D Span — Strided Layout (larger source struct)

| Method | Mean | vs Baseline | Alloc |
|--------|------|-------------|-------|
| NativeSpan Coord (baseline) | 186.8 μs | — | — |
| TraitSpan2D Coord | 228.5 μs | 1.22x slower | — |
| GetRow + foreach | 189.1 μs | **1.01x (parity)** | — |
| TraitSpan2D Both (dual spans) | 363.3 μs | 1.95x slower | — |

### Key Takeaways

- **`foreach` / `GetRow` iteration achieves parity or better** — recommended default pattern
- **`AsNativeSpan()` achieves exact parity** — use when layout sizes match
- **2D indexer has ~22-30% overhead** — use row decomposition (`GetRow`) when iterating
- **Dual-span iteration has 60-95% overhead** — fundamental cache-line effect from interleaved views
- **Zero heap allocations** across all span access patterns

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
