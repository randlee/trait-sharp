# Trait Emulation for C# - Complete Design Specification

## Executive Summary

A source generator package that brings Rust-like trait semantics to C#, enabling zero-cost polymorphism over value types without boxing. The system provides direct field access through type-safe reinterpret casts while maintaining compile-time safety guarantees.

**Key Innovation:** Solves the C# struct-interface boxing problem by generating layout-compatible base structs and providing multiple access patterns (extension methods, static methods, and zero-copy casts).

---

## Problem Statement

### The Boxing Penalty

```csharp
interface IProcessor {
    void Process();
}

struct DataTile : IProcessor {
    public int X, Y;
    public byte[] Data;
    public void Process() { /* ... */ }
}

void ProcessBatch(IProcessor[] items) {
    foreach (var item in items) {
        item.Process();  // ❌ Every struct gets BOXED to heap
    }
}
```

**Impact on pipeline processing systems:**
- Heap allocations for thousands of value-type pixels/tiles
- GC pressure from temporary boxes
- Cache misses from pointer indirection
- 3-5x performance degradation in hot loops

### Existing Workarounds Are Insufficient

1. **Generic constraints** - Code bloat, lose runtime polymorphism
2. **Unsafe pointers** - Loss of type safety, platform-specific
3. **Manual wrappers** - Boilerplate explosion
4. **Class-based** - Defeats purpose of value types

### The System Type Problem

Cannot implement interfaces on types you don't own:

```csharp
// ❌ IMPOSSIBLE - System.Drawing.Point is sealed
struct Point : IPoint2D { }  // Can't extend System.Drawing.Point
```

Need a mechanism to "retrofit" trait implementations onto existing system types.

---

## Design Goals

1. **Zero Boxing** - Value types never allocated on heap for polymorphism
2. **Zero Copy** - Direct field access via layout-compatible casts
3. **Type Safety** - Compile-time verification of layout compatibility
4. **Ergonomic API** - Feels like native language feature
5. **External Type Support** - Works with System types and third-party libraries
6. **Performance Transparency** - Clear cost model (static dispatch vs dynamic)
7. **Incremental Adoption** - Works alongside existing C# patterns

---

## Core Concepts

### Trait Definition

A trait is an interface decorated with `[Trait]` that defines a contract:

```csharp
[Trait(GenerateLayout = true)]
interface IPixelCoordinate {
    int X { get; }
    int Y { get; }
}
```

The generator creates:
1. A layout struct with the exact field layout
2. Extension methods for ergonomic access
3. Static interface methods for explicit access
4. Constraint interfaces for generic bounds

### Layout Struct

The fundamental zero-copy mechanism:

```csharp
// Generated from IPixelCoordinate
[StructLayout(LayoutKind.Sequential)]
public struct PixelCoordinateLayout {
    public int X;
    public int Y;
}
```

Types implementing the trait can be reinterpreted as this layout **if their memory layout is compatible**.

### Trait Implementation

```csharp
[ImplementsTrait(typeof(IPixelCoordinate))]
[StructLayout(LayoutKind.Sequential)]
partial struct BayerPixel {
    public int X, Y;           // Must match layout
    public byte R, G, B, A;  // Additional fields OK
}
```

Generator verifies:
- Fields X, Y exist with correct types
- Field order matches trait definition
- StructLayout is Sequential or Explicit

### External Type Registration

```csharp
[assembly: RegisterTraitImpl(typeof(IPixelCoordinate), typeof(System.Drawing.Point))]
```

Generates adapter that bridges system types to trait contract.

---

## Generated API Surface

### 1. Constraint Interface

```csharp
public interface ITrait<IPixelCoordinate, TSelf> where TSelf : unmanaged {
    // Individual property accessors via static dispatch
    static abstract int GetX_Impl(in TSelf self);
    static abstract int GetY_Impl(in TSelf self);

    // Zero-copy layout cast via Unsafe.As reinterpret
    static abstract ref readonly PixelCoordinateLayout AsLayout(in TSelf self);
}
```

**Purpose:** Provides constraint for generic parameters while enabling static dispatch.

### 2. Extension Methods

```csharp
public static class IPixelCoordinateExtensions {
    /// <summary>
    /// Zero-copy cast to layout struct for direct field access.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly PixelCoordinateLayout AsPixelCoordinate<T>(this in T self)
        where T : unmanaged, ITrait<IPixelCoordinate, T>
    {
        return ref T.AsLayout(in self);
    }
    
    /// <summary>
    /// Get X coordinate via static dispatch.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetX<T>(this in T self)
        where T : unmanaged, ITrait<IPixelCoordinate, T>
    {
        return T.GetX_Impl(in self);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetY<T>(this in T self)
        where T : unmanaged, ITrait<IPixelCoordinate, T>
    {
        return T.GetY_Impl(in self);
    }
}
```

**Purpose:** Ergonomic access - `pixel.AsPixelCoordinate()` reads naturally.

### 3. Static Interface Methods

```csharp
public partial interface IPixelCoordinate {
    /// <summary>
    /// Static accessor for X coordinate across all implementers.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int GetX<T>(in T self) 
        where T : unmanaged, ITrait<IPixelCoordinate, T>
    {
        return T.GetX_Impl(in self);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int GetY<T>(in T self) 
        where T : unmanaged, ITrait<IPixelCoordinate, T>
    {
        return T.GetY_Impl(in self);
    }
    
    /// <summary>
    /// Zero-copy cast to layout struct.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static ref readonly PixelCoordinateLayout AsLayout<T>(in T self)
        where T : unmanaged, ITrait<IPixelCoordinate, T>
    {
        return ref T.AsLayout(in self);
    }
}
```

**Purpose:** Explicit, namespaced access pattern for clarity.

### 4. Implementation for User Types

```csharp
// Generated for BayerPixel
partial struct BayerPixel : ITrait<IPixelCoordinate, BayerPixel> {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetX_Impl(in BayerPixel self) => self.X;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int GetY_Impl(in BayerPixel self) => self.Y;
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly PixelCoordinateLayout AsLayout(in BayerPixel self) {
        // SAFETY: Generator verified BayerPixel starts with {int X; int Y;}
        return ref Unsafe.As<BayerPixel, PixelCoordinateLayout>(
            ref Unsafe.AsRef(in self));
    }
}
```

### 5. Adapter for External Types

```csharp
// Generated for System.Drawing.Point
file static class SystemDrawingPointTraitImpl {
    // Empty marker - implementation attached via extension on Point itself
}

// C# doesn't allow adding interfaces to external types, so we use DIM pattern
public static class IPixelCoordinateExternalImpls {
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ref readonly PixelCoordinateLayout AsPixelCoordinate(
        this in System.Drawing.Point self)
    {
        // SAFETY: System.Drawing.Point is {int X; int Y;} - exact match
        return ref Unsafe.As<System.Drawing.Point, PixelCoordinateLayout>(
            ref Unsafe.AsRef(in self));
    }
}
```

**Note:** External types can't implement `ITrait<,>` interface, but can use extension methods for the same ergonomics.

---

## Usage Patterns

### Pattern 1: Direct Field Access (Fastest)

```csharp
public static void ApplyFlatField<T>(Span<T> pixels, Span<float> correction, int width) 
    where T : unmanaged, ITrait<IPixelCoordinate, T>
{
    for (int i = 0; i < pixels.Length; i++) {
        ref var pixel = ref pixels[i];
        ref readonly var coord = ref pixel.AsPixelCoordinate();
        
        int offset = coord.Y * width + coord.X;  // Direct field access!
        // ... apply correction[offset]
    }
}
```

**Cost:** Zero - inlined Unsafe.As + field dereference.

### Pattern 2: Extension Method Access

```csharp
public static float Distance<T1, T2>(in T1 p1, in T2 p2)
    where T1 : unmanaged, ITrait<IPixelCoordinate, T1>
    where T2 : unmanaged, ITrait<IPixelCoordinate, T2>
{
    int dx = p1.GetX() - p2.GetX();
    int dy = p1.GetY() - p2.GetY();
    return MathF.Sqrt(dx * dx + dy * dy);
}
```

**Cost:** Zero - inlined accessors via static dispatch.

### Pattern 3: Mixed Types (System + User)

```csharp
BayerPixel bayerPixel = new BayerPixel { X = 10, Y = 20, R = 255 };
System.Drawing.Point sysPoint = new System.Drawing.Point(30, 40);

// Both work with extension methods
ref readonly var coord1 = ref bayerPixel.AsPixelCoordinate();
ref readonly var coord2 = ref sysPoint.AsPixelCoordinate();

Console.WriteLine($"Bayer: ({coord1.X}, {coord1.Y})");
Console.WriteLine($"System: ({coord2.X}, {coord2.Y})");
```

**Note:** System types can't use generic constraints, but extension methods work.

### Pattern 4: Offset Traits (Multiple Views)

```csharp
[Trait]
interface IPoint2D { int X { get; } int Y { get; } }

[Trait]
interface ISize2D { int Width { get; } int Height { get; } }

[ImplementsTrait(typeof(IPoint2D))]    // Auto-detected at offset 0
[ImplementsTrait(typeof(ISize2D))]     // Auto-detected at offset 8
[StructLayout(LayoutKind.Sequential)]
partial struct Rectangle {
    public int X, Y;
    public int Width, Height;
}

// Both views work on the same struct
var rect = new Rectangle { X = 10, Y = 20, Width = 100, Height = 50 };
ref readonly var pos = ref rect.AsPoint2D();     // offset 0
ref readonly var size = ref rect.AsSize2D();      // offset 8
Console.WriteLine($"Position: ({pos.X}, {pos.Y}), Size: {size.Width}x{size.Height}");
```

**Cost:** Zero — `Unsafe.AddByteOffset` + `Unsafe.As`, both inlined away by JIT.

### Pattern 5: TraitSpan Iteration

```csharp
Rectangle[] rects = GetRectangles();

// Iterate over size view of each rectangle — no copying
foreach (ref readonly var size in rects.AsSpan().AsSize2DSpan())
{
    Console.WriteLine($"{size.Width}x{size.Height}");
}

// Mutable access
foreach (ref var pos in rects.AsSpan().AsPoint2DTraitSpan())
{
    pos.X += 10;  // Translate in place
}
```

**Cost:** One pointer add per element (offset + stride). No allocation, no copying.

---

## Attribute API

### TraitAttribute

```csharp
/// <summary>
/// Marks an interface as a trait, triggering code generation.
/// </summary>
[AttributeUsage(AttributeTargets.Interface, AllowMultiple = false, Inherited = false)]
public sealed class TraitAttribute : Attribute {
    /// <summary>
    /// Generate a layout struct for zero-copy field access.
    /// Default: true
    /// </summary>
    public bool GenerateLayout { get; set; } = true;
    
    /// <summary>
    /// Generate extension methods for ergonomic access.
    /// Default: true
    /// </summary>
    public bool GenerateExtensions { get; set; } = true;
    
    /// <summary>
    /// Generate static methods on the trait interface.
    /// Default: true
    /// </summary>
    public bool GenerateStaticMethods { get; set; } = true;
    
    /// <summary>
    /// Namespace for generated code. If null, uses trait's namespace.
    /// </summary>
    public string? GeneratedNamespace { get; set; }
}
```

