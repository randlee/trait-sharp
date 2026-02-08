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
    }
}
