using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using TraitSharp.SourceGenerator.Models;

namespace TraitSharp.SourceGenerator.Utilities
{
    /// <summary>
    /// Rewrites a default method body from trait-interface syntax to implementation-compatible syntax.
    /// Transforms:
    /// - Property accesses (e.g., X, Y) → T.GetX_Impl(in self)
    /// - Method calls on trait (e.g., Describe()) → T.Describe_Impl(in self, ...)
    /// - 'this' references → 'self'
    /// </summary>
    internal static class DefaultBodyRewriter
    {
        /// <summary>
        /// Rewrites a default method body string, replacing trait property accesses and method calls
        /// with static dispatch calls suitable for the implementing type's _Impl method.
        /// </summary>
        /// <param name="bodySyntax">The raw body syntax text (block form, e.g., "{ return X + Y; }")</param>
        /// <param name="trait">The trait model containing property and method definitions</param>
        /// <param name="implTypeName">The implementing type name (used in type parameter)</param>
        /// <returns>The rewritten body text, or null if parsing/rewriting fails</returns>
        public static string? Rewrite(string bodySyntax, TraitModel trait, string implTypeName)
        {
            // Parse the body as a block statement
            var tree = CSharpSyntaxTree.ParseText(
                $"class _Wrapper_ {{ void _M_() {bodySyntax} }}",
                new CSharpParseOptions(LanguageVersion.Latest));

            var root = tree.GetRoot();
            var methodDecl = root.DescendantNodes().OfType<MethodDeclarationSyntax>().FirstOrDefault();
            if (methodDecl?.Body == null) return null;

            // Build lookup sets for properties and methods
            var propNames = new HashSet<string>(trait.Properties.Select(p => p.Name));
            var methodLookup = new Dictionary<string, TraitMethod>();
            foreach (var m in trait.Methods)
            {
                // Key by method name (overloads handled separately if needed)
                if (!methodLookup.ContainsKey(m.Name))
                    methodLookup[m.Name] = m;
            }

            // Apply the rewriter
            var rewriter = new BodySyntaxRewriter(propNames, methodLookup, implTypeName);
            var rewritten = rewriter.Visit(methodDecl.Body);

            if (rewritten is BlockSyntax block)
            {
                return block.NormalizeWhitespace().ToFullString();
            }

            return null;
        }

        private sealed class BodySyntaxRewriter : CSharpSyntaxRewriter
        {
            private readonly HashSet<string> _propNames;
            private readonly Dictionary<string, TraitMethod> _methodLookup;
            private readonly string _implTypeName;

            public BodySyntaxRewriter(
                HashSet<string> propNames,
                Dictionary<string, TraitMethod> methodLookup,
                string implTypeName)
            {
                _propNames = propNames;
                _methodLookup = methodLookup;
                _implTypeName = implTypeName;
            }

            public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
            {
                var name = node.Identifier.Text;

                // Rewrite 'this' → 'self'
                if (name == "this")
                {
                    return SyntaxFactory.IdentifierName("self")
                        .WithTriviaFrom(node);
                }

                // Rewrite trait property accesses: X → {ImplType}.GetX_Impl(in self)
                // Only rewrite if it's a simple identifier (not member access like obj.X)
                if (_propNames.Contains(name) && !IsMemberAccessTarget(node))
                {
                    // Create: {ImplType}.Get{Name}_Impl(in self)
                    var invocation = SyntaxFactory.InvocationExpression(
                        SyntaxFactory.MemberAccessExpression(
                            SyntaxKind.SimpleMemberAccessExpression,
                            SyntaxFactory.IdentifierName(_implTypeName),
                            SyntaxFactory.IdentifierName($"Get{name}_Impl")),
                        SyntaxFactory.ArgumentList(
                            SyntaxFactory.SingletonSeparatedList(
                                SyntaxFactory.Argument(null,
                                    SyntaxFactory.Token(SyntaxKind.InKeyword),
                                    SyntaxFactory.IdentifierName("self")))));
                    return invocation.WithTriviaFrom(node);
                }

                return base.VisitIdentifierName(node);
            }

            public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
            {
                // Check for simple method call: Describe(args...)
                if (node.Expression is IdentifierNameSyntax identName)
                {
                    var methodName = identName.Identifier.Text;
                    if (_methodLookup.TryGetValue(methodName, out var traitMethod))
                    {
                        // Rewrite: MethodName(args) → {ImplType}.MethodName_Impl(in self, args...)
                        var args = new List<ArgumentSyntax>
                        {
                            SyntaxFactory.Argument(null,
                                SyntaxFactory.Token(SyntaxKind.InKeyword),
                                SyntaxFactory.IdentifierName("self"))
                        };

                        // Visit and add the original arguments
                        foreach (var arg in node.ArgumentList.Arguments)
                        {
                            var visited = (ArgumentSyntax)Visit(arg)!;
                            args.Add(visited);
                        }

                        var rewritten = SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName(_implTypeName),
                                SyntaxFactory.IdentifierName(traitMethod.ImplMethodName)),
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList(args)));

                        return rewritten.WithTriviaFrom(node);
                    }
                }

                // Check for this.Method(args...) pattern
                if (node.Expression is MemberAccessExpressionSyntax memberAccess &&
                    memberAccess.Expression is ThisExpressionSyntax)
                {
                    var methodName = memberAccess.Name.Identifier.Text;
                    if (_methodLookup.TryGetValue(methodName, out var traitMethod))
                    {
                        var args = new List<ArgumentSyntax>
                        {
                            SyntaxFactory.Argument(null,
                                SyntaxFactory.Token(SyntaxKind.InKeyword),
                                SyntaxFactory.IdentifierName("self"))
                        };

                        foreach (var arg in node.ArgumentList.Arguments)
                        {
                            var visited = (ArgumentSyntax)Visit(arg)!;
                            args.Add(visited);
                        }

                        var rewritten = SyntaxFactory.InvocationExpression(
                            SyntaxFactory.MemberAccessExpression(
                                SyntaxKind.SimpleMemberAccessExpression,
                                SyntaxFactory.IdentifierName(_implTypeName),
                                SyntaxFactory.IdentifierName(traitMethod.ImplMethodName)),
                            SyntaxFactory.ArgumentList(
                                SyntaxFactory.SeparatedList(args)));

                        return rewritten.WithTriviaFrom(node);
                    }
                }

                return base.VisitInvocationExpression(node);
            }

            public override SyntaxNode? VisitThisExpression(ThisExpressionSyntax node)
            {
                // Rewrite 'this' → 'self'
                return SyntaxFactory.IdentifierName("self")
                    .WithTriviaFrom(node);
            }

            /// <summary>
            /// Checks if the identifier is the right-hand side of a member access (e.g., obj.X),
            /// in which case we should NOT rewrite it as a trait property.
            /// </summary>
            private static bool IsMemberAccessTarget(IdentifierNameSyntax node)
            {
                if (node.Parent is MemberAccessExpressionSyntax memberAccess)
                {
                    // If this node is the .Name part of "expr.Name", it's a member access target
                    // We should only rewrite if it's the standalone identifier, not a member access
                    if (memberAccess.Name == node)
                        return true;
                }
                return false;
            }
        }
    }
}