### ImplementsTraitAttribute

```csharp
/// <summary>
/// Indicates this type implements the specified trait.
/// Type must be partial struct/class.
/// </summary>
[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class, AllowMultiple = true)]
public sealed class ImplementsTraitAttribute : Attribute {
    /// <summary>
    /// The trait interface this type implements.
    /// </summary>
    public Type TraitType { get; }
    
    /// <summary>
    /// How to implement the trait.
    /// </summary>
    public ImplStrategy Strategy { get; set; } = ImplStrategy.Auto;
    
    /// <summary>
    /// For Strategy.FieldMapping - specify custom field names.
    /// Format: "TraitProperty:ActualField,..."
    /// Example: "X:PositionX,Y:PositionY"
    /// </summary>
    public string? FieldMapping { get; set; }
    
    public ImplementsTraitAttribute(Type traitType) {
        TraitType = traitType ?? throw new ArgumentNullException(nameof(traitType));
    }
}
```

### RegisterTraitImplAttribute

```csharp
/// <summary>
/// Registers a trait implementation for an external type (assembly-level).
/// </summary>
[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class RegisterTraitImplAttribute : Attribute {
    /// <summary>
    /// The trait interface to implement.
    /// </summary>
    public Type TraitType { get; }
    
    /// <summary>
    /// The external type to add trait implementation for.
    /// </summary>
    public Type TargetType { get; }
    
    /// <summary>
    /// How to implement the trait on the external type.
    /// </summary>
    public ImplStrategy Strategy { get; set; } = ImplStrategy.Auto;
    
    /// <summary>
    /// For Strategy.FieldMapping - map trait properties to target type members.
    /// </summary>
    public string? FieldMapping { get; set; }
    
    public RegisterTraitImplAttribute(Type traitType, Type targetType) {
        TraitType = traitType ?? throw new ArgumentNullException(nameof(traitType));
        TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
    }
}
```

### ImplStrategy Enum

```csharp
/// <summary>
/// Strategy for implementing a trait.
/// All strategies require layout-compatible fields.
/// Layout incompatibility is always a compile error.
/// </summary>
public enum ImplStrategy {
    /// <summary>
    /// Generator verifies layout and emits reinterpret cast.
    /// Emit compile ERROR if layout incompatible.
    /// </summary>
    Auto = 0,

    /// <summary>
    /// Explicit declaration of reinterpret cast intent.
    /// Identical to Auto. Useful for self-documenting code.
    /// </summary>
    Reinterpret = 1,

    /// <summary>
    /// Trait properties map to different field names.
    /// Requires FieldMapping specification.
    /// Mapped fields must still be layout-compatible (contiguous, correct types).
    /// </summary>
    FieldMapping = 2
}
```

---

## Layout Compatibility Rules

### Automatic Verification

Generator performs compile-time checks. Layout compatibility is a **hard requirement** — there is no fallback. If the layout doesn't match, the type doesn't implement the trait.

```csharp
[Trait]
interface IPoint2D {
    float X { get; }
    float Y { get; }
}

[Trait]
interface ISize2D {
    float Width { get; }
    float Height { get; }
}

// ✅ VALID - Exact prefix match (offset 0)
[StructLayout(LayoutKind.Sequential)]
partial struct Point3D {
    public float X, Y, Z;  // X, Y at offset 0, 4
}

// ✅ VALID - Contiguous match at non-zero offset
[ImplementsTrait(typeof(IPoint2D))]
[ImplementsTrait(typeof(ISize2D))]
[StructLayout(LayoutKind.Sequential)]
partial struct Rectangle {
    public float X, Y;          // IPoint2D at offset 0
    public float Width, Height;  // ISize2D at offset 8
}

// ❌ INVALID - Wrong order
[StructLayout(LayoutKind.Sequential)]
partial struct BadPoint {
    public float Y, X;  // Generator ERROR: TE0003
}

// ❌ INVALID - Wrong types
[StructLayout(LayoutKind.Sequential)]
partial struct BadPoint2 {
    public int X;     // Should be float
    public float Y;   // Generator ERROR: TE0002
}

// ❌ ERROR - No StructLayout
partial struct NoLayout {
    public float X, Y;  // Generator ERROR: TE0004
}

// ❌ INVALID - Non-contiguous fields
[StructLayout(LayoutKind.Sequential)]
partial struct SplitPoint {
    public float X;
    public int Tag;     // Interrupts contiguity
    public float Y;     // Generator ERROR: TE0003
}
```

### Verification Algorithm

```
1. Verify [StructLayout(Sequential)] or [StructLayout(Explicit)] is present.
   If missing → emit compile ERROR (TE0004).

2. Find the first trait property P₀ in the implementation type.
   Record its offset as baseOffset.

3. For each property Pᵢ in trait definition (in order):
   a. Find field F in implementation type with matching name
      (or mapped name via FieldMapping).
   b. If not found → emit compile ERROR (TE0001).
   c. Verify F.Type == Pᵢ.Type. If mismatch → ERROR (TE0002).
   d. Calculate expectedOffset = baseOffset + sum(sizeof(P₀..Pᵢ₋₁)).
   e. Verify F.Offset == expectedOffset. If mismatch → ERROR (TE0003).

4. All checks pass → emit Unsafe.As with AddByteOffset(baseOffset).
   If baseOffset == 0, omit the AddByteOffset for cleaner codegen.
```

### Diagnostic Messages

```csharp
// TE0001: Trait implementation missing required property
// TE0002: Property type mismatch (expected {0}, found {1})
// TE0003: Property offset mismatch (expected {0}, found {1} — fields must be contiguous)
// TE0004: Missing [StructLayout] attribute (required for layout verification)
// TE0005: External type not found
// TE0006: Trait interface must contain only properties or methods
// TE0007: Trait property must have getter
// TE0008: Circular trait dependency detected
// TE0009: Trait fields not contiguous in implementing type
```

---

## Implementation Strategy for Generator

### Project Structure

```
TraitEmulation/
├── src/
│   ├── TraitEmulation.Attributes/
│   │   ├── TraitAttribute.cs
│   │   ├── ImplementsTraitAttribute.cs
│   │   ├── RegisterTraitImplAttribute.cs
│   │   └── ImplStrategy.cs
│   ├── TraitEmulation.SourceGenerator/
│   │   ├── TraitGenerator.cs                    (Main generator)
│   │   ├── Models/
│   │   │   ├── TraitModel.cs                    (Parsed trait info)
│   │   │   ├── ImplementationModel.cs           (Parsed impl info)
│   │   │   └── LayoutAnalysis.cs                (Layout verification result)
│   │   ├── Generators/
│   │   │   ├── LayoutStructGenerator.cs
│   │   │   ├── ConstraintInterfaceGenerator.cs
│   │   │   ├── ExtensionMethodsGenerator.cs
│   │   │   ├── StaticMethodsGenerator.cs
│   │   │   ├── ImplementationGenerator.cs
│   │   │   └── TraitSpanFactoryGenerator.cs     (AsXxxSpan extensions)
│   │   ├── Analyzers/
│   │   │   ├── LayoutCompatibilityAnalyzer.cs
│   │   │   ├── TraitUsageAnalyzer.cs
│   │   │   └── DiagnosticDescriptors.cs
│   │   └── Utilities/
│   │       ├── RoslynExtensions.cs
│   │       ├── CodeBuilder.cs
│   │       └── SymbolCache.cs
│   ├── TraitEmulation.Runtime/
│   │   ├── ReadOnlyTraitSpan.cs
│   │   ├── TraitSpan.cs
│   │   ├── ReadOnlyTraitSpan2D.cs
│   │   ├── TraitSpan2D.cs
│   │   └── ThrowHelper.cs
│   └── TraitEmulation/
│       └── (Empty - runtime placeholder for docs)
├── samples/
│   └── TraitExample/
│       └── Program.cs
└── tests/
    ├── TraitEmulation.Generator.Tests/
    │   └── GeneratorTests.cs                    (Compile-time diagnostic tests)
    ├── TraitEmulation.Runtime.Tests/
    │   ├── LayoutCastTests.cs                   (Zero-copy / pointer identity)
    │   ├── TraitSpanTests.cs                    (1D span operations)
    │   ├── TraitSpan2DTests.cs                  (2D span operations)
    │   └── PerformanceRegressionTests.cs        (Zero-allocation verification)
    └── TraitEmulation.Benchmarks/
        └── TraitBenchmark.cs                    (BenchmarkDotNet)
```

### Generator Entry Point

```csharp
[Generator(LanguageNames.CSharp)]
public sealed class TraitGenerator : IIncrementalGenerator {
    public void Initialize(IncrementalGeneratorInitializationContext context) {
        // 1. Collect trait interfaces
        var traitInterfaces = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "TraitEmulation.TraitAttribute",
                predicate: (node, _) => node is InterfaceDeclarationSyntax,
                transform: GetTraitModel)
            .Where(t => t is not null);
        
        // 2. Collect trait implementations
        var implementations = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "TraitEmulation.ImplementsTraitAttribute",
                predicate: (node, _) => node is StructDeclarationSyntax or ClassDeclarationSyntax,
                transform: GetImplementationModel)
            .Where(i => i is not null);
        
        // 3. Collect external registrations
        var externalImpls = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "TraitEmulation.RegisterTraitImplAttribute",
                predicate: (node, _) => true,
                transform: GetExternalImplModel)
            .Where(e => e is not null);
        
        // 4. Combine and generate
        var combined = traitInterfaces
            .Collect()
            .Combine(implementations.Collect())
            .Combine(externalImpls.Collect());
        
        context.RegisterSourceOutput(combined, GenerateCode);
    }
    
    private static void GenerateCode(
        SourceProductionContext context,
        ((ImmutableArray<TraitModel> Traits,
          ImmutableArray<ImplementationModel> Impls),
         ImmutableArray<ExternalImplModel> ExternalImpls) data)
    {
        var (inner, externalImpls) = data;
        var (traits, impls) = inner;
        
        foreach (var trait in traits) {
            // Generate layout struct
            if (trait.GenerateLayout) {
                var layoutCode = LayoutStructGenerator.Generate(trait);
                context.AddSource($"{trait.FullName}.Layout.g.cs", layoutCode);
            }
            
            // Generate constraint interface
            var constraintCode = ConstraintInterfaceGenerator.Generate(trait);
            context.AddSource($"{trait.FullName}.Constraint.g.cs", constraintCode);
            
            // Generate extension methods
            if (trait.GenerateExtensions) {
                var extensionCode = ExtensionMethodsGenerator.Generate(trait);
                context.AddSource($"{trait.FullName}.Extensions.g.cs", extensionCode);
            }
            
            // Generate static methods on interface
            if (trait.GenerateStaticMethods) {
                var staticCode = StaticMethodsGenerator.Generate(trait);
                context.AddSource($"{trait.FullName}.Static.g.cs", staticCode);
            }
        }
        
        // Generate implementations
        foreach (var impl in impls) {
            var implCode = ImplementationGenerator.Generate(impl, context);
            context.AddSource($"{impl.TypeName}.TraitImpl.g.cs", implCode);
        }
        
        // Generate external adapters
        foreach (var external in externalImpls) {
            var adapterCode = ImplementationGenerator.GenerateExternal(external, context);
            context.AddSource($"{external.TargetTypeName}.ExternalImpl.g.cs", adapterCode);
        }
    }
}
```

