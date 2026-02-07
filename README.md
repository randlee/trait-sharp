# TraitEmulation

Rust-like trait semantics for C#. Zero-cost polymorphism over value types.

## Features

- **Zero Boxing** - Value types never allocated on heap
- **Zero Copy** - Direct field access via layout casts
- **Type Safe** - Compile-time layout verification
- **External Types** - Works with System types
- **Source Generated** - No runtime reflection

## Quick Start

```csharp
using TraitEmulation;
using System.Runtime.InteropServices;

// Define a trait
[Trait]
interface IPoint2D {
    float X { get; }
    float Y { get; }
}

// Implement on your types
[ImplementsTrait(typeof(IPoint2D))]
[StructLayout(LayoutKind.Sequential)]
partial struct Point3D {
    public float X, Y, Z;
}

// Register system types
[assembly: RegisterTraitImpl(typeof(IPoint2D), typeof(System.Drawing.PointF))]

// Write generic algorithms
public static float Distance<T1, T2>(in T1 p1, in T2 p2)
    where T1 : unmanaged, ITrait<IPoint2D, T1>
    where T2 : unmanaged, ITrait<IPoint2D, T2>
{
    ref readonly var c1 = ref p1.AsPoint2D();
    ref readonly var c2 = ref p2.AsPoint2D();

    float dx = c1.X - c2.X;
    float dy = c1.Y - c2.Y;
    return MathF.Sqrt(dx * dx + dy * dy);
}
```

## How It Works

TraitEmulation uses a Roslyn source generator to bring Rust-like trait semantics to C#:

1. **Trait Definition** - Mark an interface with `[Trait]` to define a contract
2. **Layout Struct Generation** - The generator creates a `[StructLayout(Sequential)]` struct matching the trait's field layout
3. **Compile-Time Verification** - The generator verifies that implementing types have layout-compatible fields
4. **Zero-Copy Access** - Uses `Unsafe.As` reinterpret casts for direct field access without copying or boxing
5. **TraitSpan Types** - Strided span types (`TraitSpan<T>`, `ReadOnlyTraitSpan<T>`) enable iteration over trait views of struct arrays

## Project Structure

| Package | Description |
|---------|-------------|
| `TraitEmulation.Attributes` | Attribute classes (`[Trait]`, `[ImplementsTrait]`, `[RegisterTraitImpl]`) |
| `TraitEmulation.SourceGenerator` | Roslyn source generator (analyzer DLL) |
| `TraitEmulation.Runtime` | `TraitSpan<T>`, `ReadOnlyTraitSpan<T>`, 2D variants, `ThrowHelper` |

## Performance

The trait approach achieves the same performance as direct field access while being approximately 9x faster than interface-based polymorphism with zero heap allocations.

| Method | Ratio | Allocated |
|--------|-------|-----------|
| Direct field access | 1.00 | - |
| Trait layout cast | 1.02 | - |
| Trait extension methods | 1.00 | - |
| Interface boxing | 8.81 | 160 KB |

## Documentation

See [Design Document](docs/trait-emulation-design.md) for the complete specification.

## License

See [LICENSE](LICENSE) for details.
