using TraitSharp;

namespace TraitSharp.CrossAssembly.Traits;

// ═══════════════════════════════════════════════════════════════════════════════
// Cross-Assembly Trait Definitions
// These traits are defined in a SEPARATE assembly from their implementations.
// The source generator runs in the consuming assembly, not here.
// This validates that trait metadata is discoverable across compilation boundaries.
// ═══════════════════════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────────────────────────────
// 1. Basic property-only trait (cross-assembly baseline)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Property-only trait defined in external assembly.
/// Validates basic cross-assembly property access and layout generation.
/// </summary>
[Trait(GenerateLayout = true)]
public partial interface IExternalCoordinate
{
    int X { get; }
    int Y { get; }
}

// ─────────────────────────────────────────────────────────────────────────────
// 2. Property + method trait (cross-assembly method dispatch)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Trait with property and required method, defined externally.
/// Validates cross-assembly method dispatch via _Impl pattern.
/// </summary>
[Trait(GenerateLayout = true)]
public partial interface IExternalLabeled
{
    int Code { get; }

    /// <summary>Required: implementor must provide Label_Impl.</summary>
    string Label();
}

// ─────────────────────────────────────────────────────────────────────────────
// 3. Trait with default methods (cross-assembly default body emission)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Trait with both required and default methods, defined externally.
/// The source generator in the consuming assembly must:
/// 1. Discover the trait definition across compilation boundary
/// 2. Extract default method body syntax
/// 3. Emit default implementations for types that don't override
/// </summary>
[Trait(GenerateLayout = true)]
public partial interface IExternalShape
{
    float Width { get; }
    float Height { get; }

    /// <summary>Required: each shape must provide its own Area calculation.</summary>
    float Area();

    /// <summary>Default: perimeter based on Width + Height. Can be overridden.</summary>
    float Perimeter() => 2f * (Width + Height);

    /// <summary>Default: scale area by a factor. Tests parameterized default cross-assembly.</summary>
    float ScaledArea(float factor) => Area() * factor;
}

// ─────────────────────────────────────────────────────────────────────────────
// 4. Trait inheritance (cross-assembly inherited method dispatch)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Base trait for inheritance chain, defined externally.
/// </summary>
[Trait(GenerateLayout = true)]
public partial interface IExternalBase
{
    int Id { get; }

    /// <summary>Required: implementor must provide Describe_Impl.</summary>
    string Describe();
}

/// <summary>
/// Derived trait inheriting from IExternalBase, defined externally.
/// The consuming assembly's source generator must:
/// 1. Resolve the inheritance chain across compilation boundaries
/// 2. Ensure the derived contract extends the base contract
/// 3. Emit ancestor trait interface implementations (TraitOffset/AsLayout)
/// 4. Emit default body that calls inherited Describe()
/// </summary>
[Trait(GenerateLayout = true)]
public partial interface IExternalDerived : IExternalBase
{
    int CategoryId { get; }

    /// <summary>Default: builds detailed description calling inherited Describe().</summary>
    string DetailedDescribe() => $"{Describe()} [cat={CategoryId}]";
}

// ─────────────────────────────────────────────────────────────────────────────
// 5. Multi-level inheritance (three levels, all external)
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>
/// Three-level chain: IExternalLeaf : IExternalDerived : IExternalBase.
/// Tests deep inheritance resolution across assembly boundaries.
/// </summary>
[Trait(GenerateLayout = true)]
public partial interface IExternalLeaf : IExternalDerived
{
    int Priority { get; }

    /// <summary>Default: full summary calling inherited DetailedDescribe().</summary>
    string FullSummary() => $"[P{Priority}] {DetailedDescribe()}";
}
