# TraitSpan Performance Strategy Reference

> **Purpose**: A reference for ensuring consistent application of performance strategies across all TraitSpan types. Use this document to discuss *why* a particular strategy was or wasn't used in a specific location.

## Table of Contents

- [Type Architecture Overview](#type-architecture-overview)
- [Named Strategies](#named-strategies)
  - [S1: JIT Constant-Folded Stride](#s1-jit-constant-folded-stride)
  - [S2: Pointer-Increment Enumeration](#s2-pointer-increment-enumeration)
  - [S3: Native Span Escape Hatch](#s3-native-span-escape-hatch)
  - [S4: Unsigned Bounds Check](#s4-unsigned-bounds-check)
  - [S5: ThrowHelper Dead-Code Elimination](#s5-throwhelper-dead-code-elimination)
  - [S6: Overload-Based Compile-Time Dispatch](#s6-overload-based-compile-time-dispatch)
  - [S7: Row Decomposition (2D → 1D)](#s7-row-decomposition-2d--1d)
  - [S8: Contiguous Fast Path](#s8-contiguous-fast-path)
  - [S9: Slice Type Preservation](#s9-slice-type-preservation)
- [API Method Reference](#api-method-reference)
  - [1D Span Types](#1d-span-types)
  - [2D Span Types](#2d-span-types)
  - [Source-Generated Factory Methods](#source-generated-factory-methods)
- [Benchmark Evidence](#benchmark-evidence)

---

## Type Architecture Overview

TraitSharp provides **8 ref struct types** organized in two tiers:

| Tier | 1D | 2D | When Used |
|------|----|----|-----------|
| **Two-Parameter** (optimized) | `TraitSpan<TLayout, TSource>` / `ReadOnlyTraitSpan<TLayout, TSource>` | `TraitSpan2D<TLayout, TSource>` / `ReadOnlyTraitSpan2D<TLayout, TSource>` | Source type known at compile time, BaseOffset == 0 |
| **Single-Parameter** (strided) | `TraitSpan<TLayout>` / `ReadOnlyTraitSpan<TLayout>` | `TraitSpan2D<TLayout>` / `ReadOnlyTraitSpan2D<TLayout>` | Source type erased (generic code) or non-zero BaseOffset |

**Mutable / Read-Only**: Every strategy described below applies identically to both mutable and read-only variants unless explicitly noted. The only difference is `ref` vs `ref readonly` on return types.

### Terminology: Stride vs Pitch

| Term | Meaning | Applies To |
|------|---------|------------|
| **Stride** | Byte distance between successive source *elements* (i.e. `sizeof(TSource)`) | All span types (1D and 2D) |
| **Pitch** | Byte distance between the start of successive *rows* (i.e. `stride × width`) | 2D span types only |

This follows the convention used by CommunityToolkit.HighPerformance's `Span2D<T>`, where "pitch" is the row-to-row byte offset. In a contiguous full-width 2D view, `pitch == stride × width`. After a sub-column `Slice`, pitch may be larger than `stride × width` because rows include skipped columns.

### Field Layout Comparison

Fields are listed in struct declaration order (`[StructLayout(LayoutKind.Sequential)]`).

**1D Types** — `ReadOnlyTraitSpan<T>` / `TraitSpan<T>` and `ReadOnlyTraitSpan<T,S>` / `TraitSpan<T,S>`:

| # | Two-Parameter (`<TLayout, TSource>`) | Single-Parameter (`<TLayout>`) |
|---|--------------------------------------|--------------------------------|
| 1 | `ref TSource _reference` | `ref byte _reference` |
| 2 | `int _length` | `int _stride` |
| 3 | — | `int _length` |
| **Public** | `Length`, `Stride`¹, `IsEmpty`, `IsContiguous` | `Length`, `Stride`, `IsEmpty`, `IsContiguous` |

¹ Returns `Unsafe.SizeOf<TSource>()` — JIT constant-folded, no field storage.

**2D Types** — `ReadOnlyTraitSpan2D<T>` / `TraitSpan2D<T>` and `ReadOnlyTraitSpan2D<T,S>` / `TraitSpan2D<T,S>`:

| # | Two-Parameter (`<TLayout, TSource>`) | Single-Parameter (`<TLayout>`) |
|---|--------------------------------------|--------------------------------|
| 1 | `ref TSource _reference` | `ref byte _reference` |
| 2 | `int _width` | `int _width` |
| 3 | `int _height` | `int _height` |
| 4 | — | `int _stride` *(element stride)* |
| 5 | — | `int _rowStride` *(pitch — row stride)* |
| **Public** | `Width`, `Height`, `Length`, `Stride`¹, `Pitch`², `IsEmpty`, `IsContiguous` | `Width`, `Height`, `Length`, `Stride`, `Pitch`, `IsEmpty`, `IsContiguous` |

¹ Returns `Unsafe.SizeOf<TSource>()` — JIT constant-folded, no field storage.
² Returns `Unsafe.SizeOf<TSource>() * _width` — JIT constant-folds the element size; only `_width` is a runtime value.

**Stride source comparison:**

| | Two-Parameter | Single-Parameter |
|---|---|---|
| **Stride** | JIT constant: `Unsafe.SizeOf<TSource>()` | Runtime field: `_stride` |
| **Pitch** | JIT-assisted: `Unsafe.SizeOf<TSource>() * _width` | Runtime field: `_rowStride` |

---

## Named Strategies

### S1: JIT Constant-Folded Stride

**Problem**: Element access in a span requires computing `baseAddress + index * stride`. If `stride` is a runtime field, every element access has a multiply against a memory load.

**Strategy**: In two-parameter types (`<TLayout, TSource>`), use `Unsafe.Add<TSource>(ref _reference, index)` instead of `Unsafe.AddByteOffset(ref _reference, index * _stride)`. Because `TSource` is a concrete generic type parameter, the JIT replaces `sizeof(TSource)` with an immediate constant and folds the multiply into the addressing mode.

**Where applied**:
- All two-parameter type indexers, `DangerousGetReferenceAt`, `Slice`, `CopyTo`, `Fill`, enumerator `Current`
- Two-parameter 2D indexer: `Unsafe.Add<TSource>(ref _reference, row * _width + col)` — the JIT constant-folds the element-size multiply within the flat-index arithmetic

**Where NOT applied**:
- Single-parameter types — `TSource` is erased, so `_stride` must be a runtime field. These types exist for generic/type-erased scenarios where the source type is unknown.

**Measured impact**: Two-parameter 1D foreach achieves **parity with native `Span<T>`** (167.2 μs vs 167.0 μs). Indexer access is within 8% of native span.

### S2: Pointer-Increment Enumeration

**Problem**: A naive enumerator would compute `base + index * stride` on every iteration, requiring a multiply per element.

**Strategy**: Two different patterns are used depending on the type tier:

**Single-parameter types** (strided): The enumerator pre-decrements `_current` by `_stride` bytes in the constructor, then each `MoveNext()` adds `_stride` bytes via `Unsafe.AddByteOffset`. This turns the per-element cost into a single pointer add — no multiply.

```
Constructor: _current = Unsafe.SubtractByteOffset(ref reference, stride)
MoveNext:    _current = Unsafe.AddByteOffset(ref _current, stride); return ++_index < _length
Current:     Unsafe.As<byte, TLayout>(ref _current)
```

**Two-parameter types** (JIT-optimized): The enumerator stores `_start` (ref TSource) and `_index`, then each `Current` computes `Unsafe.Add<TSource>(ref _start, _index)`. This relies on [S1](#s1-jit-constant-folded-stride) to constant-fold the stride multiply, making the per-element cost equivalent to native span enumeration.

```
Constructor: _start = ref reference; _index = -1
MoveNext:    return ++_index < _length
Current:     Unsafe.As<TSource, TLayout>(ref Unsafe.Add<TSource>(ref _start, _index))
```

**Why two patterns**: The single-parameter pattern avoids a multiply against a *runtime* field. The two-parameter pattern can afford `index * sizeof(TSource)` because the JIT constant-folds it. Using the simpler index-based pattern on two-parameter types also allows the JIT to reason about the access pattern for bounds-check elimination.

**Where applied**: All `GetEnumerator()` / `Enumerator` implementations across all 8 span types.

**Measured impact**: `foreach` on two-parameter TraitSpan achieves parity or better vs native `Span<T>.Enumerator`.

### S3: Native Span Escape Hatch

**Problem**: When `sizeof(TLayout) == sizeof(TSource)` and the trait starts at offset 0, the trait span is a view over data that is already layout-compatible. Using trait span access patterns adds unnecessary abstraction overhead and prevents SIMD auto-vectorization.

**Strategy**: Provide `AsNativeSpan()` / `TryAsNativeSpan()` methods that return a native `Span<TLayout>` or `ReadOnlySpan<TLayout>` via `MemoryMarshal.CreateSpan` (instance methods) or `MemoryMarshal.Cast` (factory methods). The caller gets a zero-cost reinterpret of the underlying memory.

**Instance methods** (on all 8 span types):
```csharp
// Two-parameter: checks sizeof(TSource) == sizeof(TLayout)
public ReadOnlySpan<TLayout> AsNativeSpan()
{
    if (Unsafe.SizeOf<TSource>() != Unsafe.SizeOf<TLayout>())
        ThrowHelper.ThrowInvalidOperationException_NotContiguous();
    return MemoryMarshal.CreateReadOnlySpan(
        ref Unsafe.As<TSource, TLayout>(ref Unsafe.AsRef(in _reference)), _length);
}

// Single-parameter: checks _stride == sizeof(TLayout)
public ReadOnlySpan<TLayout> AsNativeSpan()
{
    if (_stride != Unsafe.SizeOf<TLayout>())
        ThrowHelper.ThrowInvalidOperationException_NotContiguous();
    return MemoryMarshal.CreateReadOnlySpan(
        ref Unsafe.As<byte, TLayout>(ref Unsafe.AsRef(in _reference)), _length);
}
```

**Factory methods** (source-generated, generic):
```csharp
// Uses MemoryMarshal.Cast for zero-copy reinterpretation
public static ReadOnlySpan<TLayout> AsXxxNativeSpan<T>(this ReadOnlySpan<T> source)
    where T : unmanaged, IXxxTrait<T>
{
    if (T.TraitOffset != 0 || Unsafe.SizeOf<T>() != Unsafe.SizeOf<TLayout>())
        ThrowHelper.ThrowInvalidOperationException_NotLayoutCompatible();
    return MemoryMarshal.Cast<T, TLayout>(source);
}
```

**Where applied**: All 8 span types (`AsNativeSpan`, `TryAsNativeSpan`), plus source-generated `AsXxxNativeSpan` / `TryAsXxxNativeSpan` factory methods.

**Measured impact**: `AsNativeSpan()` achieves **exact parity** with native span (1.00x ratio). This is the recommended fast path when layout size matches source size.

### S4: Unsigned Bounds Check

**Problem**: A bounds check requires testing both `index < 0` and `index >= length` — two branches.

**Strategy**: Cast both operands to `uint`: `(uint)index >= (uint)length`. If `index` is negative, the cast wraps to a large positive number that exceeds `length`, catching both out-of-range cases in a single branch.

**Where applied**:
- All indexers (`this[int index]`, `this[int row, int col]`)
- All `Slice` methods
- All `GetRow` methods
- All `SliceRows` methods
- All `DangerousGetReferenceAt` (NOT applied — no bounds check by design)

**Where NOT applied**:
- `DangerousGetReference` / `DangerousGetReferenceAt` — these are explicitly unchecked for hot-loop scenarios where bounds have been validated externally.

### S5: ThrowHelper Dead-Code Elimination

**Problem**: Throwing an exception inline expands the method body, preventing inlining and polluting the instruction cache. The JIT may also generate exception-handling prologues even on the hot path.

**Strategy**: All throw sites call static methods on `ThrowHelper` marked with `[DoesNotReturn]`. The JIT treats `[DoesNotReturn]` calls as cold paths and does not generate code for them on the hot path. The throw method body is never inlined.

**Methods on ThrowHelper**:
- `ThrowIndexOutOfRangeException()` — indexers
- `ThrowArgumentOutOfRangeException()` — `Slice`, `GetRow`, `SliceRows`
- `ThrowArgumentException_InvalidDimensions()` — 2D factory construction
- `ThrowInvalidOperationException_NotContiguous()` — `AsNativeSpan`
- `ThrowInvalidOperationException_NotLayoutCompatible()` — generic native span factories
- `ThrowArgumentException_DestinationTooShort()` — `CopyTo`

**Where applied**: Every bounds check and validation across all 8 span types and all factory methods.

### S6: Overload-Based Compile-Time Dispatch

**Problem**: The source generator emits both generic factory methods (`AsXxxSpan<T>(...)`) and concrete per-implementation overloads (`AsXxxSpan(Span<ConcreteType>)`). We need the compiler to automatically select the optimized concrete overload when the source type is known.

**Strategy**: C# overload resolution prefers non-generic methods over generic ones when both match. The source generator emits:

1. **Generic factory** (trait-level, single-parameter return):
   ```csharp
   public static ReadOnlyTraitSpan<Layout> AsXxxSpan<T>(this ReadOnlySpan<T> source)
       where T : unmanaged, IXxxTrait<T> { ... }
   ```

2. **Concrete factory** (per-implementation, two-parameter return):
   ```csharp
   public static ReadOnlyTraitSpan<Layout, BenchmarkPoint> AsXxxSpan(
       this ReadOnlySpan<BenchmarkPoint> source) { ... }
   ```

When calling `myArray.AsSpan().AsXxxSpan()` on a `BenchmarkPoint[]`, the compiler picks overload #2 because it's a more specific match. No user annotation or explicit type selection needed.

**Condition for emission**: Per-implementation factories are only generated when `BaseOffset == 0`. Non-zero offsets cannot use two-parameter types (which assume the layout starts at byte 0 of the source struct).

**Where applied**: Source generator `TraitSpanFactoryGenerator` — `Generate()` for generic, `GeneratePerImplementation()` / `GeneratePerExternalImplementation()` for concrete.

### S7: Row Decomposition (2D → 1D)

**Problem**: 2D element access (`span2d[row, col]`) requires two bounds checks (row and column) plus a flat-index computation. This is inherently slower than 1D access.

**Strategy**: Provide `GetRow(int row)` which returns a 1D `TraitSpan<TLayout, TSource>` (two-parameter) or `TraitSpan<TLayout>` (single-parameter). The row extraction performs one bounds check. Subsequent iteration over the row uses optimized 1D access patterns with only one bounds check per element.

**Iteration pattern**:
```csharp
for (int row = 0; row < span2d.Height; row++)
{
    foreach (ref readonly var item in span2d.GetRow(row))  // 1D iteration
        sum += item.X + item.Y;
}
```

**Why this matters**: The 2D indexer has ~22-30% overhead vs native span due to double bounds checks. Row decomposition reduces this to **parity or better** (1.01x–1.09x faster than the native span baseline in benchmarks).

**Where applied**: `GetRow()` on all four 2D types. `EnumerateRows()` / `RowEnumerator` for convenient `foreach` over rows.

### S8: Contiguous Fast Path

**Problem**: Bulk operations like `CopyTo` and `Fill` iterate element-by-element with stride arithmetic when the layout is strided. When the layout happens to be contiguous (stride equals layout size), this wastes time compared to a bulk memory operation.

**Strategy**: Check `IsContiguous` (or inline the equivalent `_stride == Unsafe.SizeOf<TLayout>()` / `Unsafe.SizeOf<TSource>() == Unsafe.SizeOf<TLayout>()`) at the top of bulk operations. If contiguous, delegate to `MemoryMarshal.CreateSpan<TLayout>().CopyTo()` or `.Fill()`, which the runtime implements with optimized `memcpy`/`memset`.

**Where applied**:
- `CopyTo(Span<TLayout>)` — all four 1D types
- `Fill(TLayout)` — mutable 1D types (`TraitSpan<TLayout>`, `TraitSpan<TLayout, TSource>`)
- 2D `Fill` — delegates to per-row `Fill` which individually checks contiguity

**Where NOT applied**:
- `ToArray()` — delegates to `CopyTo`, inherits the fast path
- `Clear()` — delegates to `Fill(default)`, inherits the fast path

### S9: Slice Type Preservation

**Problem**: Slicing a two-parameter 2D span by arbitrary sub-columns would require storing a pitch different from `width * sizeof(TSource)`, breaking the JIT constant-folding advantage.

**Strategy**: Two distinct slice methods:

1. **`SliceRows(rowStart, height)`** — returns the same two-parameter type (`TraitSpan2D<TLayout, TSource>`). This preserves all JIT optimizations because the result is still full-width contiguous rows.

2. **`Slice(rowStart, colStart, height, width)`** — returns the single-parameter strided type (`TraitSpan2D<TLayout>`). This handles arbitrary sub-column regions where pitch differs from `stride × width` and JIT constant folding is not possible.

**Design rationale**: Rather than always returning the strided form (losing optimization) or refusing sub-column slicing (losing functionality), the API offers both. Users who know they're slicing full rows get optimal performance automatically.

**Where applied**: All four 2D types.

---

## API Method Reference

The tables below list every public API method and which named strategies apply. Methods that are identical between mutable and read-only variants are listed once.

### 1D Span Types

Applies to all four 1D types: `ReadOnlyTraitSpan<TLayout>`, `TraitSpan<TLayout>`, `ReadOnlyTraitSpan<TLayout, TSource>`, `TraitSpan<TLayout, TSource>`.

| Method | Strategies | Notes |
|--------|-----------|-------|
| **Constructor** | — | Two-param: `(ref TSource, int length)`. Single-param: `(ref byte, int stride, int length)`. |
| **`this[int index]`** (Indexer) | S1, S4, S5 | Two-param uses `Unsafe.Add<TSource>`. Single-param uses `Unsafe.AddByteOffset` with `index * _stride`. |
| **`DangerousGetReference()`** | S1 | No bounds check (by design). Direct `Unsafe.As` cast of `_reference`. |
| **`DangerousGetReferenceAt(int)`** | S1 | No bounds check. Uses `Unsafe.Add<TSource>` (two-param) or `Unsafe.AddByteOffset` (single-param). |
| **`Slice(int start)`** | S1, S4, S5 | Returns same type. Two-param uses `Unsafe.Add<TSource>`. |
| **`Slice(int start, int length)`** | S1, S4, S5 | Returns same type. Separate bounds validation for start and length. |
| **`CopyTo(Span<TLayout>)`** | S1, S5, S8 | Contiguous fast path delegates to bulk `Span.CopyTo`. Strided path iterates with `Unsafe.Add` / `AddByteOffset`. |
| **`Fill(TLayout)`** | S1, S5, S8 | Mutable types only. Contiguous fast path delegates to `Span.Fill`. |
| **`Clear()`** | S8 | Delegates to `Fill(default)`. |
| **`ToArray()`** | S8 | Allocates array, delegates to `CopyTo`. |
| **`AsNativeSpan()`** | S3, S5 | Returns native `Span<TLayout>` / `ReadOnlySpan<TLayout>`. Checks contiguity/size equality. |
| **`TryAsNativeSpan()`** | S3 | Safe try-pattern. Returns `false` if not contiguous. |
| **`GetEnumerator()`** | S2 | Returns nested `Enumerator` ref struct. |
| **`Enumerator.MoveNext()`** | S2 | Pointer-increment (single-param) or index-increment (two-param). |
| **`Enumerator.Current`** | S1, S2 | Two-param: `Unsafe.Add<TSource>(ref _start, _index)`. Single-param: pre-incremented `_current` pointer. |
| **Properties** (`Length`, `IsEmpty`, `Stride`, `IsContiguous`) | — | Trivial field reads. `Stride` on two-param is `Unsafe.SizeOf<TSource>()` (JIT constant). |
| **Implicit conversions** | — | Two-param → single-param: wraps with `Unsafe.SizeOf<TSource>()` as stride. Mutable → read-only. |
| **`Empty`** | — | Returns `default`. |

### 2D Span Types

Applies to all four 2D types: `ReadOnlyTraitSpan2D<TLayout>`, `TraitSpan2D<TLayout>`, `ReadOnlyTraitSpan2D<TLayout, TSource>`, `TraitSpan2D<TLayout, TSource>`.

| Method | Strategies | Notes |
|--------|-----------|-------|
| **Constructor** | — | Two-param: `(ref TSource, int width, int height)`. Single-param: `(ref byte, int stride, int width, int height)` with computed pitch (`_rowStride = stride × width`). |
| **`this[int row, int col]`** (2D Indexer) | S1, S4, S5 | Two-param: `Unsafe.Add<TSource>(ref, row * _width + col)`. Single-param: `Unsafe.AddByteOffset(ref, row * pitch + col * stride)`. Two bounds checks (row + col). |
| **`DangerousGetReference()`** | S1 | No bounds check. Element at (0,0). |
| **`DangerousGetReferenceAt(int, int)`** | S1 | No bounds check. Flat-index or byte-offset computation. |
| **`GetRow(int row)`** | S1, S4, S5, S7 | Returns 1D span for the row. Two-param returns `TraitSpan<TLayout, TSource>`. Single-param returns `TraitSpan<TLayout>` with `_stride`. |
| **`SliceRows(int, int)`** | S4, S5, S9 | Two-param types only. Returns same two-parameter 2D type (preserves JIT optimization). |
| **`Slice(int, int, int, int)`** | S4, S5, S9 | All 2D types. Returns single-parameter strided 2D type (supports arbitrary sub-columns). |
| **`AsSpan()`** | S1 | Flattens 2D → 1D. Two-param returns `TraitSpan<TLayout, TSource>`. Single-param returns `TraitSpan<TLayout>`. |
| **`AsNativeSpan()`** | S3, S5 | Returns native span. Checks contiguity/size equality. |
| **`TryAsNativeSpan()`** | S3 | Safe try-pattern. |
| **`Fill(TLayout)`** | S7, S8 | Mutable types only. Iterates rows via `GetRow`, each row's `Fill` checks contiguity. |
| **`Clear()`** | S7, S8 | Delegates to `Fill(default)`. |
| **`EnumerateRows()`** | S7 | Returns `RowEnumerator`. Each `Current` calls `GetRow`. |
| **Properties** (`Width`, `Height`, `Length`, `Stride`, `Pitch`, `IsEmpty`, `IsContiguous`) | S1 | `Stride`: element byte distance (JIT constant on two-param). `Pitch`: row byte distance (JIT-assisted on two-param). Others are trivial field reads. |
| **Implicit conversions** | — | Two-param → single-param (strided): provides `Unsafe.SizeOf<TSource>()` as stride. Mutable → read-only. |
| **`Empty`** | — | Returns `default`. |

### Source-Generated Factory Methods

Generated by `TraitSpanFactoryGenerator` in the source generator.

#### Generic Factory (Trait-Level, All Implementations)

Returns single-parameter strided types. Generic over `T where T : unmanaged, IXxxTrait<T>`.

| Method | Strategies | Return Type |
|--------|-----------|-------------|
| `AsXxxSpan<T>(ReadOnlySpan<T>)` | S6 | `ReadOnlyTraitSpan<TLayout>` |
| `AsXxxSpan<T>(Span<T>)` | S6 | `ReadOnlyTraitSpan<TLayout>` |
| `AsXxxTraitSpan<T>(Span<T>)` | S6 | `TraitSpan<TLayout>` |
| `AsXxxSpan2D<T>(ReadOnlySpan<T>, int, int)` | S5, S6 | `ReadOnlyTraitSpan2D<TLayout>` |
| `AsXxxSpan2D<T>(Span<T>, int, int)` | S5, S6 | `ReadOnlyTraitSpan2D<TLayout>` |
| `AsXxxTraitSpan2D<T>(Span<T>, int, int)` | S5, S6 | `TraitSpan2D<TLayout>` |
| `AsXxxNativeSpan<T>(ReadOnlySpan<T>)` | S3, S5, S6 | `ReadOnlySpan<TLayout>` |
| `AsXxxNativeSpan<T>(Span<T>)` | S3, S5, S6 | `ReadOnlySpan<TLayout>` |
| `AsXxxNativeTraitSpan<T>(Span<T>)` | S3, S5, S6 | `Span<TLayout>` |
| `TryAsXxxNativeSpan<T>(ReadOnlySpan<T>, out ...)` | S3, S6 | `bool` + `ReadOnlySpan<TLayout>` |
| `TryAsXxxNativeTraitSpan<T>(Span<T>, out ...)` | S3, S6 | `bool` + `Span<TLayout>` |

#### Per-Implementation Factory (Concrete Type, BaseOffset == 0 Only)

Returns two-parameter optimized types. Non-generic, takes concrete source type.

| Method | Strategies | Return Type |
|--------|-----------|-------------|
| `AsXxxSpan(ReadOnlySpan<TSource>)` | S1, S6 | `ReadOnlyTraitSpan<TLayout, TSource>` |
| `AsXxxSpan(Span<TSource>)` | S1, S6 | `ReadOnlyTraitSpan<TLayout, TSource>` |
| `AsXxxTraitSpan(Span<TSource>)` | S1, S6 | `TraitSpan<TLayout, TSource>` |
| `AsXxxSpan2D(ReadOnlySpan<TSource>, int, int)` | S1, S5, S6 | `ReadOnlyTraitSpan2D<TLayout, TSource>` |
| `AsXxxSpan2D(Span<TSource>, int, int)` | S1, S5, S6 | `ReadOnlyTraitSpan2D<TLayout, TSource>` |
| `AsXxxTraitSpan2D(Span<TSource>, int, int)` | S1, S5, S6 | `TraitSpan2D<TLayout, TSource>` |

---

## Benchmark Evidence

All benchmarks run on Apple M4 Max, .NET 8.0, BenchmarkDotNet v0.14.0.

### 1D Same-Size Layout (BenchmarkPoint — `sizeof(TLayout) == sizeof(TSource)`)

| Method | Time | vs Baseline | Strategies Demonstrated |
|--------|------|-------------|------------------------|
| NativeSpan_Sum1D (baseline) | 167.0 μs | — | — |
| TraitSpan_Foreach | 167.2 μs | **1.00x (parity)** | S1, S2 |
| TraitSpan_Indexer | 180.2 μs | 1.08x slower | S1, S4 |
| TraitNativeSpan_Sum1D | 174.0 μs | 1.04x slower | S3 |

### 2D Same-Size Layout (BenchmarkPoint)

| Method | Time | vs Baseline | Strategies Demonstrated |
|--------|------|-------------|------------------------|
| NativeSpan_Sum2D (baseline) | 178.3 μs | — | — |
| TraitSpan2D_Sum2D (2D indexer) | 232.6 μs | 1.30x slower | S1, S4 (double bounds check) |
| TraitSpan2D_RowSum | 163.7 μs | **1.09x faster** | S1, S2, S7 |
| TraitNativeSpan_Sum2D | 178.3 μs | **1.00x (parity)** | S3 |

### 1D Strided Layout (BenchmarkRect — `sizeof(TSource) > sizeof(TLayout)`)

| Method | Time | vs Baseline | Strategies Demonstrated |
|--------|------|-------------|------------------------|
| NativeSpan_CoordSum1D (baseline) | 175.0 μs | — | — |
| TraitSpan_CoordSum1D | 180.5 μs | 1.03x slower | S1 (strided, larger struct) |
| NativeSpan_SizeSum1D | 164.9 μs | 1.06x faster | Different trait view |
| TraitSpan_SizeSum1D | 188.6 μs | 1.08x slower | S1 (strided, larger struct) |
| TraitSpan_BothSum1D (dual spans) | 280.2 μs | 1.60x slower | Doubled cache misses |

### 2D Strided Layout (BenchmarkRect)

| Method | Time | vs Baseline | Strategies Demonstrated |
|--------|------|-------------|------------------------|
| NativeSpan_CoordSum2D (baseline) | 186.8 μs | — | — |
| TraitSpan2D_CoordSum2D (2D indexer) | 228.5 μs | 1.22x slower | S4 (double bounds check) |
| TraitSpan2D_CoordRowSum | 189.1 μs | **1.01x (parity)** | S1, S2, S7 |
| TraitSpan2D_BothSum2D (dual spans) | 363.3 μs | 1.95x slower | Doubled cache misses |

### Key Takeaways

1. **`foreach` / `GetRow` iteration achieves parity or better** — use S1 + S2 + S7 as the default pattern.
2. **`AsNativeSpan()` achieves exact parity** — use S3 when layout sizes match.
3. **2D indexer has ~22-30% overhead** — inherent to double bounds checking. Use row decomposition (S7) when iterating.
4. **Dual-span iteration has 60-95% overhead** — caused by doubled cache misses from two interleaved trait views over the same array. This is a fundamental cache-line effect, not a strategy failure.
5. **Strided access (larger source struct) adds ~3-8% overhead** vs same-size layout — the JIT constant-folds the stride but the larger element size reduces cache efficiency.

### Recommended Access Patterns (Fastest → Slowest)

| Pattern | Expected Performance |
|---------|---------------------|
| `AsNativeSpan()` + native iteration | Exact parity |
| `foreach` on 1D TraitSpan | Parity or faster |
| `GetRow()` + `foreach` on 2D | Parity |
| 1D indexer loop | ~8% overhead |
| 2D indexer loop | ~22-30% overhead |
| Dual trait spans over same array | ~60-95% overhead (cache effect) |
