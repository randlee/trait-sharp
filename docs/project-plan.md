# TraitSharp Project Plan

## Status: Active
**Last Updated:** 2026-02-08
**Branch:** develop
**Current Version:** 0.1.0-alpha
**Test Count:** 229 (83 generator + 146 runtime)

---

## Completed Work (Phases 1–5 + Refactor)

| Phase | Description | Commit | Status |
|-------|-------------|--------|--------|
| Phase 1 | Core generator, layout analysis, diagnostics | `7c4fe02` | ✅ Done |
| Phase 2 | External type support, assembly-level registration | `7eb566d` | ✅ Done |
| Phase 3 | TraitSpan types, factory generators, span tests | `09dc89f` | ✅ Done |
| Phase 4 | XML documentation, README, sample project | `3a6ef8a` | ✅ Done |
| Phase 5 | NuGet packaging for all three packages | `7fd0e09` | ✅ Done |
| Refactor | Rename TraitEmulation → TraitSharp | `ef8a12e` | ✅ Done |

**Current capability:** Property-based traits with zero-copy layout casts, trait inheritance, method traits with default implementations, parameterized defaults, chained dispatch, external type registration, strided span types, NuGet packaging. 229 tests passing.

---

## Phase 6: Test Coverage Hardening

**Goal:** Raise test coverage from ~50% scenario coverage to ~90%+ before adding new features. This prevents regressions as we extend the generator for inheritance and methods.

**Rationale:** Current tests cover happy paths well but have significant gaps in edge cases, error paths, attribute parameter combinations, and boundary conditions.

### Sprint 6.1: Generator Diagnostic Coverage (TE0005–TE0008)

**Effort:** 1–2 days

Add tests for the 4 untested diagnostic codes:

| Test | Description |
|------|-------------|
| `TE0005_ExternalTypeNotFound` | `[RegisterTraitImpl]` references a type that doesn't exist or can't be resolved |
| `TE0006_InvalidTraitMember` | Trait interface contains unsupported members (events, indexers, methods without `[Trait]` support) |
| `TE0007_PropertyMustHaveGetter` | Trait property declared as set-only |
| `TE0008_CircularTraitDependency` | Future-proofing: detect cycles in trait references |

### Sprint 6.2: Attribute Parameter Combinations

**Effort:** 2–3 days

Test all `[Trait]` attribute parameter combinations:

| Test | Description |
|------|-------------|
| `Trait_GenerateLayout_False` | `[Trait(GenerateLayout = false)]` — no layout struct emitted |
| `Trait_GenerateExtensions_False` | `[Trait(GenerateExtensions = false)]` — no extension methods emitted |
| `Trait_GenerateStaticMethods_False` | `[Trait(GenerateStaticMethods = false)]` — no static helpers emitted |
| `Trait_CustomGeneratedNamespace` | `[Trait(GeneratedNamespace = "Custom.Ns")]` — output lands in specified namespace |
| `Trait_AllGenerationDisabled` | All generation flags false — only contract interface emitted |

Test all `ImplStrategy` enum values:

| Test | Description |
|------|-------------|
| `Strategy_Auto_DefaultBehavior` | Explicit `Strategy = ImplStrategy.Auto` produces same output as unspecified |
| `Strategy_Reinterpret_Explicit` | `Strategy = ImplStrategy.Reinterpret` forces reinterpret cast path |
| `Strategy_FieldMapping_PartialMapping` | Map some fields, leave others to convention |
| `Strategy_FieldMapping_InvalidFieldName` | Mapping references non-existent field → diagnostic |
| `Strategy_FieldMapping_TypeMismatch` | Mapping field exists but wrong type → diagnostic |

### Sprint 6.3: Generator Edge Cases

**Effort:** 2–3 days

| Test | Description |
|------|-------------|
| `EmptyTrait_NoProperties` | `[Trait] interface IEmpty { }` — generates empty layout struct |
| `SinglePropertyTrait` | Minimal trait with exactly 1 property |
| `ManyPropertyTrait` | Trait with 10+ properties of mixed types |
| `PropertyTypes_Float` | Trait with `float` properties |
| `PropertyTypes_Double` | Trait with `double` properties |
| `PropertyTypes_Long` | Trait with `long` (8-byte) properties |
| `PropertyTypes_Byte` | Trait with `byte` (1-byte) properties, alignment implications |
| `PropertyTypes_Bool` | Trait with `bool` properties |
| `PropertyTypes_CustomStruct` | Trait with nested struct property types |
| `GlobalNamespace_NoNamespace` | Trait/impl in global namespace (no `namespace` declaration) |
| `DeeplyNestedNamespace` | `A.B.C.D.E` namespace hierarchy |
| `ThreeTraitsOnSameStruct` | Struct implementing 3+ traits simultaneously |
| `OverlappingTraitFields` | Two traits whose fields overlap in the implementing struct |
| `ExternalType_DifferentNamespace` | `[RegisterTraitImpl]` where trait and type are in different namespaces |
| `ExternalType_Generator_Dedicated` | Dedicated generator test for assembly-level registration codegen |

### Sprint 6.4: Runtime Span Boundary Conditions

**Effort:** 2–3 days

**TraitSpan / ReadOnlyTraitSpan:**

