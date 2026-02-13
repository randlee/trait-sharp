---
title: "Proposal: Generic Method Parameters"
status: Deferred
priority: Nice-to-Have
complexity: ~3–4 days
risk: Medium-High
created-date: 2026-02-12
---

# Proposal: Generic Method Parameters

## Summary

Allow trait methods to have their own generic type parameters beyond `Self`, e.g., `T Convert<T>() where T : unmanaged` or `void Process<TArg>(TArg arg)`.

## Motivation

Currently, trait methods that accept `TypeParameters.Length > 0` are hard-rejected in `ParseTraitMethod()` with a `return null` and TE0012 diagnostic. This prevents patterns like:

```csharp
[Trait]
interface ISerializable
{
    // Generic conversion method
    T ConvertTo<T>() where T : unmanaged;

    // Generic processing with arbitrary argument type
    void Accept<TVisitor>(TVisitor visitor) where TVisitor : IVisitor;
}
```

These patterns are common in serialization, visitor, and adapter scenarios.

### Example

```csharp
[Trait]
interface IConvertible
{
    int Value { get; }

    // Method with its own generic type parameter
    T As<T>() where T : unmanaged;

    // Default with generic parameter
    string Format<TFormatter>(TFormatter fmt) where TFormatter : IFormatter
        => fmt.Format(Value);
}

[ImplementsTrait(typeof(IConvertible))]
partial struct Measurement
{
    public int Value;

    public static T As_Impl<T>(in Measurement self) where T : unmanaged
        => Unsafe.As<int, T>(ref Unsafe.AsRef(in self.Value));
}
```

## Design

### Model Changes

Add to `TraitMethod`:
```csharp
public List<string> TypeParameterNames { get; set; } = new();  // ["T", "TArg"]
public List<string> TypeConstraints { get; set; } = new();      // ["T : unmanaged"]
```

### Parsing Changes

In `TraitGenerator.ParseTraitMethod()`:
1. Remove the hard rejection: `if (methodSymbol.TypeParameters.Length > 0) return null`
2. Capture type parameters from `methodSymbol.TypeParameters`
3. Extract constraints from `ITypeParameterSymbol.ConstraintTypes`, `HasUnmanagedTypeConstraint`, `HasValueTypeConstraint`, `HasReferenceTypeConstraint`, `HasConstructorConstraint`

### Code Generation

| Generator | Current Output | With Generic Params |
|-----------|---------------|-------------------|
| Contract | `static abstract void M_Impl(in TSelf self);` | `static abstract T M_Impl<T>(in TSelf self) where T : unmanaged;` |
| Extension | `M<T>(this ref T self) { T.M_Impl(in self); }` | `M<T, TResult>(this ref T self) where TResult : unmanaged { return T.M_Impl<TResult>(in self); }` |
| Implementation | `public static void M_Impl(in Type self)` | `public static T M_Impl<T>(in Type self) where T : unmanaged` |

### DefaultBodyRewriter Impact

The rewriter must handle `GenericNameSyntax` in addition to `IdentifierNameSyntax`:

```csharp
// Trait default body:
string Format<TFormatter>(TFormatter fmt) => fmt.Format(Value);

// Must rewrite to:
public static string Format_Impl<TFormatter>(in MyStruct self, TFormatter fmt)
    where TFormatter : IFormatter
    => fmt.Format(TSelf.GetValue_Impl(in self));
```

Key challenges:
- `GenericNameSyntax` nodes (e.g., `Convert<int>()`) need a new visitor override
- Must distinguish method's own type parameter `T` (leave alone) from trait property/method references (rewrite)
- Type arguments must be forwarded: `T.Convert_Impl<int>(in self)` not `T.Convert_Impl(in self)`

### Override Detection

`HasUserOverride()` must match type parameter count:
```csharp
// Current: checks ms.Name == implMethodName
// Required: also check ms.TypeParameters.Length == method.TypeParameterNames.Count
```

### Overload Disambiguation

Current suffix logic only considers parameter count. Must also include type parameter count:
- `void M<T>()` → `M_Impl` (single type param, no suffix)
- `void M<T, U>()` → `M_Impl_1` or `M__2_Impl` (two type params)
- `void M()` and `void M<T>()` → distinct suffixes

### Cross-Assembly Metadata

Current `TraitDefaultBodyAttribute` stores only body syntax. Type parameter constraints must also survive compilation:
- Option A: Extend `TraitDefaultBodyAttribute` with a `TypeConstraints` string parameter
- Option B: Create `[TraitMethodTypeParameters("MethodName", "T : unmanaged, U : class")]` companion attribute

Option A is simpler; Option B is cleaner for complex signatures.

### Supported Constraints (Initial Scope)

To limit complexity, initially support only:
- `unmanaged`
- `class`
- `struct`
- `new()`
- Single base type or interface constraint

Reject with diagnostic for:
- Intersection constraints (`T : class, IDisposable, new()` — multiple)
- `notnull`
- Tuple constraints
- `class?` / nullable reference type constraints

## Impact Assessment

| Component | Changes Required | Risk |
|-----------|-----------------|------|
| `TraitModel.TraitMethod` | Add `TypeParameterNames` + `TypeConstraints` lists | Low |
| `TraitGenerator.ParseTraitMethod()` | Remove hard rejection, capture type params | Medium |
| `ConstraintInterfaceGenerator` | Emit generic params + where clauses | Low |
| `ExtensionMethodsGenerator` | Nested generics, forward type args | Low-Medium |
| `ImplementationGenerator` | Emit generic params on stubs | Low |
| `DefaultBodyRewriter` | Handle `GenericNameSyntax`, preserve type args | **High** |
| Override detection | Match type parameter count | Medium |
| Cross-assembly metadata | Encode constraints in attributes | Medium |
| Overload disambiguation | Include type param count in suffix | Medium |

## Risks

1. **DefaultBodyRewriter (High):** The Roslyn `SyntaxRewriter` only handles `IdentifierNameSyntax` for method calls. Generic calls use `GenericNameSyntax` — a new visitor override is required. The rewriter must also distinguish between a method's own type parameter `T` (leave alone) and a trait property/method reference (rewrite). This is the most complex change.

2. **Constraint complexity (Medium):** C# supports complex constraints (intersection types, `notnull`, `unmanaged`, `class?` in C# 11+). Limiting initial scope to simple constraints mitigates this but reduces usefulness.

3. **Cross-assembly constraint encoding (Medium):** Type parameter constraints must survive compilation boundaries. The current metadata pipeline only stores body syntax — needs extension.

4. **Overload disambiguation (Medium):** `void M<T>()` and `void M<T, U>()` need distinct suffixes. Current suffix logic only considers parameter count.

## Recommended Implementation Order

If pursued, implement in three sub-phases to isolate risk:

1. **Phase A (Low risk, ~1 day):** Model changes + ConstraintInterfaceGenerator + ImplementationGenerator — pure syntactic additions, no behavioral changes to existing code.
2. **Phase B (Medium risk, ~1.5 days):** TraitGenerator parsing + ExtensionMethodsGenerator + override detection — removes the hard rejection, builds the full pipeline.
3. **Phase C (High risk, ~1.5 days):** DefaultBodyRewriter generic syntax support — the most complex change, done last after the rest is stable.

## New Diagnostics

| Code | Description |
|------|-------------|
| `TE0015` | `UnsupportedTypeParameterConstraint` — complex constraint type not yet supported |

## Decision

**Deferred.** Not required for current functionality. The current workaround is to define non-generic method signatures and have the user cast at the call site. Will reconsider after Phase 13 benchmarks and based on user demand.