### Layout Analysis Algorithm

```csharp
public static class LayoutAnalyzer {
    public static LayoutAnalysis Analyze(
        INamedTypeSymbol implementationType,
        TraitModel trait,
        DiagnosticReporter reporter)
    {
        var result = new LayoutAnalysis {
            IsCompatible = true,
            BaseOffset = 0
        };

        // Check StructLayout attribute — hard requirement
        var layoutAttr = implementationType.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "StructLayoutAttribute");

        if (layoutAttr == null) {
            reporter.ReportError("TE0004", implementationType.Locations[0],
                "Missing [StructLayout] attribute. Required for trait layout verification.");
            result.IsCompatible = false;
            return result;
        }

        // Find the first trait property to determine base offset
        var firstTraitProp = trait.Properties[0];
        var firstField = FindMatchingField(implementationType, firstTraitProp.Name);

        if (firstField == null) {
            reporter.ReportError("TE0001", implementationType.Locations[0],
                $"Missing required field '{firstTraitProp.Name}' for trait {trait.Name}");
            result.IsCompatible = false;
            return result;
        }

        int baseOffset = CalculateFieldOffset(firstField, implementationType);
        result.BaseOffset = baseOffset;

        // Verify each trait property is contiguous from baseOffset
        int expectedOffset = baseOffset;
        foreach (var traitProp in trait.Properties) {
            var field = FindMatchingField(implementationType, traitProp.Name);

            if (field == null) {
                reporter.ReportError("TE0001", implementationType.Locations[0],
                    $"Missing required field '{traitProp.Name}' for trait {trait.Name}");
                result.IsCompatible = false;
                continue;
            }

            // Type check
            if (!SymbolEqualityComparer.Default.Equals(field.Type, traitProp.Type)) {
                reporter.ReportError("TE0002", field.Locations[0],
                    $"Type mismatch: expected {traitProp.Type}, found {field.Type}");
                result.IsCompatible = false;
                continue;
            }

            // Offset check — must be contiguous from base
            var actualOffset = CalculateFieldOffset(field, implementationType);
            if (actualOffset != expectedOffset) {
                reporter.ReportError("TE0003", field.Locations[0],
                    $"Field offset mismatch: expected {expectedOffset}, found {actualOffset}. " +
                    "Trait fields must be contiguous in the implementing type.");
                result.IsCompatible = false;
            }

            expectedOffset += GetTypeSize(field.Type);
        }

        return result;
    }

    private static int CalculateFieldOffset(IFieldSymbol field, INamedTypeSymbol type) {
        // Check for explicit [FieldOffset]
        var offsetAttr = field.GetAttributes()
            .FirstOrDefault(a => a.AttributeClass?.Name == "FieldOffsetAttribute");
        if (offsetAttr != null) {
            return (int)offsetAttr.ConstructorArguments[0].Value!;
        }

        // For Sequential layout, sum sizes of preceding fields with alignment
        int offset = 0;
        foreach (var member in type.GetMembers().OfType<IFieldSymbol>()) {
            if (member.IsStatic) continue;
            if (SymbolEqualityComparer.Default.Equals(member, field)) {
                break;
            }
            int fieldSize = GetTypeSize(member.Type);
            // Align to natural boundary: min(fieldSize, packingSize)
            int alignment = Math.Min(fieldSize, 8); // Default packing = 8
            if (alignment > 0) {
                offset = (offset + alignment - 1) & ~(alignment - 1);
            }
            offset += fieldSize;
        }

        // Align the target field itself
        int targetSize = GetTypeSize(field.Type);
        int targetAlignment = Math.Min(targetSize, 8);
        if (targetAlignment > 0) {
            offset = (offset + targetAlignment - 1) & ~(targetAlignment - 1);
        }

        return offset;
    }

    private static int GetTypeSize(ITypeSymbol type) {
        return type.SpecialType switch {
            SpecialType.System_Byte => 1,
            SpecialType.System_SByte => 1,
            SpecialType.System_Boolean => 1,
            SpecialType.System_Int16 => 2,
            SpecialType.System_UInt16 => 2,
            SpecialType.System_Char => 2,
            SpecialType.System_Int32 => 4,
            SpecialType.System_UInt32 => 4,
            SpecialType.System_Single => 4,
            SpecialType.System_Int64 => 8,
            SpecialType.System_UInt64 => 8,
            SpecialType.System_Double => 8,
            SpecialType.System_IntPtr => IntPtr.Size,
            SpecialType.System_UIntPtr => IntPtr.Size,
            _ => GetStructSize(type) // Recurse for nested unmanaged structs
        };
    }

    private static int GetStructSize(ITypeSymbol type) {
        if (type is not INamedTypeSymbol namedType || !namedType.IsValueType)
            return 0; // Unknown — will be caught by other validation

        int size = 0;
        int maxAlignment = 1;
        foreach (var member in namedType.GetMembers().OfType<IFieldSymbol>()) {
            if (member.IsStatic) continue;
            int fieldSize = GetTypeSize(member.Type);
            int alignment = Math.Min(fieldSize, 8);
            maxAlignment = Math.Max(maxAlignment, alignment);
            if (alignment > 0) {
                size = (size + alignment - 1) & ~(alignment - 1);
            }
            size += fieldSize;
        }
        // Pad to struct alignment
        if (maxAlignment > 0) {
            size = (size + maxAlignment - 1) & ~(maxAlignment - 1);
        }
        return size;
    }
}
```

---

## Code Generation Templates

### Layout Struct Template

```csharp
public static string GenerateLayoutStruct(TraitModel trait) {
    var builder = new CodeBuilder();
    
    builder.AppendLine("// <auto-generated/>");
    builder.AppendLine("#nullable enable");
    builder.AppendLine();
    builder.AppendLine($"namespace {trait.Namespace}");
    builder.OpenBrace();
    
    builder.AppendLine("/// <summary>");
    builder.AppendLine($"/// Layout-compatible struct for {trait.Name} trait.");
    builder.AppendLine("/// Used for zero-copy field access via Unsafe.As.");
    builder.AppendLine("/// </summary>");
    builder.AppendLine("[global::System.Runtime.InteropServices.StructLayout(");
    builder.AppendLine("    global::System.Runtime.InteropServices.LayoutKind.Sequential)]");
    builder.AppendLine($"public struct {trait.LayoutStructName}");
    builder.OpenBrace();
    
    foreach (var prop in trait.Properties) {
        builder.AppendLine($"public {prop.TypeName} {prop.Name};");
    }
    
    builder.CloseBrace();
    builder.CloseBrace();
    
    return builder.ToString();
}
```

### Extension Methods Template

```csharp
public static string GenerateExtensionMethods(TraitModel trait) {
    var builder = new CodeBuilder();
    
    builder.AppendLine("// <auto-generated/>");
    builder.AppendLine("#nullable enable");
    builder.AppendLine("using System.Runtime.CompilerServices;");
    builder.AppendLine();
    builder.AppendLine($"namespace {trait.Namespace}");
    builder.OpenBrace();
    
    builder.AppendLine("/// <summary>");
    builder.AppendLine($"/// Extension methods for {trait.Name} trait.");
    builder.AppendLine("/// </summary>");
    builder.AppendLine($"public static class {trait.Name}Extensions");
    builder.OpenBrace();
    
    // AsLayout method
    builder.AppendLine("/// <summary>");
    builder.AppendLine("/// Zero-copy cast to layout struct for direct field access.");
    builder.AppendLine("/// </summary>");
    builder.AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
    builder.AppendLine($"public static ref readonly {trait.LayoutStructName} As{trait.Name}<T>(");
    builder.AppendLine("    this in T self)");
    builder.AppendLine($"    where T : unmanaged, ITrait<{trait.Name}, T>");
    builder.OpenBrace();
    builder.AppendLine("return ref T.AsLayout(in self);");
    builder.CloseBrace();
    builder.AppendLine();
    
    // Individual property accessors
    foreach (var prop in trait.Properties) {
        builder.AppendLine($"/// <summary>");
        builder.AppendLine($"/// Get {prop.Name} value.");
        builder.AppendLine($"/// </summary>");
        builder.AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine($"public static {prop.TypeName} Get{prop.Name}<T>(");
        builder.AppendLine("    this in T self)");
        builder.AppendLine($"    where T : unmanaged, ITrait<{trait.Name}, T>");
        builder.OpenBrace();
        builder.AppendLine($"return T.Get{prop.Name}_Impl(in self);");
        builder.CloseBrace();
        builder.AppendLine();
    }
    
    builder.CloseBrace();
    builder.CloseBrace();
    
    return builder.ToString();
}
```

### Implementation Template

```csharp
public static string GenerateImplementation(ImplementationModel impl, TraitModel trait) {
    var builder = new CodeBuilder();

    builder.AppendLine("// <auto-generated/>");
    builder.AppendLine("#nullable enable");
    builder.AppendLine("using System.Runtime.CompilerServices;");
    builder.AppendLine();
    builder.AppendLine($"namespace {impl.Namespace}");
    builder.OpenBrace();

    builder.AppendLine($"partial {impl.TypeKind} {impl.TypeName} : ");
    builder.AppendLine($"    ITrait<{trait.FullName}, {impl.TypeName}>");
    builder.OpenBrace();

    // Property accessors
    foreach (var prop in trait.Properties) {
        builder.AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
        builder.AppendLine($"public static {prop.TypeName} Get{prop.Name}_Impl(");
        builder.AppendLine($"    in {impl.TypeName} self)");
        builder.OpenBrace();

        if (impl.FieldMapping.TryGetValue(prop.Name, out var fieldName)) {
            builder.AppendLine($"return self.{fieldName};");
        } else {
            builder.AppendLine($"return self.{prop.Name};");
        }

        builder.CloseBrace();
        builder.AppendLine();
    }

    // Layout cast — always reinterpret (layout verified at generation time)
    builder.AppendLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
    builder.AppendLine($"public static ref readonly {trait.LayoutStructName} AsLayout(");
    builder.AppendLine($"    in {impl.TypeName} self)");
    builder.OpenBrace();

    if (impl.BaseOffset == 0) {
        // Trait fields start at beginning of struct — simple cast
        builder.AppendLine($"// SAFETY: Generator verified {impl.TypeName} starts with");
        builder.AppendLine($"// {{{string.Join("; ", trait.Properties.Select(p => $"{p.TypeName} {p.Name}"))}}}");
        builder.AppendLine($"return ref global::System.Runtime.CompilerServices.Unsafe.As<");
        builder.AppendLine($"    {impl.TypeName}, {trait.LayoutStructName}>(");
        builder.AppendLine("    ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in self));");
    } else {
        // Trait fields at non-zero offset — add byte offset before cast
        builder.AppendLine($"// SAFETY: Generator verified {trait.Name} fields at byte offset {impl.BaseOffset}");
        builder.AppendLine($"return ref global::System.Runtime.CompilerServices.Unsafe.As<");
        builder.AppendLine($"    byte, {trait.LayoutStructName}>(");
        builder.AppendLine($"    ref global::System.Runtime.CompilerServices.Unsafe.AddByteOffset(");
        builder.AppendLine($"        ref global::System.Runtime.CompilerServices.Unsafe.As<{impl.TypeName}, byte>(");
        builder.AppendLine($"            ref global::System.Runtime.CompilerServices.Unsafe.AsRef(in self)),");
        builder.AppendLine($"        (nint){impl.BaseOffset}));");
    }

    builder.CloseBrace();
    builder.CloseBrace();
    builder.CloseBrace();

    return builder.ToString();
}
```

