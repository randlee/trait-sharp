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

            // Extract properties from the interface and detect invalid members
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
                else if (member is IMethodSymbol methodSymbol)
                {
                    // Skip property accessors (get_X, set_X) — they are reported as methods
                    if (methodSymbol.MethodKind == MethodKind.PropertyGet ||
                        methodSymbol.MethodKind == MethodKind.PropertySet)
                        continue;

                    // Skip event accessors (add_X, remove_X)
                    if (methodSymbol.MethodKind == MethodKind.EventAdd ||
                        methodSymbol.MethodKind == MethodKind.EventRemove)
                        continue;

                    // Regular methods are allowed — traits may support methods in future
                }
                else if (member is IEventSymbol eventSymbol)
                {
                    // TE0006: events are not valid trait members
                    model.InvalidMembers.Add(eventSymbol.Name);
                }
                else if (member is INamedTypeSymbol)
                {
                    // TE0006: nested types are not valid trait members
                    model.InvalidMembers.Add(member.Name);
                }
            }

            // Resolve trait inheritance: collect base traits and merge properties
            ResolveBaseTraits(interfaceSymbol, model, new HashSet<string>());
            BuildAllProperties(model);
            if (model.AllProperties.Count > 0 && model.HasBaseTraits)
            {
                model.Properties = new List<TraitProperty>(model.AllProperties);
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

            if (traitTypeSymbol == null) return null;

            // TE0005: target type could not be resolved
            if (targetTypeSymbol == null)
            {
                // Extract the type name from the attribute syntax for diagnostic reporting
                var targetTypeArg = attr.ConstructorArguments[1];
                var unresolvedName = targetTypeArg.Value?.ToString() ?? "unknown";

                // If the argument is an error type, try to extract the name from the syntax
                if (attr.ApplicationSyntaxReference?.GetSyntax(ct) is AttributeSyntax attrSyntax
                    && attrSyntax.ArgumentList?.Arguments.Count >= 2)
                {
                    var secondArg = attrSyntax.ArgumentList.Arguments[1];
                    unresolvedName = secondArg.Expression.ToString()
                        .Replace("typeof(", "").TrimEnd(')');
                }

                return new ExternalImplModel
                {
                    TraitInterfaceName = traitTypeSymbol.Name,
                    TraitFullName = traitTypeSymbol.ToDisplayString(),
                    UnresolvedTargetTypeName = unresolvedName,
                    Location = context.TargetSymbol.Locations.FirstOrDefault()
                };
            }

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
                else if (member is IMethodSymbol methodSymbol)
                {
                    // Skip property/event accessors
                    if (methodSymbol.MethodKind == MethodKind.PropertyGet ||
                        methodSymbol.MethodKind == MethodKind.PropertySet ||
                        methodSymbol.MethodKind == MethodKind.EventAdd ||
                        methodSymbol.MethodKind == MethodKind.EventRemove)
                        continue;
                }
                else if (member is IEventSymbol eventSymbol)
                {
                    traitModel.InvalidMembers.Add(eventSymbol.Name);
                }
                else if (member is INamedTypeSymbol)
                {
                    traitModel.InvalidMembers.Add(member.Name);
                }
            }

            // Resolve trait inheritance for implementation-side models too
            ResolveBaseTraits(traitTypeSymbol, traitModel, new HashSet<string>());
            BuildAllProperties(traitModel);
            if (traitModel.AllProperties.Count > 0 && traitModel.HasBaseTraits)
            {
                traitModel.Properties = new List<TraitProperty>(traitModel.AllProperties);
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
                // TE0005: external target type could not be resolved
                if (external.UnresolvedTargetTypeName != null)
                {
                    context.ReportDiagnostic(Diagnostic.Create(
                        DiagnosticDescriptors.TE0005_ExternalTypeNotFound,
                        external.Location,
                        external.UnresolvedTargetTypeName));
                    continue;
                }

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
            // TE0006: invalid trait members (events, nested types, etc.)
            if (trait.InvalidMembers.Count > 0)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    DiagnosticDescriptors.TE0006_InvalidTraitMember,
                    trait.Location,
                    trait.Name));
            }

            // TE0007: properties without getters
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

            // TE0010: ambiguous inherited fields (same name, different types across base traits)
            if (trait.HasBaseTraits)
            {
                var propTypes = new Dictionary<string, (string TypeName, string SourceTrait)>();
                foreach (var baseTrait in trait.BaseTraits)
                {
                    var baseProps = baseTrait.AllProperties.Count > 0
                        ? baseTrait.AllProperties
                        : baseTrait.Properties;
                    foreach (var prop in baseProps)
                    {
                        if (propTypes.TryGetValue(prop.Name, out var existing))
                        {
                            if (existing.TypeName != prop.TypeName)
                            {
                                context.ReportDiagnostic(Diagnostic.Create(
                                    DiagnosticDescriptors.TE0010_AmbiguousInheritedField,
                                    trait.Location,
                                    prop.Name, trait.Name,
                                    existing.TypeName, existing.SourceTrait,
                                    prop.TypeName, baseTrait.Name));
                            }
                        }
                        else
                        {
                            propTypes[prop.Name] = (prop.TypeName, baseTrait.Name);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Recursively resolves base traits (interfaces annotated with [Trait]) for trait inheritance.
        /// Uses a visited set to detect and break circular references.
        /// </summary>
        private static void ResolveBaseTraits(INamedTypeSymbol interfaceSymbol, TraitModel model, HashSet<string> visited)
        {
            if (!visited.Add(model.FullName))
                return; // Circular — already visited

            foreach (var baseInterface in interfaceSymbol.Interfaces)
            {
                var traitAttr = baseInterface.GetAttributes()
                    .FirstOrDefault(a => a.AttributeClass?.Name == "TraitAttribute");
                if (traitAttr == null) continue;

                var baseModel = BuildTraitModelFromSymbol(baseInterface);
                model.BaseTraits.Add(baseModel);
            }
        }

        /// <summary>
        /// Builds the merged AllProperties list by collecting inherited properties depth-first,
        /// deduplicating by name (diamond inheritance), then appending own direct properties.
        /// </summary>
        private static void BuildAllProperties(TraitModel model)
        {
            if (!model.HasBaseTraits) return;

            model.AllProperties.Clear();
            var seen = new Dictionary<string, (TraitProperty Prop, string SourceTrait)>();

            // First: inherited properties depth-first
            foreach (var baseTrait in model.BaseTraits)
            {
                var baseProps = baseTrait.AllProperties.Count > 0
                    ? baseTrait.AllProperties
                    : baseTrait.Properties;
                foreach (var prop in baseProps)
                {
                    if (seen.TryGetValue(prop.Name, out var existing))
                        continue; // Diamond dedup: first definition wins
                    seen[prop.Name] = (prop, baseTrait.Name);
                    model.AllProperties.Add(prop);
                }
            }

            // Then: own direct properties
            foreach (var prop in model.Properties)
            {
                if (seen.TryGetValue(prop.Name, out var existing))
                    continue; // Already inherited
                seen[prop.Name] = (prop, model.Name);
                model.AllProperties.Add(prop);
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
