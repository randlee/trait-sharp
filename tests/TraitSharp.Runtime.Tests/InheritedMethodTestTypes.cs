using System;
using System.Runtime.InteropServices;
using TraitSharp;

namespace TraitSharp.Runtime.Tests
{
    // ─────────────────────────────────────────────────────────────────────
    // Phase 11: Inherited Method Trait Types
    // Tests that methods inherited from base traits dispatch correctly
    // at runtime, including defaults calling inherited methods.
    // ─────────────────────────────────────────────────────────────────────

    // ── Scenario A: Base trait with property + method, derived adds more ──

    /// <summary>
    /// Base trait: has a property and a required method.
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IDescribable
    {
        int Id { get; }
        string Describe();
    }

    /// <summary>
    /// Derived trait: inherits Id + Describe() from IDescribable,
    /// adds its own property and a default method that calls the inherited Describe().
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IDetailedDescribable : IDescribable
    {
        int Priority { get; }

        /// <summary>Default: calls inherited Describe() and appends Priority.</summary>
        string DetailedDescribe() => $"{Describe()} [pri={Priority}]";
    }

    /// <summary>
    /// Implements IDescribable only (base trait). Simple case.
    /// </summary>
    [ImplementsTrait(typeof(IDescribable))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct SimpleItem
    {
        public int Id;

        public static string Describe_Impl(in SimpleItem self)
            => $"Simple({self.Id})";
    }

    /// <summary>
    /// Implements IDetailedDescribable (derived trait).
    /// Must provide Describe_Impl (inherited required method) but uses default DetailedDescribe.
    /// </summary>
    [ImplementsTrait(typeof(IDetailedDescribable))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct DetailedItem
    {
        public int Id;
        public int Priority;

        public static string Describe_Impl(in DetailedItem self)
            => $"Detailed({self.Id})";
    }

    /// <summary>
    /// Implements IDetailedDescribable and overrides the default DetailedDescribe.
    /// Tests: selective override of inherited default.
    /// </summary>
    [ImplementsTrait(typeof(IDetailedDescribable))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct DetailedItemOverride
    {
        public int Id;
        public int Priority;

        public static string Describe_Impl(in DetailedItemOverride self)
            => $"Override({self.Id})";

        public static string DetailedDescribe_Impl(in DetailedItemOverride self)
            => $"[CUSTOM: id={self.Id}, pri={self.Priority}]";
    }

    // ── Scenario B: Pure method inheritance (method-only base) ──

    /// <summary>
    /// Base trait: layout field + required method only.
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface INameable
    {
        int NameCode { get; }
        string Name();
    }

    /// <summary>
    /// Derived trait: inherits Name() from INameable, adds a default that calls it.
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IGreetable : INameable
    {
        /// <summary>Default: calls inherited Name().</summary>
        string Greet() => $"Hello, {Name()}!";
    }

    /// <summary>
    /// Implements IGreetable — provides Name_Impl (inherited), uses default Greet().
    /// </summary>
    [ImplementsTrait(typeof(IGreetable))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct NamedEntity
    {
        public int NameCode;

        public static string Name_Impl(in NamedEntity self)
            => $"Entity-{self.NameCode}";
    }

    // ── Scenario C: Three-level chain with methods ──

    /// <summary>
    /// Level 1: base trait with property + required method.
    /// Note: method name ComputeValue avoids collision with property Value's GetValue_Impl accessor.
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IValueProvider
    {
        int Value { get; }

        /// <summary>Required: compute the base value.</summary>
        int ComputeValue();
    }

    /// <summary>
    /// Level 2: inherits ComputeValue(), adds default DoubleValue().
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IDoubler : IValueProvider
    {
        /// <summary>Default: calls inherited ComputeValue() * 2.</summary>
        int DoubleValue() => ComputeValue() * 2;
    }

    /// <summary>
    /// Level 3: inherits ComputeValue() + DoubleValue(), adds default QuadValue().
    /// Three-level chain: QuadValue() → DoubleValue() → ComputeValue().
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IQuadrupler : IDoubler
    {
        /// <summary>Default: calls inherited DoubleValue() * 2.</summary>
        int QuadValue() => DoubleValue() * 2;
    }

    /// <summary>
    /// Implements IQuadrupler — provides only ComputeValue_Impl (the leaf required method).
    /// All defaults chain through three levels.
    /// </summary>
    [ImplementsTrait(typeof(IQuadrupler))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct QuadItem
    {
        public int Value;

        public static int ComputeValue_Impl(in QuadItem self)
            => self.Value;
    }

    /// <summary>
    /// Implements IQuadrupler but overrides DoubleValue (the middle default).
    /// Tests: QuadValue should call the overridden DoubleValue, not the default.
    /// </summary>
    [ImplementsTrait(typeof(IQuadrupler))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct QuadItemOverrideMiddle
    {
        public int Value;

        public static int ComputeValue_Impl(in QuadItemOverrideMiddle self)
            => self.Value;

        public static int DoubleValue_Impl(in QuadItemOverrideMiddle self)
            => self.Value * 3; // *3 instead of *2 to distinguish from default
    }

    // ── Scenario D: Base trait with default, derived doesn't add methods ──

    /// <summary>
    /// Base trait with a default method.
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface ITaggable
    {
        int TagCode { get; }

        /// <summary>Default: format tag as string.</summary>
        string FormatTag() => $"TAG-{TagCode}";
    }

    /// <summary>
    /// Derived trait inherits FormatTag() default, adds a property only.
    /// Tests: inherited default method flows through to derived implementers.
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IPriorityTaggable : ITaggable
    {
        int Level { get; }
    }

    /// <summary>
    /// Implements IPriorityTaggable — uses inherited default FormatTag().
    /// </summary>
    [ImplementsTrait(typeof(IPriorityTaggable))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct PriorityTag
    {
        public int TagCode;
        public int Level;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Phase 11: Generic algorithms for inherited method traits
    // ─────────────────────────────────────────────────────────────────────

    public static class InheritedMethodAlgorithms
    {
        /// <summary>Generic describe using base IDescribable trait constraint.</summary>
        public static string DescribeAny<T>(ref T item)
            where T : unmanaged, IDescribableTrait<T>
        {
            return item.Describe();
        }

        /// <summary>Generic detailed describe using derived constraint.</summary>
        public static string DetailedDescribeAny<T>(ref T item)
            where T : unmanaged, IDetailedDescribableTrait<T>
        {
            return item.DetailedDescribe();
        }

        /// <summary>Generic greet using IGreetable constraint (inherits INameable).</summary>
        public static string GreetAny<T>(ref T item)
            where T : unmanaged, IGreetableTrait<T>
        {
            return item.Greet();
        }

        /// <summary>Generic three-level chain using IQuadrupler constraint.</summary>
        public static string QuadSummary<T>(ref T item)
            where T : unmanaged, IQuadruplerTrait<T>
        {
            return $"val={item.ComputeValue()}, x2={item.DoubleValue()}, x4={item.QuadValue()}";
        }

        /// <summary>Generic format using ITaggable base constraint.</summary>
        public static string FormatAny<T>(ref T item)
            where T : unmanaged, ITaggableTrait<T>
        {
            return item.FormatTag();
        }
    }
}