---

## Trait Span Types

Trait spans provide strided, offset-aware views over contiguous memory of structs — the same way `Span<T>` and `ReadOnlySpan<T>` provide views over `T[]`, but projecting through a trait's layout at a computed byte offset. The API mirrors `System.Span<T>` and `System.ReadOnlySpan<T>` as closely as possible.

### ReadOnlyTraitSpan\<TLayout\>

```csharp
/// <summary>
/// A read-only view over a contiguous region of unmanaged structs,
/// projected through a trait layout at a fixed byte offset with stride.
/// Analogous to ReadOnlySpan&lt;T&gt; but with offset/stride semantics.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly ref struct ReadOnlyTraitSpan<TLayout>
    where TLayout : unmanaged
{
    private readonly ref byte _reference;
    private readonly int _length;
    private readonly int _stride;
    // _reference already points to baseOffset of first element

    /// <summary>
    /// Creates a ReadOnlyTraitSpan from a byte reference, stride, and length.
    /// </summary>
    /// <param name="reference">Reference to the first trait-view byte (base + offset of element 0).</param>
    /// <param name="stride">Byte distance between successive source elements (sizeof source type).</param>
    /// <param name="length">Number of elements.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlyTraitSpan(ref byte reference, int stride, int length)
    {
        _reference = ref reference;
        _stride = stride;
        _length = length;
    }

    /// <summary>Gets the number of elements in the span.</summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length;
    }

    /// <summary>Gets a value indicating whether this span is empty.</summary>
    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length == 0;
    }

    /// <summary>
    /// Returns a read-only reference to the element at the specified index.
    /// </summary>
    public ref readonly TLayout this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_length)
                ThrowHelper.ThrowIndexOutOfRangeException();
            return ref Unsafe.As<byte, TLayout>(
                ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference),
                    (nint)(index * _stride)));
        }
    }

    /// <summary>
    /// Forms a slice out of the current span starting at the specified index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyTraitSpan<TLayout> Slice(int start)
    {
        if ((uint)start > (uint)_length)
            ThrowHelper.ThrowArgumentOutOfRangeException();
        return new ReadOnlyTraitSpan<TLayout>(
            ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference), (nint)(start * _stride)),
            _stride,
            _length - start);
    }

    /// <summary>
    /// Forms a slice out of the current span starting at the specified index
    /// for the specified length.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyTraitSpan<TLayout> Slice(int start, int length)
    {
        if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
            ThrowHelper.ThrowArgumentOutOfRangeException();
        return new ReadOnlyTraitSpan<TLayout>(
            ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference), (nint)(start * _stride)),
            _stride,
            length);
    }

    /// <summary>
    /// Copies the contents of this span to a destination span.
    /// Each element is copied by value from the strided source.
    /// </summary>
    public void CopyTo(Span<TLayout> destination)
    {
        if ((uint)_length > (uint)destination.Length)
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        for (int i = 0; i < _length; i++) {
            destination[i] = this[i];
        }
    }

    /// <summary>
    /// Copies the contents of this span to a new array.
    /// </summary>
    public TLayout[] ToArray()
    {
        if (_length == 0) return Array.Empty<TLayout>();
        var array = new TLayout[_length];
        CopyTo(array);
        return array;
    }

    /// <summary>Returns an enumerator for this span.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    /// <summary>Enumerates elements of a ReadOnlyTraitSpan.</summary>
    public ref struct Enumerator
    {
        private readonly ref byte _reference;
        private readonly int _stride;
        private readonly int _length;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(ReadOnlyTraitSpan<TLayout> span)
        {
            _reference = ref Unsafe.AsRef(in span._reference);
            _stride = span._stride;
            _length = span._length;
            _index = -1;
        }

        /// <summary>Advances the enumerator to the next element.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int index = _index + 1;
            if (index < _length) {
                _index = index;
                return true;
            }
            return false;
        }

        /// <summary>Gets the element at the current position of the enumerator.</summary>
        public ref readonly TLayout Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.As<byte, TLayout>(
                ref Unsafe.AddByteOffset(ref _reference, (nint)(_index * _stride)));
        }
    }

    /// <summary>Returns an empty ReadOnlyTraitSpan.</summary>
    public static ReadOnlyTraitSpan<TLayout> Empty => default;
}
```

### TraitSpan\<TLayout\>

```csharp
/// <summary>
/// A mutable view over a contiguous region of unmanaged structs,
/// projected through a trait layout at a fixed byte offset with stride.
/// Analogous to Span&lt;T&gt; but with offset/stride semantics.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public ref struct TraitSpan<TLayout>
    where TLayout : unmanaged
{
    private readonly ref byte _reference;
    private readonly int _length;
    private readonly int _stride;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TraitSpan(ref byte reference, int stride, int length)
    {
        _reference = ref reference;
        _stride = stride;
        _length = length;
    }

    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length;
    }

    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _length == 0;
    }

    /// <summary>
    /// Returns a mutable reference to the element at the specified index.
    /// </summary>
    public ref TLayout this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)index >= (uint)_length)
                ThrowHelper.ThrowIndexOutOfRangeException();
            return ref Unsafe.As<byte, TLayout>(
                ref Unsafe.AddByteOffset(ref _reference, (nint)(index * _stride)));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TraitSpan<TLayout> Slice(int start)
    {
        if ((uint)start > (uint)_length)
            ThrowHelper.ThrowArgumentOutOfRangeException();
        return new TraitSpan<TLayout>(
            ref Unsafe.AddByteOffset(ref _reference, (nint)(start * _stride)),
            _stride,
            _length - start);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TraitSpan<TLayout> Slice(int start, int length)
    {
        if ((uint)start > (uint)_length || (uint)length > (uint)(_length - start))
            ThrowHelper.ThrowArgumentOutOfRangeException();
        return new TraitSpan<TLayout>(
            ref Unsafe.AddByteOffset(ref _reference, (nint)(start * _stride)),
            _stride,
            length);
    }

    /// <summary>
    /// Copies from this strided span into a contiguous destination.
    /// </summary>
    public void CopyTo(Span<TLayout> destination)
    {
        if ((uint)_length > (uint)destination.Length)
            ThrowHelper.ThrowArgumentException_DestinationTooShort();
        for (int i = 0; i < _length; i++) {
            destination[i] = this[i];
        }
    }

    /// <summary>
    /// Fills all elements with the specified value.
    /// Writes through the strided view into the source struct fields.
    /// </summary>
    public void Fill(TLayout value)
    {
        for (int i = 0; i < _length; i++) {
            this[i] = value;
        }
    }

    /// <summary>Clears all trait-view fields to default.</summary>
    public void Clear() => Fill(default);

    public TLayout[] ToArray()
    {
        if (_length == 0) return Array.Empty<TLayout>();
        var array = new TLayout[_length];
        CopyTo(array);
        return array;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Enumerator GetEnumerator() => new(this);

    public ref struct Enumerator
    {
        private readonly ref byte _reference;
        private readonly int _stride;
        private readonly int _length;
        private int _index;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal Enumerator(TraitSpan<TLayout> span)
        {
            _reference = ref span._reference;
            _stride = span._stride;
            _length = span._length;
            _index = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext()
        {
            int index = _index + 1;
            if (index < _length) {
                _index = index;
                return true;
            }
            return false;
        }

        public ref TLayout Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => ref Unsafe.As<byte, TLayout>(
                ref Unsafe.AddByteOffset(ref _reference, (nint)(_index * _stride)));
        }
    }

    /// <summary>
    /// Implicit conversion to ReadOnlyTraitSpan.
    /// </summary>
    public static implicit operator ReadOnlyTraitSpan<TLayout>(TraitSpan<TLayout> span) =>
        new(ref span._reference, span._stride, span._length);

    public static TraitSpan<TLayout> Empty => default;
}
```

### ReadOnlyTraitSpan2D\<TLayout\>

```csharp
/// <summary>
/// A read-only 2D view over a contiguous region of unmanaged structs,
/// projected through a trait layout. Provides row/column indexing over
/// data stored in row-major order.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public readonly ref struct ReadOnlyTraitSpan2D<TLayout>
    where TLayout : unmanaged
{
    private readonly ref byte _reference;
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;      // bytes between successive source elements
    private readonly int _rowStride;   // bytes between successive rows (stride * width)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlyTraitSpan2D(ref byte reference, int stride, int width, int height)
    {
        _reference = ref reference;
        _stride = stride;
        _width = width;
        _height = height;
        _rowStride = stride * width;
    }

    /// <summary>Gets the width (number of columns).</summary>
    public int Width
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _width;
    }

    /// <summary>Gets the height (number of rows).</summary>
    public int Height
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _height;
    }

    /// <summary>Gets the total number of elements (Width * Height).</summary>
    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _width * _height;
    }

    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _width == 0 || _height == 0;
    }

    /// <summary>
    /// Returns a read-only reference to the element at (row, col).
    /// </summary>
    public ref readonly TLayout this[int row, int col]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)row >= (uint)_height || (uint)col >= (uint)_width)
                ThrowHelper.ThrowIndexOutOfRangeException();
            return ref Unsafe.As<byte, TLayout>(
                ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference),
                    (nint)(row * _rowStride + col * _stride)));
        }
    }

    /// <summary>
    /// Gets a single row as a ReadOnlyTraitSpan.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyTraitSpan<TLayout> GetRow(int row)
    {
        if ((uint)row >= (uint)_height)
            ThrowHelper.ThrowArgumentOutOfRangeException();
        return new ReadOnlyTraitSpan<TLayout>(
            ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference), (nint)(row * _rowStride)),
            _stride,
            _width);
    }

    /// <summary>
    /// Gets a sub-region of this 2D span.
    /// </summary>
    public ReadOnlyTraitSpan2D<TLayout> Slice(int rowStart, int colStart, int height, int width)
    {
        if ((uint)rowStart > (uint)_height || (uint)height > (uint)(_height - rowStart))
            ThrowHelper.ThrowArgumentOutOfRangeException();
        if ((uint)colStart > (uint)_width || (uint)width > (uint)(_width - colStart))
            ThrowHelper.ThrowArgumentOutOfRangeException();
        return new ReadOnlyTraitSpan2D<TLayout>(
            ref Unsafe.AddByteOffset(ref Unsafe.AsRef(in _reference),
                (nint)(rowStart * _rowStride + colStart * _stride)),
            _stride,
            width,
            height);
    }

    /// <summary>
    /// Flattens to a 1D ReadOnlyTraitSpan (row-major order).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlyTraitSpan<TLayout> AsSpan() =>
        new(ref Unsafe.AsRef(in _reference), _stride, _width * _height);

    /// <summary>Enumerates rows.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public RowEnumerator EnumerateRows() => new(this);

    public ref struct RowEnumerator
    {
        private readonly ReadOnlyTraitSpan2D<TLayout> _span;
        private int _row;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal RowEnumerator(ReadOnlyTraitSpan2D<TLayout> span)
        {
            _span = span;
            _row = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => ++_row < _span._height;

        public ReadOnlyTraitSpan<TLayout> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _span.GetRow(_row);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RowEnumerator GetEnumerator() => this;
    }

    public static ReadOnlyTraitSpan2D<TLayout> Empty => default;
}
```

