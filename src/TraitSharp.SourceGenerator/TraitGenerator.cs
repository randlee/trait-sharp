using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TraitSharp.SourceGenerator.Analyzers;
using TraitSharp.SourceGenerator.Generators;
using TraitSharp.SourceGenerator.Models;
using TraitSharp.SourceGenerator.Utilities;

namespace TraitSharp.SourceGenerator
{
    [Generator(LanguageNames.CSharp)]
    public sealed class TraitGenerator : IIncrementalGenerator
    {
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // 1. Collect trait interfaces
            var traitInterfaces = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    "TraitSharp.TraitAttribute",
                    predicate: (node, _) => node is InterfaceDeclarationSyntax,
                    transform: GetTraitModel)
                .Where(t => t is not null)
                .Select((t, _) => t!);

            // 2. Collect trait implementations
            var implementations = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    "TraitSharp.ImplementsTraitAttribute",
                    predicate: (node, _) => node is StructDeclarationSyntax or ClassDeclarationSyntax,
                    transform: GetImplementationModels)
                .Where(i => i.HasValue)
                .SelectMany((list, _) => list!.Value);

            // 3. Collect external registrations
            var externalImpls = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    "TraitSharp.RegisterTraitImplAttribute",
                    predicate: (node, _) => true,
                    transform: GetExternalImplModel)
                .Where(e => e is not null)
                .Select((e, _) => e!);

            // 4. Combine and generate
            var combined = traitInterfaces
                .Collect()
                .Combine(implementations.Collect())
                .Combine(externalImpls.Collect());

            context.RegisterSourceOutput(combined, GenerateCode);
        }

        private static TraitModel? GetTraitModel(
            GeneratorAttributeSyntaxContext context,
            CancellationToken ct)
        {
            if (context.TargetSymbol is not INamedTypeSymbol interfaceSymbol)
                return null;

            var attr = context.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.Name == "TraitAttribute");
            if (attr == null) return null;

            var model = new TraitModel
            {
                Name = interfaceSymbol.Name,
                FullName = interfaceSymbol.ToDisplayString(),
                Namespace = interfaceSymbol.ContainingNamespace.ToDisplayString(),
                Location = interfaceSymbol.Locations.FirstOrDefault()
            };

            // Parse attribute properties
            foreach (var namedArg in attr.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "GenerateLayout":
                        model.GenerateLayout = (bool)namedArg.Value.Value!;
                        break;
                    case "GenerateExtensions":
                        model.GenerateExtensions = (bool)namedArg.Value.Value!;
                        break;
                    case "GenerateStaticMethods":
                        model.GenerateStaticMethods = (bool)namedArg.Value.Value!;
                        break;
                    case "GeneratedNamespace":
                        model.GeneratedNamespace = namedArg.Value.Value as string;
                        break;
                }
            }

            // Derive layout struct name: strip leading 'I' + "Layout"
            string shortName = model.ShortName;
            model.LayoutStructName = shortName + "Layout";

            // Extract properties from the interface
            foreach (var member in interfaceSymbol.GetMembers())
            {
                if (member is IPropertySymbol propSymbol)
                {
                    model.Properties.Add(new TraitProperty
                    {
                        Name = propSymbol.Name,
                        TypeName = propSymbol.Type.GetMinimalTypeName(),
                        Type = propSymbol.Type,
                        HasGetter = propSymbol.GetMethod != null,
                        HasSetter = propSymbol.SetMethod != null
                    });
                }
            }

            return model;
        }

        private static ImmutableArray<ImplementationModel>? GetImplementationModels(
            GeneratorAttributeSyntaxContext context,
            CancellationToken ct)
        {
            if (context.TargetSymbol is not INamedTypeSymbol typeSymbol)
                return null;

            var results = ImmutableArray.CreateBuilder<ImplementationModel>();

            // A struct can have multiple [ImplementsTrait] attributes
            foreach (var attr in context.Attributes.Where(a =>
                a.AttributeClass?.Name == "ImplementsTraitAttribute"))
            {
                if (attr.ConstructorArguments.Length == 0) continue;
                var traitTypeArg = attr.ConstructorArguments[0];
                if (traitTypeArg.Value is not INamedTypeSymbol traitTypeSymbol) continue;

                var impl = new ImplementationModel
                {
                    TypeName = typeSymbol.Name,
                    FullTypeName = typeSymbol.ToDisplayString(),
                    Namespace = typeSymbol.ContainingNamespace.ToDisplayString(),
                    TypeKind = typeSymbol.IsValueType ? "struct" : "class",
                    TraitInterfaceName = traitTypeSymbol.Name,
                    TraitFullName = traitTypeSymbol.ToDisplayString(),
                    TypeSymbol = typeSymbol,
                    Location = typeSymbol.Locations.FirstOrDefault()
                };

                // Parse Strategy and FieldMapping from named arguments
                foreach (var namedArg in attr.NamedArguments)
                {
                    switch (namedArg.Key)
                    {
                        case "Strategy":
                            impl.Strategy = (ImplStrategy)(int)namedArg.Value.Value!;
                            break;
                        case "FieldMapping":
                            impl.FieldMapping = RoslynExtensions.ParseFieldMapping(namedArg.Value.Value as string);
                            break;
                    }
                }

                // Build the TraitModel from the trait interface symbol
                var traitModel = BuildTraitModelFromSymbol(traitTypeSymbol);
                impl.Trait = traitModel;

                results.Add(impl);
            }

            return results.ToImmutable();
        }

        private static ExternalImplModel? GetExternalImplModel(
            GeneratorAttributeSyntaxContext context,
            CancellationToken ct)
        {
            var attr = context.Attributes.FirstOrDefault(a =>
                a.AttributeClass?.Name == "RegisterTraitImplAttribute");
            if (attr == null || attr.ConstructorArguments.Length < 2) return null;

            var traitTypeSymbol = attr.ConstructorArguments[0].Value as INamedTypeSymbol;
            var targetTypeSymbol = attr.ConstructorArguments[1].Value as INamedTypeSymbol;

            if (traitTypeSymbol == null || targetTypeSymbol == null) return null;

            var model = new ExternalImplModel
            {
                TraitInterfaceName = traitTypeSymbol.Name,
                TraitFullName = traitTypeSymbol.ToDisplayString(),
                TargetTypeName = targetTypeSymbol.Name,
                TargetFullTypeName = targetTypeSymbol.ToDisplayString(),
                TargetNamespace = targetTypeSymbol.ContainingNamespace.ToDisplayString(),
                TargetTypeSymbol = targetTypeSymbol,
                Location = context.TargetSymbol.Locations.FirstOrDefault()
            };

            // Parse Strategy and FieldMapping
            foreach (var namedArg in attr.NamedArguments)
            {
                switch (namedArg.Key)
                {
                    case "Strategy":
                        model.Strategy = (ImplStrategy)(int)namedArg.Value.Value!;
                        break;
                    case "FieldMapping":
                        model.FieldMapping = RoslynExtensions.ParseFieldMapping(namedArg.Value.Value as string);
                        break;
                }
            }

            model.Trait = BuildTraitModelFromSymbol(traitTypeSymbol);

            return model;
        }

        private static TraitModel BuildTraitModelFromSymbol(INamedTypeSymbol traitTypeSymbol)
        {
            var traitModel = new TraitModel
            {
                Name = traitTypeSymbol.Name,
                FullName = traitTypeSymbol.ToDisplayString(),
                Namespace = traitTypeSymbol.ContainingNamespace.ToDisplayString(),
                Location = traitTypeSymbol.Locations.FirstOrDefault()
            };

            // Check for [Trait] attribute on the interface
            var traitAttr = traitTypeSymbol.GetAttributes()
                .FirstOrDefault(a => a.AttributeClass?.Name == "TraitAttribute");
            if (traitAttr != null)
            {
                foreach (var namedArg in traitAttr.NamedArguments)
                {
                    switch (namedArg.Key)
                    {
                        case "GenerateLayout":
                            traitModel.GenerateLayout = (bool)namedArg.Value.Value!;
                            break;
                        case "GenerateExtensions":
                            traitModel.GenerateExtensions = (bool)namedArg.Value.Value!;
                            break;
                        case "GenerateStaticMethods":
                            traitModel.GenerateStaticMethods = (bool)namedArg.Value.Value!;
                            break;
                        case "GeneratedNamespace":
                            traitModel.GeneratedNamespace = namedArg.Value.Value as string;
                            break;
                    }
                }
            }

            string shortName = traitModel.ShortName;
            traitModel.LayoutStructName = shortName + "Layout";

            foreach (var member in traitTypeSymbol.GetMembers())
            {
                if (member is IPropertySymbol propSymbol)
                {
                    traitModel.Properties.Add(new TraitProperty
                    {
                        Name = propSymbol.Name,
                        TypeName = propSymbol.Type.GetMinimalTypeName(),
                        Type = propSymbol.Type,
                        HasGetter = propSymbol.GetMethod != null,
                        HasSetter = propSymbol.SetMethod != null
                    });
                }
            }

            return traitModel;
        }

        private static void GenerateCode(
            SourceProductionContext context,
            ((ImmutableArray<TraitModel>,
              ImmutableArray<ImplementationModel>),
             ImmutableArray<ExternalImplModel>) data)
        {
            var (inner, externalImpls) = data;
            var (traits, impls) = inner;

            // Build trait lookup by full name
            var traitLookup = new Dictionary<string, TraitModel>();
            foreach (var trait in traits)
            {
                traitLookup[trait.FullName] = trait;
            }

            // Generate the marker interface ITrait<TTrait, TSelf> once per namespace
            var markerNamespaces = new HashSet<string>();
            foreach (var trait in traits)
            {
                var ns = trait.EffectiveNamespace;
                if (markerNamespaces.Add(ns))
                {
                    var markerCode = ConstraintInterfaceGenerator.GenerateMarker(ns);
                    // Sanitize namespace for hint name (e.g., "<global namespace>" -> "Global")
                    var safeNs = SanitizeHintName(ns);
                    context.AddSource($"ITrait.{safeNs}.Marker.g.cs", markerCode);
                }
            }

            // Generate trait artifacts
            foreach (var trait in traits)
            {
                // Validate trait interface
                ValidateTrait(context, trait);

                // Generate layout struct
                if (trait.GenerateLayout)
                {
                    var layoutCode = LayoutStructGenerator.Generate(trait);
                    context.AddSource($"{trait.Name}.Layout.g.cs", layoutCode);
                }

                // Generate per-trait contract interface (e.g. ICoordinateTrait<TSelf>)
                var contractCode = ConstraintInterfaceGenerator.GenerateContract(trait);
                context.AddSource($"{trait.Name}.Contract.g.cs", contractCode);

                // Generate extension methods
                if (trait.GenerateExtensions)
                {
                    var extensionCode = ExtensionMethodsGenerator.Generate(trait);
                    context.AddSource($"{trait.Name}.Extensions.g.cs", extensionCode);
                }

                // Generate static methods on interface
                if (trait.GenerateStaticMethods)
                {
                    var staticCode = StaticMethodsGenerator.Generate(trait);
                    context.AddSource($"{trait.Name}.Static.g.cs", staticCode);
                }

                // Generate span factory extension methods
                if (trait.GenerateLayout)
                {
                    var spanFactoryCode = TraitSpanFactoryGenerator.Generate(trait);
                    context.AddSource($"{trait.Name}.SpanFactory.g.cs", spanFactoryCode);
                }
            }

            // Generate implementations
            foreach (var impl in impls)
            {
                if (impl.Trait == null || impl.TypeSymbol == null) continue;

                // Run layout analysis
                var analysis = LayoutCompatibilityAnalyzer.Analyze(
                    impl.TypeSymbol, impl.Trait, impl.FieldMapping,
                    d => context.ReportDiagnostic(d));

                impl.BaseOffset = analysis.BaseOffset;
                impl.IsLayoutCompatible = analysis.IsCompatible;

                if (analysis.IsCompatible)
                {
                    var implCode = ImplementationGenerator.Generate(impl);
                    context.AddSource($"{impl.TypeName}.{impl.Trait.Name}.TraitImpl.g.cs", implCode);
                }
            }

            // Generate external adapters
            foreach (var external in externalImpls)
            {
                if (external.Trait == null || external.TargetTypeSymbol == null) continue;

                // Run layout analysis on external type
                var analysis = LayoutCompatibilityAnalyzer.Analyze(
                    external.TargetTypeSymbol, external.Trait, external.FieldMapping,
                    d => context.ReportDiagnostic(d));

                external.BaseOffset = analysis.BaseOffset;
                external.IsLayoutCompatible = analysis.IsCompatible;

                if (analysis.IsCompatible)
                {
                    var adapterCode = ImplementationGenerator.GenerateExternal(external);
                    context.AddSource($"{external.TargetTypeName}.{external.Trait.Name}.ExternalImpl.g.cs", adapterCode);
                }
            }
        }

        private static void ValidateTrait(SourceProductionContext context, TraitModel trait)
        {
            foreach (var prop in trait.Properties)
            {
                if (!prop.HasGetter)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.TE0007_PropertyMustHaveGetter,
                        trait.Location,
                        prop.Name, trait.Name));
                }
            }
        }

        /// <summary>
        /// Sanitizes a namespace string for use in Roslyn source hint names.
        /// Hint names cannot contain characters like &lt; or &gt;.
        /// </summary>
        private static string SanitizeHintName(string ns)
        {
            if (string.IsNullOrEmpty(ns) || ns.Contains("<") || ns.Contains(">"))
                return "Global";
            return ns;
        }
    }
}
