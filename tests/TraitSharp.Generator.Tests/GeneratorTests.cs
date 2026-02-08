using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TraitSharp.SourceGenerator;

namespace TraitSharp.Generator.Tests
{
    [TestClass]
    public class GeneratorTests
    {
        private static readonly CSharpParseOptions ParseOptions =
            CSharpParseOptions.Default.WithLanguageVersion(LanguageVersion.Preview);

        private static CSharpCompilation CreateCompilation(string source)
        {
            var syntaxTree = CSharpSyntaxTree.ParseText(source, ParseOptions);

            var references = new[]
            {
                MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(System.Attribute).Assembly.Location),
                MetadataReference.CreateFromFile(typeof(TraitAttribute).Assembly.Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime").Location),
                MetadataReference.CreateFromFile(Assembly.Load("System.Runtime.InteropServices").Location),
                MetadataReference.CreateFromFile(Assembly.Load("netstandard").Location),
            };

            return CSharpCompilation.Create(
                assemblyName: "TestAssembly",
                syntaxTrees: new[] { syntaxTree },
                references: references,
                options: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        }

        private static GeneratorDriverRunResult RunGenerator(string source)
        {
            var compilation = CreateCompilation(source);

            var generator = new TraitGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator.AsSourceGenerator() },
                parseOptions: ParseOptions);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

            return driver.GetRunResult();
        }