| Test | Description |
|------|-------------|
| `Span_SingleElement` | Span over 1-element array |
| `Span_IndexAtLength_Throws` | `span[span.Length]` throws `IndexOutOfRangeException` |
| `Span_SliceZeroLength` | `span.Slice(0, 0)` → empty span |
| `Span_SliceAtEnd` | `span.Slice(span.Length)` → empty span |
| `Span_SliceOutOfBounds_Throws` | `span.Slice(0, span.Length + 1)` throws |
| `Span_CopyTo_ExactLength` | `CopyTo` target has exact matching length |
| `Span_CopyTo_TooSmall_Throws` | Target span too small → `ArgumentException` |
| `Span_ManualStride_Construction` | Construct with explicit stride parameter, verify access |
| `Span_MultipleEnumerations` | Enumerate same span twice, verify identical results |
| `ReadOnlySpan_IsEmpty_ZeroLength` | Manual construction with 0 length → `IsEmpty == true` |

**TraitSpan2D / ReadOnlyTraitSpan2D:**

| Test | Description |
|------|-------------|
| `Span2D_1x1_Grid` | Single element 2D span |
| `Span2D_1xN_SingleRow` | Single row, N columns |
| `Span2D_Nx1_SingleColumn` | N rows, single column |
| `Span2D_Index_0_0` | Access first element `[0,0]` |
| `Span2D_Index_LastElement` | Access `[Height-1, Width-1]` |
| `Span2D_Index_RowOutOfBounds_Throws` | `[Height, 0]` throws |
| `Span2D_Index_ColOutOfBounds_Throws` | `[0, Width]` throws |
| `Span2D_GetRow_First` | `GetRow(0)` returns correct data |
| `Span2D_GetRow_Last` | `GetRow(Height-1)` returns correct data |
| `Span2D_GetRow_OutOfBounds_Throws` | `GetRow(Height)` throws |
| `Span2D_SliceFull` | `Slice(0, 0, Height, Width)` returns equivalent span |
| `Span2D_SliceZeroDimension` | `Slice(0, 0, 0, Width)` → empty |
| `Span2D_SliceOutOfBounds_Throws` | Slice beyond dimensions throws |
| `Span2D_IsEmpty_ZeroHeight` | Height == 0 → `IsEmpty == true` |
| `Span2D_IsEmpty_ZeroWidth` | Width == 0 → `IsEmpty == true` |
| `Span2D_Clear` | Verify `Clear()` zeroes all elements |
| `Span2D_EnumerateRows_Empty` | Enumerate 0-height span → 0 iterations |
| `Span2D_EnumerateRows_SingleRow` | Enumerate 1-row span → 1 iteration |

### Sprint 6.5: ThrowHelper & Performance Regression Tests

**Effort:** 1–2 days

**ThrowHelper direct tests:**

| Test | Description |
|------|-------------|
| `ThrowHelper_IndexOutOfRange_Type` | Verify throws `IndexOutOfRangeException` |
| `ThrowHelper_ArgumentOutOfRange_Type` | Verify throws `ArgumentOutOfRangeException` |
| `ThrowHelper_DestinationTooShort_Type` | Verify throws `ArgumentException` with message |
| `ThrowHelper_InvalidDimensions_Type` | Verify throws `ArgumentException` with message |

**Additional performance regression tests:**

| Test | Description |
|------|-------------|
| `Span_Slice_ZeroAllocation` | Slicing doesn't allocate |
| `Span_Fill_ZeroAllocation` | Fill operation doesn't allocate |
| `Span_Clear_ZeroAllocation` | Clear operation doesn't allocate |
| `Span2D_Operations_ZeroAllocation` | 2D indexing, GetRow, Slice don't allocate |
| `MultiTrait_Access_ZeroAllocation` | Accessing multiple traits on same element doesn't allocate |
| `ToArray_Allocates_ExactSize` | `ToArray()` allocates exactly `Length * sizeof(TLayout)` |

### Sprint 6.6: Layout Analyzer Edge Cases

**Effort:** 1–2 days

| Test | Description |
|------|-------------|
| `Analyzer_ExplicitLayout_WithFieldOffset` | `[StructLayout(Explicit)]` with `[FieldOffset]` attributes |
| `Analyzer_AlignmentPadding_MixedSizes` | Struct with `byte, int, byte` fields — verify padding calculation |
| `Analyzer_NestedStructField` | Trait property type is another struct |
| `Analyzer_FieldOffset_NonZeroBase` | Fields at non-zero offset with explicit layout |

### Phase 6 Summary

| Sprint | Tests Added | Effort |
|--------|------------|--------|
| 6.1 Diagnostics | ~4 | 1–2 days |
| 6.2 Attribute params | ~10 | 2–3 days |
| 6.3 Generator edge cases | ~15 | 2–3 days |
| 6.4 Span boundaries | ~28 | 2–3 days |
| 6.5 ThrowHelper + perf | ~10 | 1–2 days |
| 6.6 Analyzer edge cases | ~4 | 1–2 days |
| **Total** | **~71 new tests** | **~10–15 days** |

**Expected result:** 42 existing + ~71 new = ~113 tests. Coverage rises from ~50% to ~85-90%.

---

## Phase 7: Trait Inheritance

**Goal:** Allow trait interfaces to inherit from other trait interfaces. The generator merges layouts, resolves property hierarchies, and validates composite implementations.

**Prerequisite:** Phase 6 complete (test foundation in place)

### Design