### TraitSpan2D\<TLayout\>

```csharp
/// <summary>
/// A mutable 2D view over a contiguous region of unmanaged structs,
/// projected through a trait layout. Provides row/column indexing over
/// data stored in row-major order.
/// </summary>
[StructLayout(LayoutKind.Auto)]
public ref struct TraitSpan2D<TLayout>
    where TLayout : unmanaged
{
    private readonly ref byte _reference;
    private readonly int _width;
    private readonly int _height;
    private readonly int _stride;
    private readonly int _rowStride;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal TraitSpan2D(ref byte reference, int stride, int width, int height)
    {
        _reference = ref reference;
        _stride = stride;
        _width = width;
        _height = height;
        _rowStride = stride * width;
    }

    public int Width
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _width;
    }

    public int Height
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _height;
    }

    public int Length
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _width * _height;
    }

    public bool IsEmpty
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _width == 0 || _height == 0;
    }

    public ref TLayout this[int row, int col]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)row >= (uint)_height || (uint)col >= (uint)_width)
                ThrowHelper.ThrowIndexOutOfRangeException();
            return ref Unsafe.As<byte, TLayout>(
                ref Unsafe.AddByteOffset(ref _reference,
                    (nint)(row * _rowStride + col * _stride)));
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TraitSpan<TLayout> GetRow(int row)
    {
        if ((uint)row >= (uint)_height)
            ThrowHelper.ThrowArgumentOutOfRangeException();
        return new TraitSpan<TLayout>(
            ref Unsafe.AddByteOffset(ref _reference, (nint)(row * _rowStride)),
            _stride,
            _width);
    }

    public TraitSpan2D<TLayout> Slice(int rowStart, int colStart, int height, int width)
    {
        if ((uint)rowStart > (uint)_height || (uint)height > (uint)(_height - rowStart))
            ThrowHelper.ThrowArgumentOutOfRangeException();
        if ((uint)colStart > (uint)_width || (uint)width > (uint)(_width - colStart))
            ThrowHelper.ThrowArgumentOutOfRangeException();
        return new TraitSpan2D<TLayout>(
            ref Unsafe.AddByteOffset(ref _reference,
                (nint)(rowStart * _rowStride + colStart * _stride)),
            _stride,
            width,
            height);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public TraitSpan<TLayout> AsSpan() =>
        new(ref _reference, _stride, _width * _height);

    /// <summary>
    /// Fills all elements across all rows with the specified value.
    /// </summary>
    public void Fill(TLayout value)
    {
        for (int r = 0; r < _height; r++) {
            var row = GetRow(r);
            row.Fill(value);
        }
    }

    public void Clear() => Fill(default);

    public RowEnumerator EnumerateRows() => new(this);

    public ref struct RowEnumerator
    {
        private readonly TraitSpan2D<TLayout> _span;
        private int _row;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal RowEnumerator(TraitSpan2D<TLayout> span)
        {
            _span = span;
            _row = -1;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool MoveNext() => ++_row < _span._height;

        public TraitSpan<TLayout> Current
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get => _span.GetRow(_row);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public RowEnumerator GetEnumerator() => this;
    }

    public static implicit operator ReadOnlyTraitSpan2D<TLayout>(TraitSpan2D<TLayout> span) =>
        new(ref span._reference, span._stride, span._width, span._height);

    public static TraitSpan2D<TLayout> Empty => default;
}
```

### Generated Factory Extension Methods

The generator produces factory methods for each trait/type pair:

```csharp
// Generated for Rectangle implementing ISize2D (at offset 8)
public static class ISize2DSpanExtensions {
    /// <summary>
    /// Creates a read-only trait span viewing ISize2D fields across all elements.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyTraitSpan<Size2DLayout> AsSize2DSpan<T>(
        this ReadOnlySpan<T> source)
        where T : unmanaged, ITrait<ISize2D, T>
    {
        int offset = T.TraitOffset;  // Compile-time constant per T (e.g. 8 for Rectangle)
        return new ReadOnlyTraitSpan<Size2DLayout>(
            ref Unsafe.AddByteOffset(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(source)),
                (nint)offset),
            Unsafe.SizeOf<T>(),
            source.Length);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TraitSpan<Size2DLayout> AsSize2DTraitSpan<T>(
        this Span<T> source)
        where T : unmanaged, ITrait<ISize2D, T>
    {
        int offset = T.TraitOffset;
        return new TraitSpan<Size2DLayout>(
            ref Unsafe.AddByteOffset(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(source)),
                (nint)offset),
            Unsafe.SizeOf<T>(),
            source.Length);
    }

    /// <summary>
    /// Creates a 2D read-only trait span with the given dimensions.
    /// source.Length must equal width * height.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlyTraitSpan2D<Size2DLayout> AsSize2DSpan2D<T>(
        this ReadOnlySpan<T> source, int width, int height)
        where T : unmanaged, ITrait<ISize2D, T>
    {
        if (source.Length != width * height)
            ThrowHelper.ThrowArgumentException_InvalidDimensions();
        int offset = T.TraitOffset;
        return new ReadOnlyTraitSpan2D<Size2DLayout>(
            ref Unsafe.AddByteOffset(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(source)),
                (nint)offset),
            Unsafe.SizeOf<T>(),
            width,
            height);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TraitSpan2D<Size2DLayout> AsSize2DTraitSpan2D<T>(
        this Span<T> source, int width, int height)
        where T : unmanaged, ITrait<ISize2D, T>
    {
        if (source.Length != width * height)
            ThrowHelper.ThrowArgumentException_InvalidDimensions();
        int offset = T.TraitOffset;
        return new TraitSpan2D<Size2DLayout>(
            ref Unsafe.AddByteOffset(
                ref Unsafe.As<T, byte>(ref MemoryMarshal.GetReference(source)),
                (nint)offset),
            Unsafe.SizeOf<T>(),
            width,
            height);
    }
}
```

### Trait Span Usage Examples

```csharp
[Trait] interface IPoint2D { int X { get; } int Y { get; } }
[Trait] interface ISize2D { int Width { get; } int Height { get; } }

[ImplementsTrait(typeof(IPoint2D))]
[ImplementsTrait(typeof(ISize2D))]
[StructLayout(LayoutKind.Sequential)]
partial struct Rectangle { public int X, Y, Width, Height; }

// --- 1D usage ---

Rectangle[] rects = new Rectangle[1000];
Span<Rectangle> span = rects;

// Read-only iteration over sizes
foreach (ref readonly var size in span.AsSize2DSpan())
    Console.WriteLine($"{size.Width}x{size.Height}");

// Mutable: shift all positions
foreach (ref var pos in span.AsPoint2DTraitSpan())
{
    pos.X += 10;
    pos.Y += 5;
}

// Slice support
var firstTen = span.AsSize2DSpan().Slice(0, 10);

// Copy to contiguous array
Size2DLayout[] sizes = span.AsSize2DSpan().ToArray();

// --- 2D usage ---

const int W = 100, H = 10;
var grid = new Rectangle[W * H];
Span<Rectangle> gridSpan = grid;

// Create 2D view
var grid2D = gridSpan.AsPoint2DTraitSpan2D(W, H);

// Access by row/col
ref var topLeft = ref grid2D[0, 0];
ref var center = ref grid2D[H / 2, W / 2];

// Iterate rows
foreach (var row in grid2D.EnumerateRows())
{
    foreach (ref var pos in row)
    {
        pos.X *= 2;
    }
}

// Sub-region
var subRegion = grid2D.Slice(rowStart: 2, colStart: 5, height: 3, width: 10);
```

---

## Real-World Example: Data Processing Pipeline

### Define Traits

```csharp
using TraitEmulation;

[Trait(GenerateLayout = true)]
public interface IPixelCoordinate {
    int X { get; }
    int Y { get; }
}

[Trait(GenerateLayout = true)]
public interface IRGBPixel {
    byte R { get; }
    byte G { get; }
    byte B { get; }
}
```

### Implement on Custom Types

```csharp
[ImplementsTrait(typeof(IPixelCoordinate))]
[ImplementsTrait(typeof(IRGBPixel))]
[StructLayout(LayoutKind.Sequential)]
public partial struct BayerPixel {
    public int X, Y;
    public byte R, G, B, A;
}

[ImplementsTrait(typeof(IPixelCoordinate))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Point3D {
    public int X, Y;
    public float Z;
}
```

### Register System Types

```csharp
[assembly: RegisterTraitImpl(typeof(IPixelCoordinate), typeof(System.Drawing.Point))]
```

### Write Generic Algorithms