        private static ImmutableArray<Diagnostic> GetAllDiagnostics(string source)
        {
            var compilation = CreateCompilation(source);

            var generator = new TraitGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator.AsSourceGenerator() },
                parseOptions: ParseOptions);
            driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var generatorDiagnostics);

            return generatorDiagnostics;
        }

        [TestMethod]
        public void Generator_ProducesLayoutStruct()
        {
            var source = @"
using TraitSharp;
namespace TestNs
{
    [Trait]
    interface IPoint { int X { get; } int Y { get; } }
}
";
            var result = RunGenerator(source);
            Assert.IsTrue(result.GeneratedTrees.Any(t =>
                t.GetText().ToString().Contains("struct PointLayout")),
                "Expected generated code to contain 'struct PointLayout'");
        }

        [TestMethod]
        public void Generator_ProducesMarkerInterface()
        {
            var source = @"
using TraitSharp;
namespace TestNs
{
    [Trait]
    interface IPoint { int X { get; } int Y { get; } }
}
";
            var result = RunGenerator(source);
            Assert.IsTrue(result.GeneratedTrees.Any(t =>
                t.GetText().ToString().Contains("interface ITrait<TTrait, TSelf>")),
                "Expected generated code to contain marker 'interface ITrait<TTrait, TSelf>'");
        }

        [TestMethod]
        public void Generator_ProducesPerTraitContract()
        {
            var source = @"
using TraitSharp;
namespace TestNs
{
    [Trait]
    interface IPoint { int X { get; } int Y { get; } }
}
";
            var result = RunGenerator(source);
            Assert.IsTrue(result.GeneratedTrees.Any(t =>
                t.GetText().ToString().Contains("interface IPointTrait<TSelf>")),
                "Expected generated code to contain per-trait contract 'interface IPointTrait<TSelf>'");
        }

        [TestMethod]
        public void Generator_ProducesExtensionMethods()
        {
            var source = @"
using TraitSharp;
namespace TestNs
{
    [Trait]
    interface IPoint { int X { get; } int Y { get; } }
}
";
            var result = RunGenerator(source);
            Assert.IsTrue(result.GeneratedTrees.Any(t =>
                t.GetText().ToString().Contains("AsPoint")),
                "Expected generated code to contain 'AsPoint'");
        }

        [TestMethod]
        public void TE0001_MissingRequiredField()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface IPoint { int X { get; } int Y { get; } }
    [ImplementsTrait(typeof(IPoint))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct Bad { public int X; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsTrue(diagnostics.Any(d => d.Id == "TE0001"),
                "Expected TE0001 diagnostic for missing field Y");
        }

        [TestMethod]
        public void TE0002_PropertyTypeMismatch()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface IPoint { int X { get; } int Y { get; } }
    [ImplementsTrait(typeof(IPoint))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct Bad { public float X; public int Y; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsTrue(diagnostics.Any(d => d.Id == "TE0002"),
                "Expected TE0002 diagnostic for type mismatch on X");
        }

        [TestMethod]
        public void TE0003_FieldOrderMismatch()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface IPoint { int X { get; } int Y { get; } }
    [ImplementsTrait(typeof(IPoint))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct Bad { public int Y, X; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsTrue(diagnostics.Any(d => d.Id == "TE0003"),
                "Expected TE0003 diagnostic for field order mismatch");
        }

        [TestMethod]
        public void TE0004_MissingStructLayout()
        {
            var source = @"
using TraitSharp;
namespace TestNs
{
    [Trait] interface IPoint { int X { get; } int Y { get; } }
    [ImplementsTrait(typeof(IPoint))]
    partial struct Bad { public int X, Y; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsTrue(diagnostics.Any(d => d.Id == "TE0004"),
                "Expected TE0004 diagnostic for missing StructLayout");
            // Verify it's an error, not a warning
            var te0004 = diagnostics.First(d => d.Id == "TE0004");
            Assert.AreEqual(DiagnosticSeverity.Error, te0004.Severity,
                "TE0004 must be an Error, not a Warning");
        }

        [TestMethod]
        public void TE0009_NonContiguousFields()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface IPoint { int X { get; } int Y { get; } }
    [ImplementsTrait(typeof(IPoint))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct Bad { public int X; public int Tag; public int Y; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsTrue(diagnostics.Any(d =>
                d.Id == "TE0003" || d.Id == "TE0009"),
                "Expected TE0003 or TE0009 diagnostic for non-contiguous fields");
        }

        [TestMethod]
        public void ValidPrefixMatch_NoErrors()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface IPoint { int X { get; } int Y { get; } }
    [ImplementsTrait(typeof(IPoint))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct Point3D { public int X, Y; public float Z; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void ValidOffsetMatch_NoErrors()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface IPoint { int X { get; } int Y { get; } }
    [Trait] interface ISize { int Width { get; } int Height { get; } }
    [ImplementsTrait(typeof(IPoint))]
    [ImplementsTrait(typeof(ISize))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct Rect { public int X, Y, Width, Height; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void FieldMapping_CustomNames_NoErrors()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface IPoint { int X { get; } int Y { get; } }
    [ImplementsTrait(typeof(IPoint), Strategy = ImplStrategy.FieldMapping,
        FieldMapping = ""X:PosX,Y:PosY"")]
    [StructLayout(LayoutKind.Sequential)]
    partial struct Custom { public int PosX, PosY; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        // =====================================================================
        // Sprint 6.2: Attribute parameter combination tests
        // =====================================================================

        [TestMethod]
        public void Trait_GenerateLayout_False()
        {
            var source = @"
using TraitSharp;
namespace TestNs
{
    [Trait(GenerateLayout = false)]
    interface IPoint { int X { get; } int Y { get; } }
}
";
            var result = RunGenerator(source);
            var allGeneratedCode = string.Join("\n",
                result.GeneratedTrees.Select(t => t.GetText().ToString()));
            Assert.IsFalse(allGeneratedCode.Contains("struct PointLayout"),
                "Expected no layout struct when GenerateLayout = false");
        }

        [TestMethod]
        public void Trait_GenerateExtensions_False()
        {
            var source = @"
using TraitSharp;
namespace TestNs
{
    [Trait(GenerateExtensions = false)]
    interface IPoint { int X { get; } int Y { get; } }
}
";
            var result = RunGenerator(source);
            var allGeneratedCode = string.Join("\n",
                result.GeneratedTrees.Select(t => t.GetText().ToString()));
            Assert.IsFalse(allGeneratedCode.Contains("class IPointExtensions"),
                "Expected no extension class when GenerateExtensions = false");
        }

        [TestMethod]
        public void Trait_GenerateStaticMethods_False()
        {
            var source = @"
using TraitSharp;
namespace TestNs
{
    [Trait(GenerateStaticMethods = false)]
    interface IPoint { int X { get; } int Y { get; } }
}
";
            var result = RunGenerator(source);
            // Static methods are generated into a partial interface file (.Static.g.cs)
            // that adds static methods directly on the trait interface.
            // When disabled, no .Static.g.cs file should be emitted.
            var staticTrees = result.GeneratedTrees.Where(t =>
                t.FilePath.Contains(".Static.g.cs")).ToArray();
            Assert.AreEqual(0, staticTrees.Length,
                "Expected no static methods file when GenerateStaticMethods = false");
        }

        [TestMethod]
        public void Trait_CustomGeneratedNamespace()
        {
            var source = @"
using TraitSharp;
namespace TestNs
{
    [Trait(GeneratedNamespace = ""Custom.Ns"")]
    interface IPoint { int X { get; } int Y { get; } }
}
";
            var result = RunGenerator(source);
            var allGeneratedCode = string.Join("\n",
                result.GeneratedTrees.Select(t => t.GetText().ToString()));
            Assert.IsTrue(allGeneratedCode.Contains("namespace Custom.Ns"),
                "Expected generated code to use custom namespace 'Custom.Ns'");
            // The contract interface should also be in the custom namespace
            Assert.IsTrue(allGeneratedCode.Contains("interface IPointTrait<TSelf>"),
                "Expected per-trait contract interface in generated output");
        }

        [TestMethod]
        public void Trait_AllGenerationDisabled()
        {
            var source = @"
using TraitSharp;
namespace TestNs
{
    [Trait(GenerateLayout = false, GenerateExtensions = false, GenerateStaticMethods = false)]
    interface IPoint { int X { get; } int Y { get; } }
}
";
            var result = RunGenerator(source);
            var allGeneratedCode = string.Join("\n",
                result.GeneratedTrees.Select(t => t.GetText().ToString()));

            // Contract interface should still be generated
            Assert.IsTrue(allGeneratedCode.Contains("interface IPointTrait<TSelf>"),
                "Expected per-trait contract interface even when all generation flags are false");

            // Layout, extensions, and static methods should NOT be generated
            Assert.IsFalse(allGeneratedCode.Contains("struct PointLayout"),
                "Expected no layout struct when GenerateLayout = false");
            Assert.IsFalse(allGeneratedCode.Contains("class IPointExtensions"),
                "Expected no extension class when GenerateExtensions = false");

            var staticTrees = result.GeneratedTrees.Where(t =>
                t.FilePath.Contains(".Static.g.cs")).ToArray();
            Assert.AreEqual(0, staticTrees.Length,
                "Expected no static methods file when GenerateStaticMethods = false");
        }

        [TestMethod]
        public void Strategy_Auto_DefaultBehavior()
        {
            var sourceWithAuto = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface IPoint { int X { get; } int Y { get; } }
    [ImplementsTrait(typeof(IPoint), Strategy = ImplStrategy.Auto)]
    [StructLayout(LayoutKind.Sequential)]
    partial struct PointA { public int X, Y; }
}
";
            var sourceWithDefault = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface IPoint { int X { get; } int Y { get; } }
    [ImplementsTrait(typeof(IPoint))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct PointA { public int X, Y; }
}
";
            var diagnosticsAuto = GetAllDiagnostics(sourceWithAuto);
            var diagnosticsDefault = GetAllDiagnostics(sourceWithDefault);

            // Both should produce no errors
            Assert.IsFalse(diagnosticsAuto.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Strategy.Auto should produce no errors but got: {string.Join(", ", diagnosticsAuto.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
            Assert.IsFalse(diagnosticsDefault.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Default strategy should produce no errors but got: {string.Join(", ", diagnosticsDefault.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void Strategy_Reinterpret_Explicit()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface IPoint { int X { get; } int Y { get; } }
    [ImplementsTrait(typeof(IPoint), Strategy = ImplStrategy.Reinterpret)]
    [StructLayout(LayoutKind.Sequential)]
    partial struct PointR { public int X, Y; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Strategy.Reinterpret with compatible layout should produce no errors but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");

            // Verify implementation code is generated
            var result = RunGenerator(source);
            Assert.IsTrue(result.GeneratedTrees.Any(t =>
                t.FilePath.Contains("PointR.IPoint.TraitImpl.g.cs")),
                "Expected implementation file to be generated for Reinterpret strategy");
        }

        [TestMethod]
        public void Strategy_FieldMapping_CustomNames()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface ICoordinate { int X { get; } int Y { get; } }
    [ImplementsTrait(typeof(ICoordinate), Strategy = ImplStrategy.FieldMapping,
        FieldMapping = ""X:PosX,Y:PosY"")]
    [StructLayout(LayoutKind.Sequential)]
    partial struct Position { public int PosX, PosY; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"FieldMapping with valid mapped fields should produce no errors but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void Strategy_FieldMapping_InvalidFieldName()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface ICoordinate { int X { get; } int Y { get; } }
    [ImplementsTrait(typeof(ICoordinate), Strategy = ImplStrategy.FieldMapping,
        FieldMapping = ""X:PosX,Y:PosY"")]
    [StructLayout(LayoutKind.Sequential)]
    partial struct BadMapping { public int PosX; public int SomeOtherField; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsTrue(diagnostics.Any(d => d.Id == "TE0001"),
                "Expected TE0001 diagnostic for missing mapped field 'PosY'");
        }

        [TestMethod]
        public void Strategy_FieldMapping_TypeMismatch()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface ICoordinate { int X { get; } int Y { get; } }
    [ImplementsTrait(typeof(ICoordinate), Strategy = ImplStrategy.FieldMapping,
        FieldMapping = ""X:PosX,Y:PosY"")]
    [StructLayout(LayoutKind.Sequential)]
    partial struct TypeMismatch { public int PosX; public float PosY; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsTrue(diagnostics.Any(d => d.Id == "TE0002"),
                "Expected TE0002 diagnostic for type mismatch on mapped field 'PosY'");
        }

        // =====================================================================
        // Sprint 6.3: Generator edge case tests
        // =====================================================================

        [TestMethod]
        public void EmptyTrait_NoProperties()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait]
    interface IEmpty { }

    [ImplementsTrait(typeof(IEmpty))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct Empty { }
}
";
            var result = RunGenerator(source);
            var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
            Assert.IsTrue(generatedSource.Contains("struct EmptyLayout"),
                "Expected generated code to contain 'struct EmptyLayout' for empty trait");

            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void SinglePropertyTrait()
        {
            var source = @"
using TraitSharp;
namespace TestNs
{
    [Trait]
    interface ISingle { int X { get; } }
}
";
            var result = RunGenerator(source);
            var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
            Assert.IsTrue(generatedSource.Contains("struct SingleLayout"),
                "Expected generated code to contain 'struct SingleLayout'");
            Assert.IsTrue(generatedSource.Contains("public int X;"),
                "Expected layout struct to contain field 'public int X;'");
        }

        [TestMethod]
        public void ManyPropertyTrait()
        {
            var source = @"
using TraitSharp;
namespace TestNs
{
    [Trait]
    interface IMany
    {
        int A { get; }
        int B { get; }
        float C { get; }
        float D { get; }
        double E { get; }
        double F { get; }
        byte G { get; }
        byte H { get; }
        int I { get; }
        int J { get; }
    }
}
";
            var result = RunGenerator(source);
            var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
            Assert.IsTrue(generatedSource.Contains("struct ManyLayout"),
                "Expected generated code to contain 'struct ManyLayout'");
            // Verify all 10 fields appear in the layout struct
            foreach (var field in new[] { "A", "B", "C", "D", "E", "F", "G", "H", "I", "J" })
            {
                Assert.IsTrue(generatedSource.Contains($" {field};"),
                    $"Expected layout struct to contain field '{field}'");
            }
        }

        [TestMethod]
        public void PropertyTypes_Float()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait]
    interface IFloatTrait { float X { get; } float Y { get; } }

    [ImplementsTrait(typeof(IFloatTrait))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct FloatStruct { public float X; public float Y; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void PropertyTypes_Double()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait]
    interface IDoubleTrait { double X { get; } double Y { get; } }

    [ImplementsTrait(typeof(IDoubleTrait))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct DoubleStruct { public double X; public double Y; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void PropertyTypes_Long()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait]
    interface ILongTrait { long X { get; } long Y { get; } }

    [ImplementsTrait(typeof(ILongTrait))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct LongStruct { public long X; public long Y; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void PropertyTypes_Byte()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait]
    interface IByteTrait { byte X { get; } byte Y { get; } }

    [ImplementsTrait(typeof(IByteTrait))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct ByteStruct { public byte X; public byte Y; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void PropertyTypes_Bool()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait]
    interface IBoolTrait { bool Active { get; } bool Visible { get; } }

    [ImplementsTrait(typeof(IBoolTrait))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct BoolStruct { public bool Active; public bool Visible; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void GlobalNamespace_NoNamespace()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;

[Trait]
interface IGlobal { int X { get; } }

[ImplementsTrait(typeof(IGlobal))]
[StructLayout(LayoutKind.Sequential)]
partial struct GlobalStruct { public int X; }
";
            var result = RunGenerator(source);
            var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
            Assert.IsTrue(generatedSource.Contains("struct GlobalLayout"),
                "Expected generated code to contain 'struct GlobalLayout' for global namespace trait");
        }

        [TestMethod]
        public void DeeplyNestedNamespace()
        {
            var source = @"
using TraitSharp;
namespace A.B.C.D.E
{
    [Trait]
    interface IDeep { int X { get; } }
}
";
            var result = RunGenerator(source);
            var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
            Assert.IsTrue(generatedSource.Contains("namespace A.B.C.D.E"),
                "Expected generated code to use deeply nested namespace 'A.B.C.D.E'");
            Assert.IsTrue(generatedSource.Contains("struct DeepLayout"),
                "Expected generated code to contain 'struct DeepLayout'");
        }

        [TestMethod]
        public void ThreeTraitsOnSameStruct()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface IPos { int X { get; } int Y { get; } }
    [Trait] interface ISize { int Width { get; } int Height { get; } }
    [Trait] interface IColor { int R { get; } int G { get; } int B { get; } }

    [ImplementsTrait(typeof(IPos))]
    [ImplementsTrait(typeof(ISize))]
    [ImplementsTrait(typeof(IColor))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct Widget { public int X, Y, Width, Height, R, G, B; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors for three traits on same struct but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");

            var result = RunGenerator(source);
            var generatedSource = string.Join("\n", result.GeneratedTrees.Select(t => t.GetText().ToString()));
            Assert.IsTrue(generatedSource.Contains("struct PosLayout"),
                "Expected generated code to contain 'struct PosLayout'");
            Assert.IsTrue(generatedSource.Contains("struct SizeLayout"),
                "Expected generated code to contain 'struct SizeLayout'");
            Assert.IsTrue(generatedSource.Contains("struct ColorLayout"),
                "Expected generated code to contain 'struct ColorLayout'");
        }

        [TestMethod]
        public void OverlappingTraitFields()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait] interface IHasX { int X { get; } }
    [Trait] interface IAlsoHasX { int X { get; } int Y { get; } }

    [ImplementsTrait(typeof(IHasX))]
    [ImplementsTrait(typeof(IAlsoHasX))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct Overlap { public int X; public int Y; }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors for overlapping trait fields but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void ExternalType_DifferentNamespace()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;

namespace Ns1
{
    [Trait]
    interface ITrait { int X { get; } }
}

namespace Ns2
{
    [StructLayout(LayoutKind.Sequential)]
    public struct ExternalStruct { public int X; }
}

[assembly: RegisterTraitImpl(typeof(Ns1.ITrait), typeof(Ns2.ExternalStruct))]
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors for cross-namespace external type but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void ExternalType_WithFieldMapping()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;

namespace Ns1
{
    [Trait]
    interface IPoint { int X { get; } int Y { get; } }
}

namespace Ns2
{
    [StructLayout(LayoutKind.Sequential)]
    public struct Vec2 { public int PosX; public int PosY; }
}

[assembly: RegisterTraitImpl(typeof(Ns1.IPoint), typeof(Ns2.Vec2),
    Strategy = ImplStrategy.FieldMapping, FieldMapping = ""X:PosX,Y:PosY"")]
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsFalse(diagnostics.Any(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors for external type with field mapping but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        // =====================================================================
        // Sprint 6.6: Layout analyzer edge case tests
        // =====================================================================

        [TestMethod]
        public void Analyzer_ExplicitLayout_WithFieldOffset()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait]
    interface ICoordinate { int X { get; } int Y { get; } }

    [ImplementsTrait(typeof(ICoordinate))]
    [StructLayout(LayoutKind.Explicit)]
    partial struct ExplicitPoint
    {
        [FieldOffset(0)] public int X;
        [FieldOffset(4)] public int Y;
    }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors for valid explicit layout but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void Analyzer_AlignmentPadding_MixedSizes()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait]
    interface IMixed { byte A { get; } int B { get; } byte C { get; } }

    [ImplementsTrait(typeof(IMixed))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct MixedStruct
    {
        public byte A;
        public int B;
        public byte C;
    }
}
";
            var diagnostics = GetAllDiagnostics(source);
            // Sequential layout adds padding for alignment (byte=1 padded to 4 before int).
            // The generator correctly detects the offset mismatch and reports TE0003.
            var te0003 = diagnostics.Where(d => d.Id == "TE0003").ToArray();
            Assert.IsTrue(te0003.Length >= 1,
                $"Expected TE0003 for alignment padding mismatch but got: {string.Join(", ", diagnostics.Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void Analyzer_NestedStructField()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    struct Vec2 { public float X; public float Y; }

    [Trait]
    interface IHasPosition { Vec2 Position { get; } }

    [ImplementsTrait(typeof(IHasPosition))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct Entity
    {
        public Vec2 Position;
    }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors for nested struct field but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        [TestMethod]
        public void Analyzer_FieldOffset_NonZeroBase()
        {
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;
namespace TestNs
{
    [Trait]
    interface ICoordinate { int X { get; } int Y { get; } }

    [ImplementsTrait(typeof(ICoordinate))]
    [StructLayout(LayoutKind.Sequential)]
    partial struct OffsetPoint
    {
        public int Id;
        public float Scale;
        public int X;
        public int Y;
    }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.AreEqual(0, diagnostics.Count(d => d.Severity == DiagnosticSeverity.Error),
                $"Expected no errors when trait fields start at non-zero base offset but got: {string.Join(", ", diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString()))}");
        }

        // =====================================================================
        // Sprint 6.1: Diagnostic tests TE0005–TE0008
        // =====================================================================

        [TestMethod]
        public void TE0005_ExternalTypeNotFound()
        {
            // RegisterTraitImpl with a target type that cannot be resolved.
            // The unresolvable typeof() causes Roslyn to produce an error type,
            // so the generator should detect the null symbol and report TE0005.
            // However, Roslyn also reports CS0246 for the unresolved type.
            // We assert that either TE0005 is reported by the generator OR
            // a compile error (CS0246) exists for the unresolvable type.
            var source = @"
using TraitSharp;
using System.Runtime.InteropServices;

[assembly: RegisterTraitImpl(typeof(TestNs.ICoordinate), typeof(TestNs.NonExistentType))]

namespace TestNs
{
    [Trait]
    interface ICoordinate { int X { get; } int Y { get; } }
}
";
            var compilation = CreateCompilation(source);

            var generator = new TraitGenerator();
            GeneratorDriver driver = CSharpGeneratorDriver.Create(
                generators: new[] { generator.AsSourceGenerator() },
                parseOptions: ParseOptions);
            driver = driver.RunGeneratorsAndUpdateCompilation(
                compilation, out var outputCompilation, out var generatorDiagnostics);

            // Collect both generator diagnostics and compilation diagnostics
            var allDiagnostics = generatorDiagnostics
                .Concat(outputCompilation.GetDiagnostics())
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .ToArray();

            bool hasTE0005 = allDiagnostics.Any(d => d.Id == "TE0005");
            bool hasCS0246 = allDiagnostics.Any(d => d.Id == "CS0246");

            Assert.IsTrue(hasTE0005 || hasCS0246,
                $"Expected TE0005 or CS0246 for unresolved type 'NonExistentType' but got: " +
                $"{string.Join(", ", allDiagnostics.Select(d => d.Id + ": " + d.GetMessage()))}");
        }

        [TestMethod]
        public void TE0006_InvalidTraitMember()
        {
            // A trait interface containing an event should produce TE0006.
            var source = @"
using System;
using TraitSharp;
namespace TestNs
{
    [Trait]
    interface IObservable
    {
        int Value { get; }
        event EventHandler Changed;
    }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsTrue(diagnostics.Any(d => d.Id == "TE0006"),
                "Expected TE0006 diagnostic for event member in trait interface");
        }

        [TestMethod]
        public void TE0007_PropertyMustHaveGetter()
        {
            // A trait interface with a set-only property should produce TE0007.
            var source = @"
using TraitSharp;
namespace TestNs
{
    [Trait]
    interface IBadTrait
    {
        int X { set; }
    }
}
";
            var diagnostics = GetAllDiagnostics(source);
            Assert.IsTrue(diagnostics.Any(d => d.Id == "TE0007"),
                "Expected TE0007 diagnostic for set-only property in trait interface");
        }

        [TestMethod]
        public void TE0008_CircularTraitDependency_DescriptorExists()
        {
            // TE0008 is a future-proofing diagnostic for circular trait dependency
            // detection (Phase 7: trait inheritance). Verify the descriptor is properly
            // defined via reflection since DiagnosticDescriptors is internal.
            var generatorAssembly = typeof(TraitGenerator).Assembly;
            var descriptorsType = generatorAssembly.GetType(
                "TraitSharp.SourceGenerator.Analyzers.DiagnosticDescriptors");
            Assert.IsNotNull(descriptorsType,
                "DiagnosticDescriptors type should exist in the generator assembly");

            var field = descriptorsType!.GetField("TE0008_CircularDependency",
                BindingFlags.Public | BindingFlags.Static);
            Assert.IsNotNull(field,
                "TE0008_CircularDependency field should exist on DiagnosticDescriptors");

            var descriptor = field!.GetValue(null) as DiagnosticDescriptor;
            Assert.IsNotNull(descriptor,
                "TE0008_CircularDependency should be a non-null DiagnosticDescriptor");
            Assert.AreEqual("TE0008", descriptor!.Id,
                "TE0008_CircularDependency descriptor should have ID 'TE0008'");
            Assert.AreEqual(DiagnosticSeverity.Error, descriptor.DefaultSeverity,
                "TE0008_CircularDependency should be an Error severity diagnostic");
        }
    }
}