```csharp
// Base traits
[Trait]
interface IPoint {
    float X { get; }
    float Y { get; }
}

[Trait]
interface ISize {
    float Width { get; }
    float Height { get; }
}

// Composite trait — inherits both
[Trait]
interface IRectangle : IPoint, ISize {
    // Inherits: X, Y, Width, Height
    // Layout struct = PointLayout fields + SizeLayout fields
}

// Implementation
[ImplementsTrait(typeof(IRectangle))]
[StructLayout(LayoutKind.Sequential)]
partial struct Rect {
    public float X, Y, Width, Height;
}

// Usage: can use as IPoint, ISize, or IRectangle
public static float Area<T>(in T rect)
    where T : unmanaged, ITrait<IRectangle, T>
{
    ref readonly var r = ref rect.AsRectangle();
    return r.Width * r.Height;
}

// Also works through base trait constraints
public static float DistanceFromOrigin<T>(in T point)
    where T : unmanaged, ITrait<IPoint, T>
{
    ref readonly var p = ref point.AsPoint();
    return MathF.Sqrt(p.X * p.X + p.Y * p.Y);
}
```

**Diamond inheritance example:**

```csharp
[Trait]
interface IIdentifiable {
    int Id { get; }
}

[Trait]
interface ILabeled : IIdentifiable {
    // Inherits: Id
    float Score { get; }
}

[Trait]
interface ITagged : IIdentifiable {
    // Inherits: Id
    int Tag { get; }
}

[Trait]
interface IAnnotatedItem : ILabeled, ITagged {
    // Diamond: both ILabeled and ITagged inherit IIdentifiable
    // Resolution: Id appears once (from IIdentifiable), not duplicated
    // Final layout: Id, Score, Tag
}

[ImplementsTrait(typeof(IAnnotatedItem))]
[StructLayout(LayoutKind.Sequential)]
partial struct AnnotatedItem {
    public int Id;
    public float Score;
    public int Tag;
    // Other fields...
}
```

### Sprint 7.1: TraitModel Inheritance Support

**Effort:** 2–3 days

- Extend `TraitModel` with `BaseTraits` list and `AllProperties` (flattened)
- Implement recursive base trait resolution in `TraitGenerator.GetTraitModel()`
- Detect `[Trait]` interfaces that extend other `[Trait]` interfaces
- Handle diamond inheritance: deduplicate properties by name from shared ancestors
- Add `TE0010_AmbiguousInheritedField` diagnostic for conflicting field names from unrelated base traits (same name, different types)
- Add `TE0011_CircularTraitInheritance` diagnostic for `A : B : A` cycles

### Sprint 7.2: Layout Merging

**Effort:** 2–3 days

- Generate merged layout struct containing all inherited + own properties
- Preserve field order: base trait properties first (in inheritance declaration order), then own properties
- Validate contiguity of merged layout in implementing structs
- Handle padding/alignment when merging layouts with mixed field sizes
- Update `LayoutCompatibilityAnalyzer` to validate merged layouts against implementing types

### Sprint 7.3: Constraint Interface Inheritance

**Effort:** 1–2 days

- Generate `IRectangleTrait<TSelf> : IPointTrait<TSelf>, ISizeTrait<TSelf>`
- Ensure implementing a composite trait satisfies all base trait constraints
- A `Rect` that implements `IRectangle` is automatically usable where `ITrait<IPoint, T>` is required
- Generate extension methods for composite trait that include inherited accessors

### Sprint 7.4: Inheritance Tests

**Effort:** 2–3 days

| Test | Description |
|------|-------------|
| `Inheritance_SingleBase` | `IRectangle : IPoint` — basic single inheritance |
| `Inheritance_MultiBase` | `IRectangle : IPoint, ISize` — multiple base traits |
| `Inheritance_Diamond_DeduplicatesField` | Diamond pattern — `Id` appears once |
| `Inheritance_Diamond_LayoutCorrect` | Diamond — layout struct has correct field count |
| `Inheritance_ThreeLevels` | `A : B : C` — three-level deep hierarchy |
| `Inheritance_BaseConstraint_Satisfied` | Implementing `IRectangle` satisfies `ITrait<IPoint, T>` |
| `Inheritance_MergedLayout_ZeroCopy` | Verify zero-copy access through merged layout |
| `Inheritance_MergedLayout_PointerIdentity` | Verify pointer identity for inherited fields |
| `Inheritance_AmbiguousField_Diagnostic` | Two unrelated bases with same field name, different types → TE0010 |
| `Inheritance_Circular_Diagnostic` | `A : B : A` → TE0011 |
| `Inheritance_EmptyDerived` | `IEmpty : IPoint` — derived adds no new properties |
| `Inheritance_ExternalType_WithInheritance` | `[RegisterTraitImpl]` on external type for inherited trait |
| `Inheritance_SpanFactory_Generated` | `TraitSpan<RectangleLayout>` factory generated for inherited trait |
| `Inheritance_ExtensionMethods_AllLevels` | Extension methods available for base and derived trait |

### Phase 7 Summary

| Sprint | Effort |
|--------|--------|
| 7.1 Model + resolution | 2–3 days |
| 7.2 Layout merging | 2–3 days |
| 7.3 Constraint interfaces | 1–2 days |
| 7.4 Tests | 2–3 days |
| **Total** | **~7–11 days** |

---