```csharp
public static class Processing {
    /// <summary>
    /// Apply to any pixel type with coordinates.
    /// </summary>
    public static void Apply<T>(
        Span<T> pixels,
        ReadOnlySpan<float> correction,
        int width)
        where T : unmanaged, ITrait<IPixelCoordinate, T>
    {
        for (int i = 0; i < pixels.Length; i++) {
            ref var pixel = ref pixels[i];
            ref readonly var coord = ref pixel.AsPixelCoordinate();
            
            int offset = coord.Y * width + coord.X;
            float factor = correction[offset];
            
            // Apply correction (type-specific logic would go here)
        }
    }
    
    /// <summary>
    /// Compute distance between any two coordinate types.
    /// </summary>
    public static float Distance<T1, T2>(in T1 p1, in T2 p2)
        where T1 : unmanaged, ITrait<IPixelCoordinate, T1>
        where T2 : unmanaged, ITrait<IPixelCoordinate, T2>
    {
        ref readonly var c1 = ref p1.AsPixelCoordinate();
        ref readonly var c2 = ref p2.AsPixelCoordinate();
        
        int dx = c1.X - c2.X;
        int dy = c1.Y - c2.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
    
    /// <summary>
    /// Convert any RGB pixel to grayscale.
    /// </summary>
    public static byte ToGrayscale<T>(in T pixel)
        where T : unmanaged, ITrait<IRGBPixel, T>
    {
        ref readonly var rgb = ref pixel.AsRGBPixel();
        return (byte)(0.299f * rgb.R + 0.587f * rgb.G + 0.114f * rgb.B);
    }
}
```

### Usage Example

```csharp
// Works with custom types
BayerPixel[] bayerPixels = LoadData();
Processing.Apply(bayerPixels, factors, width);

// Works with System types
System.Drawing.Point[] points = GetPoints();
var sysPoint = points[0];
var bayerPixel = bayerPixels[0];
float dist = Processing.Distance(sysPoint, bayerPixel);  // Mixed types!

// No boxing, zero copy, full type safety
Console.WriteLine($"Distance: {dist}");
```

---

## Performance Characteristics

### Benchmarks

```csharp
public class TraitBenchmark {
    private BayerPixel[] _pixels;
    
    [GlobalSetup]
    public void Setup() {
        _pixels = new BayerPixel[10000];
        for (int i = 0; i < _pixels.Length; i++) {
            _pixels[i] = new BayerPixel { X = i % 100, Y = i / 100 };
        }
    }
    
    [Benchmark(Baseline = true)]
    public int Direct_FieldAccess() {
        int sum = 0;
        for (int i = 0; i < _pixels.Length; i++) {
            sum += _pixels[i].X + _pixels[i].Y;
        }
        return sum;
    }
    
    [Benchmark]
    public int Trait_LayoutCast() {
        int sum = 0;
        for (int i = 0; i < _pixels.Length; i++) {
            ref readonly var coord = ref _pixels[i].AsPixelCoordinate();
            sum += coord.X + coord.Y;
        }
        return sum;
    }
    
    [Benchmark]
    public int Trait_ExtensionMethods() {
        int sum = 0;
        for (int i = 0; i < _pixels.Length; i++) {
            sum += _pixels[i].GetX() + _pixels[i].GetY();
        }
        return sum;
    }
    
    [Benchmark]
    public int Interface_Boxing() {
        IPixelCoordinate[] pixels = _pixels;  // Boxing!
        int sum = 0;
        for (int i = 0; i < pixels.Length; i++) {
            sum += pixels[i].X + pixels[i].Y;
        }
        return sum;
    }
}
```

**Expected Results:**

| Method                  | Mean    | Ratio | Allocated |
|-------------------------|---------|-------|-----------|
| Direct_FieldAccess      | 5.2 μs  | 1.00  | -         |
| Trait_LayoutCast        | 5.3 μs  | 1.02  | -         |
| Trait_ExtensionMethods  | 5.2 μs  | 1.00  | -         |
| Interface_Boxing        | 45.8 μs | 8.81  | 160 KB    |

**Key Takeaway:** Trait approach is **~9x faster** than interface with **zero allocations**.

---

## Testing Strategy

Tests are **mandatory** — every feature must have corresponding tests before it is considered complete. Tests are organized in three tiers: generator diagnostics (compile-time), runtime correctness (zero-copy verification), and trait span operations.

### Required: Generator Diagnostic Tests

These verify the generator produces correct output and rejects invalid input.

```csharp
[TestClass]
public class GeneratorTests {
    [TestMethod]
    public void Generator_ProducesLayoutStruct()
    {
        var source = """
            using TraitEmulation;
            [Trait]
            interface IPoint { int X { get; } int Y { get; } }
            """;
        var result = RunGenerator(source);
        Assert.IsTrue(result.GeneratedSources.Any(s =>
            s.SourceText.ToString().Contains("struct PointLayout")));
    }

    [TestMethod]
    public void Generator_ProducesConstraintInterface()
    {
        var source = """
            using TraitEmulation;
            [Trait]
            interface IPoint { int X { get; } int Y { get; } }
            """;
        var result = RunGenerator(source);
        Assert.IsTrue(result.GeneratedSources.Any(s =>
            s.SourceText.ToString().Contains("ITrait<IPoint, TSelf>")));
    }

    [TestMethod]
    public void Generator_ProducesExtensionMethods()
    {
        var source = """
            using TraitEmulation;
            [Trait]
            interface IPoint { int X { get; } int Y { get; } }
            """;
        var result = RunGenerator(source);
        Assert.IsTrue(result.GeneratedSources.Any(s =>
            s.SourceText.ToString().Contains("AsPoint")));
    }

    [TestMethod]
    public void TE0001_MissingRequiredField()
    {
        var source = """
            using TraitEmulation;
            using System.Runtime.InteropServices;
            [Trait] interface IPoint { int X { get; } int Y { get; } }
            [ImplementsTrait(typeof(IPoint))]
            [StructLayout(LayoutKind.Sequential)]
            partial struct Bad { public int X; }
            """;
        var result = RunGenerator(source);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "TE0001"));
    }

    [TestMethod]
    public void TE0002_PropertyTypeMismatch()
    {
        var source = """
            using TraitEmulation;
            using System.Runtime.InteropServices;
            [Trait] interface IPoint { int X { get; } int Y { get; } }
            [ImplementsTrait(typeof(IPoint))]
            [StructLayout(LayoutKind.Sequential)]
            partial struct Bad { public float X; public int Y; }
            """;
        var result = RunGenerator(source);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "TE0002"));
    }

    [TestMethod]
    public void TE0003_FieldOrderMismatch()
    {
        var source = """
            using TraitEmulation;
            using System.Runtime.InteropServices;
            [Trait] interface IPoint { int X { get; } int Y { get; } }
            [ImplementsTrait(typeof(IPoint))]
            [StructLayout(LayoutKind.Sequential)]
            partial struct Bad { public int Y, X; }
            """;
        var result = RunGenerator(source);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "TE0003"));
    }

    [TestMethod]
    public void TE0004_MissingStructLayout()
    {
        var source = """
            using TraitEmulation;
            [Trait] interface IPoint { int X { get; } int Y { get; } }
            [ImplementsTrait(typeof(IPoint))]
            partial struct Bad { public int X, Y; }
            """;
        var result = RunGenerator(source);
        Assert.IsTrue(result.Diagnostics.Any(d => d.Id == "TE0004"));
    }

    [TestMethod]
    public void TE0009_NonContiguousFields()
    {
        var source = """
            using TraitEmulation;
            using System.Runtime.InteropServices;
            [Trait] interface IPoint { int X { get; } int Y { get; } }
            [ImplementsTrait(typeof(IPoint))]
            [StructLayout(LayoutKind.Sequential)]
            partial struct Bad { public int X; public int Tag; public int Y; }
            """;
        var result = RunGenerator(source);
        Assert.IsTrue(result.Diagnostics.Any(d =>
            d.Id == "TE0003" || d.Id == "TE0009"));
    }

    [TestMethod]
    public void ValidPrefixMatch_NoErrors()
    {
        var source = """
            using TraitEmulation;
            using System.Runtime.InteropServices;
            [Trait] interface IPoint { int X { get; } int Y { get; } }
            [ImplementsTrait(typeof(IPoint))]
            [StructLayout(LayoutKind.Sequential)]
            partial struct Point3D { public int X, Y; public float Z; }
            """;
        var result = RunGenerator(source);
        Assert.IsFalse(result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
    }

    [TestMethod]
    public void ValidOffsetMatch_NoErrors()
    {
        var source = """
            using TraitEmulation;
            using System.Runtime.InteropServices;
            [Trait] interface IPoint { int X { get; } int Y { get; } }
            [Trait] interface ISize { int Width { get; } int Height { get; } }
            [ImplementsTrait(typeof(IPoint))]
            [ImplementsTrait(typeof(ISize))]
            [StructLayout(LayoutKind.Sequential)]
            partial struct Rect { public int X, Y, Width, Height; }
            """;
        var result = RunGenerator(source);
        Assert.IsFalse(result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
    }

    [TestMethod]
    public void FieldMapping_CustomNames_NoErrors()
    {
        var source = """
            using TraitEmulation;
            using System.Runtime.InteropServices;
            [Trait] interface IPoint { int X { get; } int Y { get; } }
            [ImplementsTrait(typeof(IPoint), Strategy = ImplStrategy.FieldMapping,
                FieldMapping = "X:PosX,Y:PosY")]
            [StructLayout(LayoutKind.Sequential)]
            partial struct Custom { public int PosX, PosY; }
            """;
        var result = RunGenerator(source);
        Assert.IsFalse(result.Diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error));
    }
}
```

### Required: Runtime Correctness Tests

These verify zero-copy semantics, correct field values, and pointer identity.

```csharp
[TestClass]
public class LayoutCastTests {
    [TestMethod]
    public void PrefixLayout_ZeroCopy_PointerIdentity()
    {
        var pixel = new BayerPixel { X = 10, Y = 20, R = 255 };
        ref readonly var coord = ref pixel.AsPixelCoordinate();

        Assert.AreEqual(10, coord.X);
        Assert.AreEqual(20, coord.Y);

        // Pointer identity proves zero-copy
        unsafe {
            fixed (int* pixelPtr = &pixel.X)
            fixed (int* coordPtr = &coord.X) {
                Assert.IsTrue(pixelPtr == coordPtr, "Not zero-copy — pointers differ!");
            }
        }
    }

    [TestMethod]
    public void OffsetLayout_ZeroCopy_CorrectValues()
    {
        var rect = new Rectangle { X = 1, Y = 2, Width = 100, Height = 50 };

        ref readonly var pos = ref rect.AsPoint2D();
        ref readonly var size = ref rect.AsSize2D();

        Assert.AreEqual(1, pos.X);
        Assert.AreEqual(2, pos.Y);
        Assert.AreEqual(100, size.Width);
        Assert.AreEqual(50, size.Height);
    }

    [TestMethod]
    public void OffsetLayout_ZeroCopy_PointerIdentity()
    {
        var rect = new Rectangle { X = 1, Y = 2, Width = 100, Height = 50 };
        ref readonly var size = ref rect.AsSize2D();

        unsafe {
            fixed (int* rectWidthPtr = &rect.Width)
            fixed (int* sizeWidthPtr = &size.Width) {
                Assert.IsTrue(rectWidthPtr == sizeWidthPtr,
                    "Offset trait is not zero-copy — pointers differ!");
            }
        }
    }

    [TestMethod]
    public void ExtensionMethod_MatchesLayoutCast()
    {
        var pixel = new BayerPixel { X = 42, Y = 99 };
        ref readonly var layout = ref pixel.AsPixelCoordinate();

        Assert.AreEqual(layout.X, pixel.GetX());
        Assert.AreEqual(layout.Y, pixel.GetY());
    }

    [TestMethod]
    public void ExternalType_ExtensionMethod_ZeroCopy()
    {
        var sysPoint = new System.Drawing.Point(10, 20);
        ref readonly var coord = ref sysPoint.AsPixelCoordinate();

        Assert.AreEqual(10, coord.X);
        Assert.AreEqual(20, coord.Y);
    }

    [TestMethod]
    public void MultipleTrait_SameStruct_IndependentViews()
    {
        var pixel = new BayerPixel { X = 5, Y = 10, R = 200, G = 100, B = 25, A = 255 };

        ref readonly var coord = ref pixel.AsPixelCoordinate();
        ref readonly var rgb = ref pixel.AsRGBPixel();

        Assert.AreEqual(5, coord.X);
        Assert.AreEqual(10, coord.Y);
        Assert.AreEqual(200, rgb.R);
        Assert.AreEqual(100, rgb.G);
        Assert.AreEqual(25, rgb.B);
    }

    [TestMethod]
    public void MutableTraitSpan_WritesBack_ToSourceFields()
    {
        var rects = new Rectangle[] {
            new() { X = 0, Y = 0, Width = 10, Height = 10 },
            new() { X = 1, Y = 1, Width = 20, Height = 20 },
        };

        foreach (ref var pos in rects.AsSpan().AsPoint2DTraitSpan())
        {
            pos.X += 100;
            pos.Y += 200;
        }

        Assert.AreEqual(100, rects[0].X);
        Assert.AreEqual(200, rects[0].Y);
        Assert.AreEqual(101, rects[1].X);
        Assert.AreEqual(201, rects[1].Y);
        // Size fields untouched
        Assert.AreEqual(10, rects[0].Width);
        Assert.AreEqual(20, rects[1].Width);
    }
}
```

