using System;
using System.Runtime.InteropServices;
using TraitSharp;

namespace TraitSharp.Runtime.Tests
{
    // ─────────────────────────────────────────────────────────────────────
    // Phase 8: Method Trait Types
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Trait with a single required method and a layout property.
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface ILabeled
    {
        int Tag { get; }
        string Describe();
    }

    /// <summary>
    /// Implements ILabeled with user-provided Describe_Impl.
    /// </summary>
    [ImplementsTrait(typeof(ILabeled))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct LabeledItem
    {
        public int Tag;
        public float Value;

        public static string Describe_Impl(in LabeledItem self)
        {
            return $"Item(Tag={self.Tag}, Value={self.Value:F1})";
        }
    }

    /// <summary>
    /// Trait with multiple methods: one returns string, one returns void, one returns int.
    /// Tests diverse return types for method dispatch.
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IMultiMethod
    {
        int Id { get; }
        string Name();
        void Reset();
        int DoubleId();
    }

    /// <summary>
    /// Implements IMultiMethod with all three methods.
    /// Reset is a void method that is a no-op (stateless struct dispatch).
    /// </summary>
    [ImplementsTrait(typeof(IMultiMethod))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct MultiMethodItem
    {
        public int Id;

        public static string Name_Impl(in MultiMethodItem self)
            => $"MMI-{self.Id}";

        public static void Reset_Impl(in MultiMethodItem self)
        {
            // No-op for readonly dispatch
        }

        public static int DoubleId_Impl(in MultiMethodItem self)
            => self.Id * 2;
    }

    /// <summary>
    /// Trait with method only, no properties. Tests pure method dispatch.
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IGreeter
    {
        string Greet(string name);
    }

    /// <summary>
    /// Implements IGreeter — method with parameter.
    /// </summary>
    [ImplementsTrait(typeof(IGreeter))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct FormalGreeter
    {
        public int Dummy; // Structs need at least one field for unmanaged layout

        public static string Greet_Impl(in FormalGreeter self, string name)
            => $"Good day, {name}!";
    }

    /// <summary>
    /// Second implementer of IGreeter — verifies different types dispatch differently.
    /// </summary>
    [ImplementsTrait(typeof(IGreeter))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct CasualGreeter
    {
        public int Dummy;

        public static string Greet_Impl(in CasualGreeter self, string name)
            => $"Hey {name}!";
    }

    // ─────────────────────────────────────────────────────────────────────
    // Phase 8: Generic algorithms using method trait constraints
    // ─────────────────────────────────────────────────────────────────────

    public static class MethodTraitAlgorithms
    {
        /// <summary>Generic describe using ILabeled trait constraint.</summary>
        public static string DescribeItem<T>(ref T item)
            where T : unmanaged, ILabeledTrait<T>
        {
            return item.Describe();
        }

        /// <summary>Generic greet using IGreeter trait constraint.</summary>
        public static string GreetPerson<T>(ref T greeter, string name)
            where T : unmanaged, IGreeterTrait<T>
        {
            return greeter.Greet(name);
        }

        /// <summary>Generic multi-method using IMultiMethod trait constraint.</summary>
        public static string SummarizeMulti<T>(ref T item)
            where T : unmanaged, IMultiMethodTrait<T>
        {
            return $"{item.Name()} doubled={item.DoubleId()}";
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // Phase 9: Default Method Types
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Trait with default methods:
    /// - Describe() has a default body referencing the Tag property
    /// - Area() is required (no default)
    /// - Perimeter() has a default body returning 0
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IShape
    {
        int Tag { get; }

        /// <summary>Default: returns a generic description.</summary>
        string Describe() { return $"Shape(Tag={Tag})"; }

        /// <summary>Required: no default body.</summary>
        float Area();

        /// <summary>Default: returns 0f.</summary>
        float Perimeter() { return 0f; }
    }

    /// <summary>
    /// ShapeRect: overrides Area (required) + Perimeter (default), keeps Describe default.
    /// Tests: default method used when no override provided.
    /// </summary>
    [ImplementsTrait(typeof(IShape))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct ShapeRect
    {
        public int Tag;
        public float Width, Height;

        public static float Area_Impl(in ShapeRect self)
            => self.Width * self.Height;

        public static float Perimeter_Impl(in ShapeRect self)
            => 2f * (self.Width + self.Height);
    }

    /// <summary>
    /// ShapeCircle: overrides Area (required) + Describe (default), keeps Perimeter default.
    /// Tests: default Perimeter returns 0f, overridden Describe uses custom format.
    /// </summary>
    [ImplementsTrait(typeof(IShape))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct ShapeCircle
    {
        public int Tag;
        public float Radius;

        public static float Area_Impl(in ShapeCircle self)
            => MathF.PI * self.Radius * self.Radius;

        public static string Describe_Impl(in ShapeCircle self)
            => $"Circle(r={self.Radius:F1}, Tag={self.Tag})";
    }

    /// <summary>
    /// ShapeSquare: overrides ALL methods — no defaults used.
    /// Corner case: ensures defaults are completely skipped.
    /// </summary>
    [ImplementsTrait(typeof(IShape))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct ShapeSquare
    {
        public int Tag;
        public float Side;

        public static float Area_Impl(in ShapeSquare self)
            => self.Side * self.Side;

        public static string Describe_Impl(in ShapeSquare self)
            => $"Square(s={self.Side:F1}, Tag={self.Tag})";

        public static float Perimeter_Impl(in ShapeSquare self)
            => 4f * self.Side;
    }

    /// <summary>
    /// Trait with only default methods (no required methods).
    /// Edge case: all methods have defaults, implementing type provides nothing.
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IDefaultOnly
    {
        int Code { get; }
        string Label() { return $"Default-{Code}"; }
        int Priority() { return 0; }
    }

    /// <summary>
    /// DefaultOnlyItem: provides NO method overrides — all defaults should be used.
    /// </summary>
    [ImplementsTrait(typeof(IDefaultOnly))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct DefaultOnlyItem
    {
        public int Code;
    }

    /// <summary>
    /// Trait with expression-body defaults (arrow syntax).
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IExprDefault
    {
        int Val { get; }
        int Doubled() => Val * 2;
        string Display() => $"V={Val}";
    }

    /// <summary>
    /// ExprDefaultItem: uses expression-body defaults, no overrides.
    /// </summary>
    [ImplementsTrait(typeof(IExprDefault))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct ExprDefaultItem
    {
        public int Val;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Phase 9: Generic algorithms for default method traits
    // ─────────────────────────────────────────────────────────────────────

    public static class DefaultMethodAlgorithms
    {
        /// <summary>Generic shape summary using IShape trait constraint.</summary>
        public static string ShapeSummary<T>(ref T shape)
            where T : unmanaged, IShapeTrait<T>
        {
            return $"{shape.Describe()} → area={shape.Area():F2}, perim={shape.Perimeter():F2}";
        }

        /// <summary>Generic default-only using IDefaultOnly trait constraint.</summary>
        public static string DefaultOnlySummary<T>(ref T item)
            where T : unmanaged, IDefaultOnlyTrait<T>
        {
            return $"{item.Label()} pri={item.Priority()}";
        }
    }
}