## Phase 8: Method Traits ✅ Done

**Commit:** `1c5ff28` (merged via PR #5)

**Goal:** Support trait interfaces with method members, enabling generic algorithms beyond pure data access.

**Prerequisite:** Phase 7 complete (inheritance provides foundation for method inheritance)

**Delivered:** Method parsing, contract interface generation with `static abstract` methods, extension method dispatch, implementation scaffolding with `{Method}_Impl` convention, method inheritance integration, overload disambiguation, Self-type resolution. Runtime integration tests verify dispatch for single-method, multi-method, parameterized methods, void returns, and generic algorithm scenarios.

### Design

```csharp
[Trait]
interface IPoint2D {
    float X { get; }
    float Y { get; }

    // Method trait members
    float DistanceTo(in Self other);
    Self Translate(float dx, float dy);
}

// Implementation must provide method bodies
[ImplementsTrait(typeof(IPoint2D))]
[StructLayout(LayoutKind.Sequential)]
partial struct Vector2 {
    public float X, Y;

    // Generated contract requires these:
    public static float DistanceTo_Impl(in Vector2 self, in Vector2 other)
    {
        float dx = self.X - other.X;
        float dy = self.Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    public static Vector2 Translate_Impl(in Vector2 self, float dx, float dy)
    {
        return new Vector2 { X = self.X + dx, Y = self.Y + dy };
    }
}

// Generic algorithm using method traits
public static float TotalPathLength<T>(ReadOnlySpan<T> points)
    where T : unmanaged, ITrait<IPoint2D, T>
{
    float total = 0;
    for (int i = 1; i < points.Length; i++)
        total += points[i - 1].DistanceTo(in points[i]);
    return total;
}
```

### Self Type Resolution

The `Self` keyword in method signatures resolves to the implementing type:

| Trait Declaration | Generated Contract | Implementation |
|---|---|---|
| `float DistanceTo(in Self other)` | `static abstract float DistanceTo_Impl(in TSelf self, in TSelf other)` | `static float DistanceTo_Impl(in Vector2 self, in Vector2 other)` |
| `Self Translate(float dx, float dy)` | `static abstract TSelf Translate_Impl(in TSelf self, float dx, float dy)` | `static Vector2 Translate_Impl(in Vector2 self, float dx, float dy)` |
| `bool Equals(in Self other)` | `static abstract bool Equals_Impl(in TSelf self, in TSelf other)` | `static bool Equals_Impl(in Vector2 self, in Vector2 other)` |

### Sprint 8.1: Method Parsing

**Effort:** 2–3 days

- Extend `TraitModel` with `Methods` list (`TraitMethod` class)
- Parse method signatures from trait interfaces in `TraitGenerator`
- Handle `Self` type parameter replacement (replace with `TSelf` in contract, concrete type in impl)
- Parse method return types, parameter types, parameter modifiers (`in`, `ref`, `out`)
- Validate: methods must not have bodies in the trait interface (those become default impls in Phase 9)
- Add `TE0012_InvalidMethodSignature` diagnostic for unsupported method patterns

### Sprint 8.2: Contract Interface Method Generation

**Effort:** 2–3 days

- Extend `ConstraintInterfaceGenerator` to emit `static abstract` method declarations
- Method naming convention: `{MethodName}_Impl`
- First parameter is always `in TSelf self` (implicit receiver)
- `Self` in parameters → `TSelf`, `Self` in return → `TSelf`
- Handle overloaded methods (append index suffix: `Compare_Impl`, `Compare_Impl_1`)

### Sprint 8.3: Extension Method Generation for Methods

**Effort:** 1–2 days

- Extend `ExtensionMethodsGenerator` to wrap method trait members
- Generate ergonomic extension methods: `point.DistanceTo(in other)` delegates to `T.DistanceTo_Impl(in self, in other)`
- Handle all parameter modifiers correctly

### Sprint 8.4: Implementation Scaffolding

**Effort:** 1–2 days

- Extend `ImplementationGenerator` to emit method implementation stubs
- The user writes the method body; generator emits the partial class with the correct signature
- Verify method signatures match the contract at compile time
- External types: method traits on external types require a static adapter class

### Sprint 8.5: Method Inheritance Integration

**Effort:** 2–3 days

- Methods inherited from base traits flow through the same resolution as properties
- Composite trait inherits all methods from base traits
- Diamond method deduplication (same rules as properties)
- Add `TE0013_AmbiguousInheritedMethod` diagnostic

### Sprint 8.6: Method Trait Tests

**Effort:** 3–4 days

| Test | Description |
|------|-------------|
| `MethodTrait_SingleMethod` | Trait with one method, verify contract generated |
| `MethodTrait_MultipleMethod` | Trait with 3+ methods |
| `MethodTrait_SelfParameter` | `in Self other` parameter resolves correctly |
| `MethodTrait_SelfReturnType` | `Self` return type resolves to implementing type |
| `MethodTrait_MixedPropertiesAndMethods` | Trait with both properties and methods |
| `MethodTrait_ExtensionMethod_Dispatches` | Extension method correctly calls `T.Method_Impl(...)` |
| `MethodTrait_GenericAlgorithm` | End-to-end: write generic function using method trait |
| `MethodTrait_InheritedMethod` | Method inherited from base trait |
| `MethodTrait_OverloadedMethods` | Two methods with same name, different params |
| `MethodTrait_RefParameters` | Method with `ref` parameter |
| `MethodTrait_MultipleReturnTypes` | Methods returning various types (void, int, struct, Self) |
| `MethodTrait_ExternalType_Adapter` | Method trait on external type via adapter |
| `MethodTrait_InvalidSignature_Diagnostic` | Unsupported method pattern → TE0012 |
| `MethodTrait_VoidReturn` | `void DoSomething(in Self self)` works |
| `MethodTrait_ZeroAllocation` | Method dispatch doesn't allocate |

### Phase 8 Summary

| Sprint | Effort |
|--------|--------|
| 8.1 Method parsing | 2–3 days |
| 8.2 Contract generation | 2–3 days |
| 8.3 Extension methods | 1–2 days |
| 8.4 Implementation scaffolding | 1–2 days |
| 8.5 Inheritance integration | 2–3 days |
| 8.6 Tests | 3–4 days |
| **Total** | **~11–17 days** |

---

## Phase 9: Default Method Implementations ✅ Done

**Commit:** `7bda269` (merged via PR #6)

**Goal:** Allow trait methods to have default bodies that are auto-generated for implementors that don't provide their own `{Method}_Impl`. Default methods access trait properties and call other trait methods via the same static abstract dispatch.

**Prerequisite:** Phase 8 complete (method traits)

**Delivered:** `HasDefaultBody` and `DefaultBodySyntax` extraction, `DefaultBodyRewriter` (Roslyn `CSharpSyntaxRewriter`) that transforms property accesses → `T.Get{P}_Impl(in self)` and method calls → `T.{M}_Impl(in self, ...)`, override detection via `HasUserOverride()`, expression-body and block-body support. Runtime integration tests verify default dispatch, selective override, all-override, default-only traits, expression-body defaults, and generic algorithm scenarios. Consumer sample updated with IShape/Rectangle/Circle/Square examples.

### Design

**Key Insight:** C# interfaces parsed by the source generator can have default interface methods (DIM) with bodies in .NET 8+. However, the generator can't *execute* those bodies — it reads the *syntax tree*. So the strategy is:

1. The user writes a method body on the trait interface (C# DIM syntax)
2. The generator **extracts the syntax body** from the interface method
3. When an implementing type does NOT define `{Method}_Impl`, the generator **emits the default body** as a static method on the implementation's partial struct
4. The emitted default body rewrites property accesses → `TSelf.Get{Prop}_Impl(in self)` and method calls → `TSelf.{Method}_Impl(in self, ...)` via a syntax rewriter

**No new attribute needed.** A method with a body = default impl. A method without a body = required impl (Phase 8 behavior).

```csharp
// Trait with mix of required and default methods
[Trait(GenerateLayout = true)]
public partial interface IShape
{
    float Width { get; }
    float Height { get; }

    // Required method — implementor MUST provide Area_Impl
    float Area();

    // Default method — implementor MAY override Perimeter_Impl
    float Perimeter() => 2 * (Width + Height);

    // Default method using another method
    bool IsSquare() => MathF.Abs(Width - Height) < 0.001f;

    // Default method with parameter
    float ScaledArea(float factor) => Area() * factor;
}

// Implementation: provides required Area_Impl, inherits default Perimeter_Impl
[ImplementsTrait(typeof(IShape))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Rectangle
{
    public float Width, Height;

    // Required: user provides this
    public static float Area_Impl(in Rectangle self)
        => self.Width * self.Height;

    // Perimeter_Impl, IsSquare_Impl, ScaledArea_Impl are AUTO-GENERATED
    // by the source generator using the default bodies from IShape
}

// Implementation: overrides the default Perimeter_Impl
[ImplementsTrait(typeof(IShape))]
[StructLayout(LayoutKind.Sequential)]
public partial struct Circle
{
    public float Width, Height; // Width = Height = diameter

    public static float Area_Impl(in Circle self)
        => MathF.PI * (self.Width / 2) * (self.Width / 2);

    // Override: user provides custom Perimeter_Impl
    public static float Perimeter_Impl(in Circle self)
        => MathF.PI * self.Width;
}

// Generic algorithm works with both
public static float TotalArea<T>(ReadOnlySpan<T> shapes) where T : unmanaged, IShapeTrait<T>
{
    float total = 0;
    for (int i = 0; i < shapes.Length; i++)
    {
        ref readonly var s = ref shapes[i];
        total += IShape.Area(in s);       // Required method — each type has its own
    }
    return total;
}
```

### Default Body Rewriting Rules

When emitting a default implementation for `Rectangle`, the generator rewrites the default body:

| Trait syntax | Generated implementation |
|---|---|
| `Width` (property access) | `T.GetWidth_Impl(in self)` |
| `Height` (property access) | `T.GetHeight_Impl(in self)` |
| `Area()` (method call) | `T.Area_Impl(in self)` |
| `Perimeter()` (method call) | `T.Perimeter_Impl(in self)` |
| `ScaledArea(factor)` (method call with args) | `T.ScaledArea_Impl(in self, factor)` |
| `other.Width` (param property) | `T.GetWidth_Impl(in other)` |

### How Override Detection Works

For each trait method that has a default body, the generator checks whether the implementing type already defines a matching `{Method}_Impl` static method:

1. **Scan implementing type's members** for `static {ReturnType} {Method}_Impl(in {TypeName} self, ...)`
2. If found → **skip** (user override takes precedence)
3. If not found → **emit** the default body as `public static {ReturnType} {Method}_Impl(in {TypeName} self, ...)` in the partial struct

### Sprint 9.1: TraitMethod Model — HasDefaultBody + DefaultBodySyntax

**Effort:** 1 day

- Add `HasDefaultBody` bool to `TraitMethod`
- Add `DefaultBodySyntax` string property to store the raw method body text
- In `ParseTraitMethod` (syntax pipeline): Check if the `IMethodSymbol` has a `DeclaringSyntaxReferences` with a method body. If the method's syntax node is a `MethodDeclarationSyntax` with `Body` or `ExpressionBody`, extract the body text
- In `ParseTraitMethod` (symbol pipeline / `BuildTraitModelFromSymbol`): Same check

### Sprint 9.2: Default Body Syntax Rewriter

**Effort:** 2 days

Create `DefaultBodyRewriter` utility class that:
- Takes a default body string, the trait model, and the implementing type name
- Rewrites property accesses: `{PropName}` → `{TypeName}.Get{PropName}_Impl(in self)` or `TSelf.Get{PropName}_Impl(in self)` depending on context
- Rewrites method calls: `{MethodName}(args)` → `{TypeName}.{MethodName}_Impl(in self, args)`
- Rewrites `self`-typed parameter accesses: `other.Width` → `{TypeName}.GetWidth_Impl(in other)`
- Uses Roslyn `SyntaxRewriter` or regex-based text rewriting on the extracted body

Implementation approach: **Syntax-based rewriting using Roslyn CSharpSyntaxTree.ParseText + SyntaxRewriter**:
- Parse the body as a method body statement
- Walk the syntax tree, replacing `IdentifierNameSyntax` nodes matching trait property names with the static dispatch call
- Walk `InvocationExpressionSyntax` nodes matching trait method names

### Sprint 9.3: Implementation Generator — Emit Default Bodies

**Effort:** 1–2 days

- In `ImplementationGenerator.Generate()`, after emitting property accessors:
  - For each trait method with `HasDefaultBody`:
    - Check if the implementing `INamedTypeSymbol` already defines a matching static method
    - If NOT found: rewrite the default body and emit it as a static method
- Override detection: scan `impl.TypeSymbol.GetMembers()` for `IMethodSymbol` with matching name pattern

### Sprint 9.4: Contract Interface — Mark Optional Methods

**Effort:** 0.5 days

- Default methods still appear in the contract interface as `static abstract` (no change)
- The implementing type always has the method — either user-provided or generator-emitted
- No changes to `ConstraintInterfaceGenerator` needed

### Sprint 9.5: Generator Tests for Default Methods

**Effort:** 2 days

| Test | Description |
|------|-------------|
| `Default_VoidMethod_GeneratesDefaultBody` | Trait with `void Log() => Console.WriteLine("logged");` — default body emitted |
| `Default_ReturningMethod_GeneratesDefaultBody` | Trait with `int Double() => Value * 2;` — return expression emitted |
| `Default_PropertyAccess_Rewritten` | Default body accessing `Width` rewrites to `T.GetWidth_Impl(in self)` |
| `Default_MethodCall_Rewritten` | Default body calling `Area()` rewrites to `T.Area_Impl(in self)` |
| `Default_MethodCallWithArgs_Rewritten` | Default body calling `Scale(2.0f)` rewrites correctly |
| `Default_UserOverride_Skipped` | When impl provides `Method_Impl`, no default emitted |
| `Default_MixedRequiredAndDefault` | Trait with 1 required + 1 default → only default is auto-generated |
| `Default_AllDefaults_NoUserCode` | All methods have defaults → all auto-generated |
| `Default_InheritedDefault_Propagated` | Base trait default method flows to derived trait impl |
| `Default_SelfParam_Rewritten` | Default body with Self-typed param: `other.Width` rewrites correctly |
| `Default_ExpressionBody_Handled` | `float Area() => Width * Height;` expression body extracted |
| `Default_BlockBody_Handled` | `float Area() { return Width * Height; }` block body extracted |
| `Default_ChainedMethodCalls` | Default body calling another default method |
| `Default_NoDefaultBody_RemainsRequired` | Method without body → still required (TE0012-like enforcement) |

### Sprint 9.6: Consumer Example — TraitExample Sample Updates

**Effort:** 1 day

Add comprehensive consumer examples to `samples/TraitExample/` that demonstrate ALL Phase 9 features in actual working code, including corner cases:

**New trait definitions:**

```csharp
// IShape: mix of required and default methods, property access in defaults
[Trait(GenerateLayout = true)]
public partial interface IShape
{
    float Width { get; }
    float Height { get; }
    float Area();                                    // Required
    float Perimeter() => 2 * (Width + Height);       // Default (property access)
    bool IsSquare() => MathF.Abs(Width - Height) < 0.001f; // Default (property comparison)
    float ScaledArea(float factor) => Area() * factor; // Default (calls required method)
}
```

**New implementing structs:**

```csharp
// Rectangle: provides required Area_Impl, inherits ALL defaults
[ImplementsTrait(typeof(IShape))]
public partial struct Rectangle { float Width, Height; ... }

// Circle: provides required Area_Impl + overrides Perimeter_Impl
[ImplementsTrait(typeof(IShape))]
public partial struct Circle { float Width, Height; ... }

// Square: provides required Area_Impl + overrides IsSquare (always true)
[ImplementsTrait(typeof(IShape))]
public partial struct Square { float Width, Height; ... }
```

**Integration tests in Program.cs covering corner cases:**

| Test | Corner case |
|------|------------|
| Rectangle.Area() | Required method — user-provided |
| Rectangle.Perimeter() | Default method accessing properties |
| Rectangle.IsSquare() for non-square | Default method with float comparison |
| Rectangle.IsSquare() for 5x5 | Default method returning true |
| Rectangle.ScaledArea(2) | Default method calling required method |
| Circle.Area() | Required method — user-provided (π*r²) |
| Circle.Perimeter() | User OVERRIDE of default (π*d) |
| Circle.IsSquare() | Default inherited (Width==Height for circle) |
| Square.IsSquare() | User OVERRIDE always returns true |
| Generic dispatch via IShape constraint | All methods dispatch through trait constraint |
| LabeledItem still works | Existing Phase 8 method trait unchanged |
| DataPoint/ICoordinate still works | Existing property traits unchanged |

### Phase 9 Summary

| Sprint | Effort |
|--------|--------|
| 9.1 Model: HasDefaultBody + extraction | 1 day |
| 9.2 Default body syntax rewriter | 2 days |
| 9.3 Implementation generator: emit defaults | 1–2 days |
| 9.4 Contract interface: no change needed | 0.5 days |
| 9.5 Generator tests (14 new tests) | 2 days |
| 9.6 Consumer example + corner case integration tests | 1 day |
| **Total** | **~6–8 days** |

---

## Phase 10: Parameterized Defaults & Chained Dispatch ✅ Done

**Commit:** `4a2b8f0` (merged via PR #7)

**Goal:** Verify and test parameterized default methods (defaults with method parameters) and chained default dispatch (default method calling another default method or required method).

**Delivered:** No generator code changes required — the Phase 9 `DefaultBodyRewriter` already handles parameter forwarding and chained calls correctly. Phase 10 is pure verification via comprehensive runtime integration tests (28 new tests).

**Key test scenarios:**
- `IScalable`: default `ScaledArea(float factor) => Area() * factor` — parameter + call to required method
- `IChainable`: chain `Quadrupled() => Doubled() * 2` → `Doubled() => BaseValue() * 2` — default calling default calling required
- `IFormattable`: defaults combining property access + single/multi parameters
- `IComputable`: parameter forwarding through chained dispatch — `ComputeDouble(int x) => Compute(x) * 2`
- Override detection: `ScalableRectOverride`, `ChainItemOverrideMiddle`, `FormattableItemOverride`
- Generic algorithms: `ParameterizedDefaultAlgorithms` class with trait-constrained generics

---

## Phase 11: Inherited Method Dispatch & Cross-Assembly Traits

**Goal:** Verify inherited method dispatch (trait inheriting methods from base traits) works end-to-end at runtime, and validate cross-assembly trait patterns (traits defined in one assembly, implemented in another).

**Status:** In Progress

**Prerequisite:** Phases 7–10 complete

### Background

The generator infrastructure for inherited methods already exists:
- `BuildAllMethods` (TraitGenerator.cs) merges inherited methods depth-first with diamond deduplication
- `trait.Methods` is replaced with `AllMethods` when base traits exist, so all downstream generators handle inherited methods automatically
- However, **no runtime tests exist** for traits that inherit methods (all Phase 7 inheritance tests are property-only)

For cross-assembly traits:
- `RegisterTraitImplAttribute` enables assembly-level registration for external types
- Current tests only cover property-based cross-assembly traits
- No cross-assembly method trait tests exist

### Sprint 11.1: Inherited Method Trait Types

**Effort:** 1 day

Define test types that combine inheritance + methods:

| Type | Description |
|------|-------------|
| `IDescribable` | Base trait: `int Id { get; }` + method `string Describe()` |
| `IDetailedDescribable : IDescribable` | Derived: adds `string Category { get; }` + method `string DetailedDescribe()` with default body calling `Describe()` |
| `SimpleItem` | Implements `IDescribable` only |
| `DetailedItem` | Implements `IDetailedDescribable` — inherits `Describe()` from base |
| `INameable` | Base trait: `string Name()` method only (no properties except layout field) |
| `IGreetable : INameable` | Derived: adds `string Greet()` default calling `Name()` |
| `NamedEntity` | Implements `IGreetable` — tests inherited method + default calling inherited method |
| `IValueProvider` | Base: `int Value { get; }` + `int GetValue()` required |
| `IDoubler : IValueProvider` | Derived: adds `int DoubleValue()` default → `GetValue() * 2` |
| `IDiamond : ILeftTrait, IRightTrait` | Diamond: both sides inherit `IValueProvider` — method deduplication |

### Sprint 11.2: Inherited Method Runtime Tests

**Effort:** 1 day

| Test | Description |
|------|-------------|
| `InheritedMethod_BaseTraitDispatch` | Call `Describe()` through base trait constraint on `SimpleItem` |
| `InheritedMethod_DerivedTraitIncludesBaseMethod` | Call inherited `Describe()` through derived constraint on `DetailedItem` |
| `InheritedMethod_DefaultCallingInherited` | `DetailedDescribe()` default calls inherited `Describe()` |
| `InheritedMethod_PureMethodInheritance` | `IGreetable` inherits `Name()` from `INameable` |
| `InheritedMethod_DefaultCallingInheritedMethod` | `Greet()` default calls inherited `Name()` |
| `InheritedMethod_GenericAlgorithm_BaseConstraint` | Generic `<T : IDescribableTrait<T>>` works with both base and derived types |
| `InheritedMethod_GenericAlgorithm_DerivedConstraint` | Generic `<T : IDetailedDescribableTrait<T>>` dispatches inherited + own methods |
| `InheritedMethod_DiamondDeduplication` | Diamond method inherited once, not duplicated |
| `InheritedMethod_OverrideInheritedDefault` | Derived implementer overrides a default inherited from base |
| `InheritedMethod_ChainedInheritance` | Three-level chain: `ITop : IMid : IBase` with methods at each level |

### Sprint 11.3: Cross-Assembly Trait Verification

**Effort:** 1 day

Expand the existing `RegisterTraitImpl` + `ExternalPoint` pattern to cover method traits:

| Type | Description |
|------|-------------|
| `IExternalLabeled` | Trait with property + method: `int Code { get; }` + `string Label()` |
| `ExternalWidget` | Non-partial struct (simulates external type) |
| `ExternalWidgetAdapter` | Static class providing `Label_Impl` for `ExternalWidget` |
| Assembly-level registration | `[assembly: RegisterTraitImpl(typeof(IExternalLabeled), typeof(ExternalWidget))]` |

| Test | Description |
|------|-------------|
| `CrossAssembly_ExternalType_MethodDispatch` | Method trait on external type dispatches correctly |
| `CrossAssembly_ExternalType_GenericAlgorithm` | External type works in generic trait-constrained algorithm |
| `CrossAssembly_ExternalType_DefaultMethod` | External type with default method uses default body |
| `CrossAssembly_PropertyAndMethod_Combined` | External type with both property access and method dispatch |

### Sprint 11.4: Consumer Sample Updates

**Effort:** 0.5 days

Add inherited method examples to `samples/TraitExample/`:
- `IDescribable` → `IDetailedShape : IShape, IDescribable` demonstrating multi-inheritance with methods
- Program.cs integration tests verifying inherited method dispatch at runtime

### Phase 11 Summary

| Sprint | Tests Added | Effort |
|--------|------------|--------|
| 11.1 Inherited method types | — | 1 day |
| 11.2 Inherited method tests | ~10 | 1 day |
| 11.3 Cross-assembly tests | ~4 | 1 day |
| 11.4 Consumer sample | — | 0.5 days |
| **Total** | **~14 new tests** | **~3.5 days** |

---

## Timeline Overview

```
Phase 6: Test Coverage Hardening       ✅ Done   (3c93204)  71 new tests
Phase 7: Trait Inheritance              ✅ Done   (5717da1)  14 new tests
Phase 8: Method Traits                  ✅ Done   (1c5ff28)  12 new tests
Phase 9: Default Implementations        ✅ Done   (7bda269)  14 new tests
Phase 10: Parameterized Defaults        ✅ Done   (4a2b8f0)  28 new tests
Phase 11: Inherited Methods + X-Asm     In Progress           ~14 new tests
                                        ─────────
Total Phases 6–11:                      229 passing → ~243    153+ tests
```

---

## New Diagnostic Codes

| Code | Phase | Description |
|------|-------|-------------|
| TE0010 | 7 | Ambiguous inherited field: two unrelated base traits define same-named field with different types |
| TE0011 | 7 | Circular trait inheritance detected |
| TE0012 | 8 | Invalid method signature in trait interface |
| TE0013 | 9 | Default body rewrite failure (syntax could not be parsed) |

---

## Risk Register

| Risk | Impact | Mitigation |
|------|--------|------------|
| Diamond inheritance layout conflicts | High | Deduplicate by shared ancestor; diagnostic for genuine conflicts |
| Self type erasure in generic contexts | Medium | Use `TSelf` generic parameter; static dispatch only |
| Method overload resolution complexity | Medium | Simple suffix-based disambiguation; limit to parameter count differences |
| .NET version compatibility for static abstract members | Medium | Target net7.0+ for method traits; property traits remain netstandard2.0 |
| Performance regression from inheritance resolution | Low | Cache resolved hierarchies; benchmark generator speed |
| Default body syntax rewriting correctness | High | Roslyn syntax rewriter with comprehensive test coverage; fallback to string substitution |
| Override detection false positives | Medium | Strict signature matching: name + parameter count + types |
| Expression vs block body handling | Low | Support both forms; normalize to block body internally |

---

## Open Design Questions

1. **Mutable methods:** Should traits support `ref Self` (mutable self)? Current design uses `in Self` (readonly).
2. **Generic method parameters:** Can trait methods have their own generic parameters beyond `Self`?
3. ~~**Partial default overrides:** Can an implementation override some default methods but inherit others?~~ → **Resolved in Phase 9:** Yes. Each default method is independently overridable.
4. ~~**Cross-assembly trait inheritance:** Does trait inheritance work when base trait is in a different assembly?~~ → **Addressed in Phase 11:** Validated via `RegisterTraitImplAttribute` with method traits. Same-project external type simulation covers the pattern; true cross-assembly requires a separate library project (future work).