### Required: Trait Span Tests

```csharp
[TestClass]
public class TraitSpanTests {
    private Rectangle[] _data;

    [TestInitialize]
    public void Setup()
    {
        _data = new Rectangle[10];
        for (int i = 0; i < _data.Length; i++)
            _data[i] = new Rectangle { X = i, Y = i * 10, Width = i + 1, Height = (i + 1) * 2 };
    }

    [TestMethod]
    public void ReadOnlyTraitSpan_Length_MatchesSource()
    {
        var span = _data.AsSpan().AsSize2DSpan();
        Assert.AreEqual(_data.Length, span.Length);
    }

    [TestMethod]
    public void ReadOnlyTraitSpan_Indexer_ReturnsCorrectValues()
    {
        var span = _data.AsSpan().AsSize2DSpan();
        for (int i = 0; i < span.Length; i++) {
            Assert.AreEqual(i + 1, span[i].Width);
            Assert.AreEqual((i + 1) * 2, span[i].Height);
        }
    }

    [TestMethod]
    [ExpectedException(typeof(IndexOutOfRangeException))]
    public void ReadOnlyTraitSpan_Indexer_BoundsCheck()
    {
        var span = _data.AsSpan().AsSize2DSpan();
        _ = span[span.Length]; // Should throw
    }

    [TestMethod]
    public void ReadOnlyTraitSpan_Slice_SubsetCorrect()
    {
        var span = _data.AsSpan().AsPoint2DSpan();
        var sliced = span.Slice(3, 4);
        Assert.AreEqual(4, sliced.Length);
        Assert.AreEqual(3, sliced[0].X);
        Assert.AreEqual(6, sliced[3].X);
    }

    [TestMethod]
    public void ReadOnlyTraitSpan_ToArray_Copies()
    {
        var span = _data.AsSpan().AsPoint2DSpan();
        var array = span.ToArray();
        Assert.AreEqual(span.Length, array.Length);
        for (int i = 0; i < array.Length; i++) {
            Assert.AreEqual(span[i].X, array[i].X);
            Assert.AreEqual(span[i].Y, array[i].Y);
        }
    }

    [TestMethod]
    public void ReadOnlyTraitSpan_Enumeration_AllElements()
    {
        var span = _data.AsSpan().AsPoint2DSpan();
        int count = 0;
        foreach (ref readonly var pos in span) {
            Assert.AreEqual(count, pos.X);
            count++;
        }
        Assert.AreEqual(_data.Length, count);
    }

    [TestMethod]
    public void ReadOnlyTraitSpan_Empty_IsEmpty()
    {
        var empty = ReadOnlyTraitSpan<Point2DLayout>.Empty;
        Assert.IsTrue(empty.IsEmpty);
        Assert.AreEqual(0, empty.Length);
    }

    [TestMethod]
    public void TraitSpan_Fill_SetsAllElements()
    {
        var rects = new Rectangle[5];
        var span = rects.AsSpan().AsSize2DTraitSpan();
        span.Fill(new Size2DLayout { Width = 42, Height = 99 });

        for (int i = 0; i < rects.Length; i++) {
            Assert.AreEqual(42, rects[i].Width);
            Assert.AreEqual(99, rects[i].Height);
        }
    }

    [TestMethod]
    public void TraitSpan_Clear_ZeroesTraitFields()
    {
        var rects = new Rectangle[] {
            new() { X = 1, Y = 2, Width = 3, Height = 4 }
        };
        rects.AsSpan().AsSize2DTraitSpan().Clear();

        Assert.AreEqual(0, rects[0].Width);
        Assert.AreEqual(0, rects[0].Height);
        // Non-trait fields preserved
        Assert.AreEqual(1, rects[0].X);
        Assert.AreEqual(2, rects[0].Y);
    }

    [TestMethod]
    public void TraitSpan_ImplicitConversion_ToReadOnly()
    {
        TraitSpan<Point2DLayout> mutable = _data.AsSpan().AsPoint2DTraitSpan();
        ReadOnlyTraitSpan<Point2DLayout> readOnly = mutable; // Implicit
        Assert.AreEqual(mutable.Length, readOnly.Length);
        Assert.AreEqual(mutable[0].X, readOnly[0].X);
    }
}
```

### Required: Trait Span 2D Tests

```csharp
[TestClass]
public class TraitSpan2DTests {
    private const int W = 4, H = 3;
    private Rectangle[] _grid;

    [TestInitialize]
    public void Setup()
    {
        _grid = new Rectangle[W * H];
        for (int r = 0; r < H; r++)
            for (int c = 0; c < W; c++)
                _grid[r * W + c] = new Rectangle {
                    X = c, Y = r, Width = c * 10, Height = r * 10
                };
    }

    [TestMethod]
    public void ReadOnlyTraitSpan2D_Dimensions()
    {
        var span2D = _grid.AsSpan().AsPoint2DSpan2D(W, H);
        Assert.AreEqual(W, span2D.Width);
        Assert.AreEqual(H, span2D.Height);
        Assert.AreEqual(W * H, span2D.Length);
    }

    [TestMethod]
    public void ReadOnlyTraitSpan2D_RowColIndexing()
    {
        var span2D = _grid.AsSpan().AsPoint2DSpan2D(W, H);
        for (int r = 0; r < H; r++)
            for (int c = 0; c < W; c++) {
                Assert.AreEqual(c, span2D[r, c].X);
                Assert.AreEqual(r, span2D[r, c].Y);
            }
    }

    [TestMethod]
    public void ReadOnlyTraitSpan2D_GetRow_CorrectLength()
    {
        var span2D = _grid.AsSpan().AsSize2DSpan2D(W, H);
        var row = span2D.GetRow(1);
        Assert.AreEqual(W, row.Length);
        for (int c = 0; c < W; c++)
            Assert.AreEqual(c * 10, row[c].Width);
    }

    [TestMethod]
    public void ReadOnlyTraitSpan2D_Slice_SubRegion()
    {
        var span2D = _grid.AsSpan().AsPoint2DSpan2D(W, H);
        var sub = span2D.Slice(1, 1, 2, 2);  // rows 1-2, cols 1-2
        Assert.AreEqual(2, sub.Width);
        Assert.AreEqual(2, sub.Height);
        Assert.AreEqual(1, sub[0, 0].X);
        Assert.AreEqual(1, sub[0, 0].Y);
        Assert.AreEqual(2, sub[1, 1].X);
        Assert.AreEqual(2, sub[1, 1].Y);
    }

    [TestMethod]
    public void ReadOnlyTraitSpan2D_AsSpan_Flattens()
    {
        var span2D = _grid.AsSpan().AsPoint2DSpan2D(W, H);
        var flat = span2D.AsSpan();
        Assert.AreEqual(W * H, flat.Length);
    }

    [TestMethod]
    public void ReadOnlyTraitSpan2D_EnumerateRows_AllRows()
    {
        var span2D = _grid.AsSpan().AsPoint2DSpan2D(W, H);
        int rowCount = 0;
        foreach (var row in span2D.EnumerateRows()) {
            Assert.AreEqual(W, row.Length);
            rowCount++;
        }
        Assert.AreEqual(H, rowCount);
    }

    [TestMethod]
    public void TraitSpan2D_MutableAccess_WritesBack()
    {
        var span2D = _grid.AsSpan().AsPoint2DTraitSpan2D(W, H);
        span2D[0, 0].X = 999;
        span2D[0, 0].Y = 888;
        Assert.AreEqual(999, _grid[0].X);
        Assert.AreEqual(888, _grid[0].Y);
    }

    [TestMethod]
    public void TraitSpan2D_Fill_SetsAll()
    {
        var span2D = _grid.AsSpan().AsSize2DTraitSpan2D(W, H);
        span2D.Fill(new Size2DLayout { Width = 7, Height = 3 });
        for (int i = 0; i < _grid.Length; i++) {
            Assert.AreEqual(7, _grid[i].Width);
            Assert.AreEqual(3, _grid[i].Height);
        }
    }

    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void TraitSpan2D_DimensionMismatch_Throws()
    {
        // W*H != source.Length
        var bad = _grid.AsSpan().AsPoint2DSpan2D(W + 1, H);
    }

    [TestMethod]
    public void TraitSpan2D_ImplicitConversion_ToReadOnly()
    {
        TraitSpan2D<Point2DLayout> mutable = _grid.AsSpan().AsPoint2DTraitSpan2D(W, H);
        ReadOnlyTraitSpan2D<Point2DLayout> readOnly = mutable;
        Assert.AreEqual(mutable.Width, readOnly.Width);
        Assert.AreEqual(mutable.Height, readOnly.Height);
    }
}
```

### Required: Performance Regression Tests

These use BenchmarkDotNet and must be run manually, but the test project must include them.

