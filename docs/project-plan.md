# TraitSharp Project Plan

## Status: Active
**Last Updated:** 2026-02-08
**Branch:** develop
**Current Version:** 0.1.0-alpha

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

**Current capability:** Property-based traits with zero-copy layout casts, external type registration, strided span types, NuGet packaging. 42 tests passing.

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

## Phase 8: Method Traits

**Goal:** Support trait interfaces with method members, enabling generic algorithms beyond pure data access.

**Prerequisite:** Phase 7 complete (inheritance provides foundation for method inheritance)

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

## Phase 9: Default Implementations (Future)

**Goal:** Allow trait methods to provide default bodies that work for any implementor.

**Prerequisite:** Phase 8 complete (method traits)

```csharp
[Trait]
interface IPoint2D {
    float X { get; }
    float Y { get; }

    [DefaultImpl]
    float Magnitude() => MathF.Sqrt(X * X + Y * Y);

    [DefaultImpl]
    float DistanceTo(in Self other) {
        float dx = X - other.X;
        float dy = Y - other.Y;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
```

**Estimated effort:** 5–8 days (design + implementation + tests)

*Detailed sprint breakdown to be added after Phase 8 design is validated.*

---

## Timeline Overview

```
Phase 6: Test Coverage Hardening       ~10–15 days    (71 new tests)
Phase 7: Trait Inheritance              ~7–11 days     (14 new tests)
Phase 8: Method Traits                  ~11–17 days    (15 new tests)
Phase 9: Default Implementations        ~5–8 days      (future)
                                        ───────────
Total Phases 6–8:                       ~28–43 days    (100 new tests)
```

---

## New Diagnostic Codes

| Code | Phase | Description |
|------|-------|-------------|
| TE0010 | 7 | Ambiguous inherited field: two unrelated base traits define same-named field with different types |
| TE0011 | 7 | Circular trait inheritance detected |
| TE0012 | 8 | Invalid method signature in trait interface |
| TE0013 | 8 | Ambiguous inherited method from multiple base traits |

---

## Risk Register

| Risk | Impact | Mitigation |
|------|--------|------------|
| Diamond inheritance layout conflicts | High | Deduplicate by shared ancestor; diagnostic for genuine conflicts |
| Self type erasure in generic contexts | Medium | Use `TSelf` generic parameter; static dispatch only |
| Method overload resolution complexity | Medium | Simple suffix-based disambiguation; limit to parameter count differences |
| .NET version compatibility for static abstract members | Medium | Target net7.0+ for method traits; property traits remain netstandard2.0 |
| Performance regression from inheritance resolution | Low | Cache resolved hierarchies; benchmark generator speed |

---

## Open Design Questions

1. **Mutable methods:** Should traits support `ref Self` (mutable self)? Current design uses `in Self` (readonly).
2. **Generic method parameters:** Can trait methods have their own generic parameters beyond `Self`?
3. **Partial default overrides:** Can an implementation override some default methods but inherit others?
4. **Cross-assembly trait inheritance:** Does trait inheritance work when base trait is in a different assembly?
