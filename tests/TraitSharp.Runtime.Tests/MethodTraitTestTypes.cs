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

    // ─────────────────────────────────────────────────────────────────────
    // Phase 10: Parameterized Default Methods & Chained Dispatch
    // ─────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Trait with a parameterized default method:
    /// - Area() is required
    /// - ScaledArea(float factor) has a default body that calls Area() * factor
    /// Tests: default method with parameter + chained call to required method.
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IScalable
    {
        float Width { get; }
        float Height { get; }

        /// <summary>Required: compute base area.</summary>
        float Area();

        /// <summary>Default: scale the area by a factor. Calls Area() internally.</summary>
        float ScaledArea(float factor) => Area() * factor;
    }

    /// <summary>
    /// ScalableRect: implements Area, relies on default ScaledArea.
    /// Tests: parameterized default forwarding to required method.
    /// </summary>
    [ImplementsTrait(typeof(IScalable))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct ScalableRect
    {
        public float Width, Height;

        public static float Area_Impl(in ScalableRect self)
            => self.Width * self.Height;
    }

    /// <summary>
    /// ScalableRectOverride: overrides BOTH Area and ScaledArea.
    /// Tests: overriding a parameterized default completely.
    /// </summary>
    [ImplementsTrait(typeof(IScalable))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct ScalableRectOverride
    {
        public float Width, Height;

        public static float Area_Impl(in ScalableRectOverride self)
            => self.Width * self.Height;

        public static float ScaledArea_Impl(in ScalableRectOverride self, float factor)
            => self.Width * self.Height * factor + 1f; // +1 to distinguish from default
    }

    /// <summary>
    /// Trait with chained default methods:
    /// - BaseValue() is required
    /// - Doubled() default calls BaseValue() * 2
    /// - Quadrupled() default calls Doubled() * 2 (chain: default calling default)
    /// Tests: default method calling another default method (chained dispatch).
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IChainable
    {
        int Seed { get; }

        /// <summary>Required: the base value.</summary>
        int BaseValue();

        /// <summary>Default: doubles the base value. Calls BaseValue().</summary>
        int Doubled() => BaseValue() * 2;

        /// <summary>Default: quadruples. Calls Doubled() (another default).</summary>
        int Quadrupled() => Doubled() * 2;
    }

    /// <summary>
    /// ChainItem: implements only BaseValue, relies on both Doubled and Quadrupled defaults.
    /// Tests: full chain default→default→required.
    /// </summary>
    [ImplementsTrait(typeof(IChainable))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct ChainItem
    {
        public int Seed;

        public static int BaseValue_Impl(in ChainItem self)
            => self.Seed;
    }

    /// <summary>
    /// ChainItemOverrideMiddle: overrides Doubled, keeps Quadrupled default.
    /// Tests: Quadrupled default calls overridden Doubled, not the default Doubled.
    /// </summary>
    [ImplementsTrait(typeof(IChainable))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct ChainItemOverrideMiddle
    {
        public int Seed;

        public static int BaseValue_Impl(in ChainItemOverrideMiddle self)
            => self.Seed;

        public static int Doubled_Impl(in ChainItemOverrideMiddle self)
            => self.Seed * 3; // Intentionally *3 to distinguish from default *2
    }

    /// <summary>
    /// Trait with a default method that uses a property and a parameter together.
    /// - Name is a property
    /// - Greeting(string prefix) default uses both the property and the parameter
    /// Tests: default body referencing both a property and a method parameter.
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IFormattable
    {
        int Id { get; }

        /// <summary>Default: format with prefix. Combines property + parameter.</summary>
        string Format(string prefix) => $"{prefix}-{Id}";

        /// <summary>Default: format with prefix and suffix (two parameters).</summary>
        string FormatFull(string prefix, string suffix) => $"{prefix}-{Id}-{suffix}";
    }

    /// <summary>
    /// FormattableItem: uses all defaults. Tests multi-param + property defaults.
    /// </summary>
    [ImplementsTrait(typeof(IFormattable))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct FormattableItem
    {
        public int Id;
    }

    /// <summary>
    /// FormattableItemOverride: overrides Format, keeps FormatFull default.
    /// Tests: selective override with multi-param default remaining.
    /// </summary>
    [ImplementsTrait(typeof(IFormattable))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct FormattableItemOverride
    {
        public int Id;

        public static string Format_Impl(in FormattableItemOverride self, string prefix)
            => $"[{prefix}:{self.Id}]"; // Different format to distinguish
    }

    /// <summary>
    /// Trait with a default that calls another method with a parameter.
    /// - Compute(int x) is required
    /// - ComputeDouble(int x) default calls Compute(x) * 2 — passes parameter through
    /// Tests: default method forwarding its own parameter to another method call.
    /// </summary>
    [Trait(GenerateLayout = true)]
    public partial interface IComputable
    {
        int Base { get; }

        /// <summary>Required: compute with input.</summary>
        int Compute(int x);

        /// <summary>Default: double the computation. Forwards parameter x to Compute().</summary>
        int ComputeDouble(int x) => Compute(x) * 2;

        /// <summary>Default: add base to computation. Uses property + forwarded parameter.</summary>
        int ComputePlusBase(int x) => Compute(x) + Base;
    }

    /// <summary>
    /// ComputableItem: implements Compute, relies on ComputeDouble and ComputePlusBase defaults.
    /// Tests: parameter forwarding in chained default dispatch.
    /// </summary>
    [ImplementsTrait(typeof(IComputable))]
    [StructLayout(LayoutKind.Sequential)]
    public partial struct ComputableItem
    {
        public int Base;

        public static int Compute_Impl(in ComputableItem self, int x)
            => self.Base + x;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Phase 10: Generic algorithms for parameterized default traits
    // ─────────────────────────────────────────────────────────────────────

    public static class ParameterizedDefaultAlgorithms
    {
        /// <summary>Generic scaled area using IScalable trait constraint.</summary>
        public static float ScaleArea<T>(ref T item, float factor)
            where T : unmanaged, IScalableTrait<T>
        {
            return item.ScaledArea(factor);
        }

        /// <summary>Generic chain test using IChainable trait constraint.</summary>
        public static string ChainSummary<T>(ref T item)
            where T : unmanaged, IChainableTrait<T>
        {
            return $"base={item.BaseValue()}, x2={item.Doubled()}, x4={item.Quadrupled()}";
        }

        /// <summary>Generic format using IFormattable trait constraint.</summary>
        public static string FormatWith<T>(ref T item, string prefix)
            where T : unmanaged, IFormattableTrait<T>
        {
            return item.Format(prefix);
        }

        /// <summary>Generic compute using IComputable trait constraint.</summary>
        public static string ComputeSummary<T>(ref T item, int x)
            where T : unmanaged, IComputableTrait<T>
        {
            return $"compute({x})={item.Compute(x)}, double={item.ComputeDouble(x)}, plusBase={item.ComputePlusBase(x)}";
        }
    }
}