```csharp
[TestClass]
public class PerformanceRegressionTests {
    /// <summary>
    /// Sanity check: trait layout cast must not allocate.
    /// </summary>
    [TestMethod]
    public void LayoutCast_ZeroAllocation()
    {
        var pixel = new BayerPixel { X = 1, Y = 2 };
        long before = GC.GetAllocatedBytesForCurrentThread();

        for (int i = 0; i < 10000; i++) {
            ref readonly var coord = ref pixel.AsPixelCoordinate();
            _ = coord.X + coord.Y;
        }

        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.AreEqual(before, after, "Layout cast should not allocate!");
    }

    /// <summary>
    /// Sanity check: trait span iteration must not allocate.
    /// </summary>
    [TestMethod]
    public void TraitSpan_Iteration_ZeroAllocation()
    {
        var data = new Rectangle[100];
        long before = GC.GetAllocatedBytesForCurrentThread();

        var span = data.AsSpan().AsPoint2DSpan();
        int sum = 0;
        foreach (ref readonly var pos in span)
            sum += pos.X + pos.Y;

        long after = GC.GetAllocatedBytesForCurrentThread();
        Assert.AreEqual(before, after, "TraitSpan iteration should not allocate!");
    }
}
```

---

## Migration Guide

### From Interface-Based Code

**Before:**

```csharp
interface IProcessor {
    void Process();
}

struct DataTile : IProcessor {
    public int X, Y;
    public void Process() { /* ... */ }
}

void ProcessBatch(IProcessor[] items) {  // BOXING
    foreach (var item in items) {
        item.Process();
    }
}
```

**After:**

```csharp
[Trait]
interface IProcessor {
    void Process();
}

[ImplementsTrait(typeof(IProcessor))]
partial struct DataTile {
    public int X, Y;
    public void Process() { /* ... */ }
}

void ProcessBatch<T>(Span<T> items)  // NO BOXING
    where T : unmanaged, ITrait<IProcessor, T>
{
    foreach (ref var item in items) {
        T.Process_Impl(ref item);
    }
}
```

### From Generic Constraint Code

**Before:**

```csharp
void Process<T>(T[] items) where T : IComparable<T> {
    // Works but limited to single constraint
}
```

**After:**

```csharp
void Process<T>(T[] items) 
    where T : unmanaged, ITrait<IComparable, T>, ITrait<IFormattable, T>
{
    // Multiple trait constraints!
}
```

---

## Future Enhancements

### Future: Method Traits

```csharp
[Trait]
interface IComparable {
    int CompareTo(in Self other);  // Self = implementing type
}
```

### Future: Trait Inheritance

```csharp
[Trait]
interface IColoredPixel : IPixelCoordinate, IRGBPixel {
    // Inherits X, Y, R, G, B
}
```

### Future: Default Implementations

```csharp
[Trait]
interface IPoint2D {
    float X { get; }
    float Y { get; }

    [DefaultImplementation]
    float DistanceFromOrigin() => MathF.Sqrt(X * X + Y * Y);
}
```

### Future: Const Generics for Arrays

```csharp
[Trait]
interface IVector<const int N> {
    float this[int index] { get; }
}
```

---

## Package Metadata

### NuGet Packages

The project ships as **three NuGet packages**:

| Package | Contents | Dependency |
|---------|----------|------------|
| `TraitEmulation.Attributes` | Attribute classes (`[Trait]`, `[ImplementsTrait]`, etc.) | None |
| `TraitEmulation` | Source generator (analyzer DLL) | TraitEmulation.Attributes |
| `TraitEmulation.Runtime` | `TraitSpan<T>`, `ReadOnlyTraitSpan<T>`, 2D variants, ThrowHelper | None |

Consumers reference `TraitEmulation` (which transitively pulls in Attributes) plus `TraitEmulation.Runtime` if they use trait spans.

#### TraitEmulation (Source Generator)

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>preview</LangVersion>
    <PackageId>TraitEmulation</PackageId>
    <Version>1.0.0</Version>
    <Authors>Rand Lee</Authors>
    <Description>
      Rust-like trait emulation for C#. Zero-cost polymorphism over value types
      without boxing. Source generator with compile-time layout verification.
    </Description>
    <PackageTags>traits;polymorphism;codegen;roslyn;source-generator</PackageTags>
    <IncludeBuildOutput>false</IncludeBuildOutput>
    <DevelopmentDependency>true</DevelopmentDependency>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.CodeAnalysis.CSharp" Version="4.8.0" PrivateAssets="all" />
    <PackageReference Include="Microsoft.CodeAnalysis.Analyzers" Version="3.3.4" PrivateAssets="all" />
  </ItemGroup>

  <ItemGroup>
    <None Include="$(OutputPath)\$(AssemblyName).dll" Pack="true"
          PackagePath="analyzers/dotnet/cs" Visible="false" />
  </ItemGroup>
</Project>
```

#### TraitEmulation.Runtime

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>preview</LangVersion>
    <PackageId>TraitEmulation.Runtime</PackageId>
    <Version>1.0.0</Version>
    <Authors>Rand Lee</Authors>
    <Description>
      Runtime types for TraitEmulation: TraitSpan, ReadOnlyTraitSpan,
      and 2D variants for strided trait-view iteration over struct arrays.
    </Description>
    <PackageTags>traits;span;ref-struct;high-performance</PackageTags>
    <AllowUnsafeBlocks>true</AllowUnsafeBlocks>
  </PropertyGroup>
</Project>
```

### README.md

```markdown
# TraitEmulation

Rust-like trait semantics for C#. Zero-cost polymorphism over value types.

## Features

- 🚀 **Zero Boxing** - Value types never allocated on heap
- ⚡ **Zero Copy** - Direct field access via layout casts
- 🔒 **Type Safe** - Compile-time layout verification
- 🎯 **External Types** - Works with System types
- 📦 **Source Generated** - No runtime reflection

## Quick Start

```csharp
using TraitEmulation;

// Define trait
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

## Documentation

See [Design Document](DESIGN.md) for complete specification.
```

---

## Appendix: Complete Working Example

```csharp
// File: Traits.cs
using TraitEmulation;

namespace TraitExamples;

[Trait(GenerateLayout = true)]
public interface IPixelCoordinate {
    int X { get; }
    int Y { get; }
}

[Trait(GenerateLayout = true)]
public interface IRGBPixel {
    byte R { get; }
    byte G { get; }
    byte B { get; }
}

// File: BayerPixel.cs
using System.Runtime.InteropServices;
using TraitEmulation;

namespace TraitExamples;

[ImplementsTrait(typeof(IPixelCoordinate))]
[ImplementsTrait(typeof(IRGBPixel))]
[StructLayout(LayoutKind.Sequential)]
public partial struct BayerPixel {
    public int X, Y;
    public byte R, G, B, A;
}

// File: AssemblyInfo.cs
using TraitEmulation;
using TraitExamples;

[assembly: RegisterTraitImpl(typeof(IPixelCoordinate), typeof(System.Drawing.Point))]

// File: Processing.cs
using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Numerics;

namespace TraitExamples;

public static class Processing {
    public static void ApplyFlatField<T>(
        Span<T> pixels,
        ReadOnlySpan<float> correction,
        int width)
        where T : unmanaged, ITrait<IPixelCoordinate, T>
    {
        for (int i = 0; i < pixels.Length; i++) {
            ref readonly var coord = ref pixels[i].AsPixelCoordinate();
            int offset = coord.Y * width + coord.X;
            // Apply correction...
        }
    }
    
    public static float Distance<T1, T2>(in T1 p1, in T2 p2)
        where T1 : unmanaged, ITrait<IPixelCoordinate, T1>
        where T2 : unmanaged, ITrait<IPixelCoordinate, T2>
    {
        ref readonly var c1 = ref p1.AsPixelCoordinate();
        ref readonly var c2 = ref p2.AsPixelCoordinate();
        
        int dx = c1.X - c2.X;
        int dy = c1.Y - c2.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}

// File: Program.cs
using System;
using TraitExamples;

var bayerPixels = new BayerPixel[100];
var sysPoint = new System.Drawing.Point(50, 50);

// Works with both types - no boxing!
float dist = Processing.Distance(bayerPixels[0], sysPoint);
Console.WriteLine($"Distance: {dist}");
```

---

## Implementation Checklist

### Phase 1: Core Generator + Layout Analysis
- [ ] Attribute definitions (TraitAttribute, ImplementsTraitAttribute, RegisterTraitImplAttribute, ImplStrategy)
- [ ] Trait interface parser
- [ ] Layout struct generator
- [ ] Constraint interface generator
- [ ] Extension methods generator
- [ ] Implementation generator (reinterpret only — no fallback)
- [ ] Alignment-aware field offset calculator
- [ ] Type size resolver (including nested struct recursion)
- [ ] Layout compatibility checker with offset detection
- [ ] Diagnostic reporter (TE0001–TE0009)
- [ ] **Tests: all Generator Diagnostic Tests pass**
- [ ] **Tests: all Runtime Correctness Tests pass (prefix layout)**

### Phase 2: Offset Traits + External Type Support
- [ ] Auto-detection of non-zero base offset for trait fields
- [ ] Offset-aware codegen (AddByteOffset in AsLayout)
- [ ] Assembly-level attribute parser (RegisterTraitImplAttribute)
- [ ] External adapter generator
- [ ] System type registry
- [ ] **Tests: all offset layout tests pass (Rectangle → IPoint2D + ISize2D)**
- [ ] **Tests: external type extension method tests pass**

### Phase 3: Trait Span Types
- [ ] ReadOnlyTraitSpan\<TLayout\> ref struct
- [ ] TraitSpan\<TLayout\> ref struct
- [ ] ReadOnlyTraitSpan2D\<TLayout\> ref struct
- [ ] TraitSpan2D\<TLayout\> ref struct
- [ ] ThrowHelper for bounds checking
- [ ] Generated factory extension methods (AsXxxSpan, AsXxxTraitSpan, 2D variants)
- [ ] Implicit conversion operators (TraitSpan → ReadOnlyTraitSpan, 2D variant)
- [ ] **Tests: all Trait Span Tests pass**
- [ ] **Tests: all Trait Span 2D Tests pass**
- [ ] **Tests: Performance Regression Tests pass (zero allocation)**

### Phase 4: Documentation + Samples
- [ ] XML doc comments on all public APIs
- [ ] README with examples
- [ ] Migration guide
- [ ] API reference
- [ ] Sample project (TraitExample)

### Phase 5: Packaging
- [ ] NuGet package build (separate Attributes + Generator packages)
- [ ] Analyzer packaging
- [ ] Symbol packages
- [ ] Release automation

---

## Summary

This design provides a complete, production-ready solution for zero-cost trait emulation in C#. It solves the struct-interface boxing problem while maintaining type safety and enabling polymorphism over external types like `System.Drawing.Point`.

**Key Benefits:**
- 9x performance improvement over interfaces
- Zero heap allocations
- Works with any unmanaged value type
- Compile-time safety guarantees (no fallbacks — layout mismatch is always a compile error)
- Offset traits: one struct can implement multiple traits at different field positions
- TraitSpan/TraitSpan2D: strided iteration over trait views with Span-like API
- Natural C# syntax via extension methods

**Ready for Claude Code to implement.**
