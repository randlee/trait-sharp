---
title: "Proposal: Mutable Methods (ref Self)"
status: Deferred
priority: Nice-to-Have
complexity: ~3–4 days
risk: Medium-High
created-date: 2026-02-12
---

# Proposal: Mutable Methods (`ref Self`)

## Summary

Allow trait methods to take `ref Self` (mutable receiver) instead of the current `in Self` (readonly). This enables mutation-oriented patterns like `void Translate(float dx, float dy)` that modify the struct in place.

## Motivation

Currently all trait method dispatch passes `in Self` — a readonly reference. This prevents traits from expressing mutation semantics directly. Users must work around this by returning a new value (`Self Translate(float dx, float dy)` returning a copy), which is idiomatic for immutable value types but awkward for mutable game/simulation structs.

### Example

```csharp
[Trait]
interface IMovable
{
    float X { get; }
    float Y { get; }

    // Mutable method — modifies self in place
    void Translate(ref Self self, float dx, float dy);

    // Readonly method — reads only
    float DistanceFromOrigin(in Self self);
}

[ImplementsTrait(typeof(IMovable))]
partial struct Entity
{
    public float X, Y;

    public static void Translate_Impl(ref Entity self, float dx, float dy)
    {
        self.X += dx;
        self.Y += dy;
    }

    public static float DistanceFromOrigin_Impl(in Entity self)
        => MathF.Sqrt(self.X * self.X + self.Y * self.Y);
}
```

## Design

### Model Changes

Add `IsMutableSelf` boolean to `TraitMethod`. Default is `false` (preserving backward compatibility). When the trait interface declares a method parameter as `ref Self` instead of `in Self`, the parser sets `IsMutableSelf = true`.

### Detection

In `ParseTraitMethod()`, check the first parameter (or the implicit receiver) for `ref` vs `in` modifier. The convention:
- `in Self` → readonly (current behavior, default)
- `ref Self` → mutable

### Code Generation

Each generator must emit `ref` or `in` per-method based on `IsMutableSelf`:

| Generator | Current | With Mutable |
|-----------|---------|-------------|
| `ConstraintInterfaceGenerator` | `static abstract void M_Impl(in TSelf self, ...)` | `static abstract void M_Impl(ref TSelf self, ...)` |
| `ExtensionMethodsGenerator` | `M<T>(this ref T self, ...) { T.M_Impl(in self, ...); }` | `M<T>(this ref T self, ...) { T.M_Impl(ref self, ...); }` |
| `ImplementationGenerator` | `public static void M_Impl(in Type self, ...)` | `public static void M_Impl(ref Type self, ...)` |

Note: Extension methods already use `this ref T self` — only the forwarding call changes (`in self` → `ref self`).

### DefaultBodyRewriter Impact

Default method bodies that call other trait methods must forward the correct modifier:
- Mutable default calling readonly method: **OK** (`ref` → `in` is implicit)
- Readonly default calling mutable method: **Compile error** — the rewriter can't catch this at generation time

The rewriter must track which methods are mutable and emit the correct `ref`/`in` in forwarded calls.

### Cross-Assembly Metadata

The `[TraitDefaultBody]` attribute needs an additional field or a companion attribute to encode whether a method uses `ref Self`. Options:
1. Add `bool IsMutable` parameter to `TraitDefaultBodyAttribute`
2. Create separate `[TraitMethodModifier("MethodName", "ref")]` attribute

Option 1 is simpler and preferred.

## Impact Assessment

| Component | Changes Required | Risk |
|-----------|-----------------|------|
| `TraitModel.TraitMethod` | Add `IsMutableSelf` flag | Low |
| `TraitGenerator.ParseTraitMethod()` | Parse `ref Self` vs `in Self` | Low |
| `ConstraintInterfaceGenerator` | Per-method `ref`/`in` emission | **High** — hardcoded `in` must change |
| `ExtensionMethodsGenerator` | Change forwarding call modifier | Low |
| `ImplementationGenerator` | Per-method `ref`/`in` emission | Medium |
| `DefaultBodyRewriter` | Track mutability context for call forwarding | Medium |
| Runtime (`TraitSpan`) | No changes — already returns mutable `ref TLayout` | None |
| Cross-assembly metadata | Encode mutability in attribute | Medium |

## Risks

1. **Generator coordination (High):** The `in TSelf self` first parameter is hardcoded in three generators. All must change in lockstep per-method. A mismatch produces silent compilation errors in generated code.

2. **Mixed mutability (Medium):** A trait with both `in Self` and `ref Self` methods needs each contract method to emit the correct modifier independently. This is per-method, not per-trait.

3. **DefaultBodyRewriter scope (Medium):** Default bodies that call other trait methods must forward `ref self` vs `in self` correctly. The rewriter currently has no concept of parameter mutability.

4. **Backward compatibility (Low):** `ref Self` is opt-in per method. Existing `in Self` behavior is unchanged. No migration needed.

## New Diagnostics

| Code | Description |
|------|-------------|
| `TE0014` | `MutableSelfInReadonlyContext` — default body with `in Self` calls a `ref Self` method |

## Decision

**Deferred.** Not required for current functionality. Will reconsider after Phase 13 benchmarks confirm the performance story. The readonly `in Self` pattern covers the primary use case (zero-copy read access); mutation via return-new-value is an acceptable workaround.
