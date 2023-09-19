// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System.Reflection;
using System.Text;
using ConfigurationProcessor.SourceGeneration;
using ConfigurationProcessor.SourceGeneration.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ConfigurationProcessor;

/// <summary>
/// Generates method for registration based on an appsetting.json file.
/// </summary>
[Generator]
public class Generator : ISourceGenerator
{
    /// <inheritdoc/>
    public void Initialize(GeneratorInitializationContext context)
    {
        context.RegisterForSyntaxNotifications(SyntaxContextReceiver.Create);
    }

    /// <inheritdoc/>
    public void Execute(GeneratorExecutionContext context)
    {
        if (context.SyntaxContextReceiver is not SyntaxContextReceiver receiver || receiver.ClassDeclarations.Count == 0)
        {
            // nothing to do yet
            return;
        }

#if DEBUG
        System.Diagnostics.Debugger.Launch();
#endif

        var p = new Parser(context, context.ReportDiagnostic, context.CancellationToken);
        IReadOnlyList<ServiceRegistrationClass> registrationClasses = p.GetServiceRegistrationClasses(receiver.ClassDeclarations);
        if (registrationClasses.Count > 0)
        {
            var paths = context.Compilation.ExternalReferences.Select(x => x.Display!).ToList();
            var resolver = new PathAssemblyResolver(paths);
            var mlc = new MetadataLoadContext(resolver);
            var references = context.Compilation.ExternalReferences.Select(x => mlc.LoadFromAssemblyPath(x.Display!)).ToList();

            string result = Emitter.Emit(registrationClasses, references, context.CancellationToken);

            context.AddSource($"{registrationClasses.First().Name}.g.cs", SourceText.From(result, Encoding.UTF8));
        }
    }

    private sealed class SyntaxContextReceiver : ISyntaxContextReceiver
    {
        public HashSet<ClassDeclarationSyntax> ClassDeclarations { get; } = new();

        internal static SyntaxContextReceiver Create()
        {
            return new SyntaxContextReceiver();
        }

        public void OnVisitSyntaxNode(GeneratorSyntaxContext context)
        {
            if (IsSyntaxTargetForGeneration(context.Node))
            {
                var classSyntax = GetSemanticTargetForGeneration(context);
                if (classSyntax != null)
                {
                    ClassDeclarations.Add(classSyntax);
                }
            }
        }

        private static bool IsSyntaxTargetForGeneration(SyntaxNode node) =>
            node is MethodDeclarationSyntax m && m.AttributeLists.Count > 0;

        private static ClassDeclarationSyntax? GetSemanticTargetForGeneration(GeneratorSyntaxContext context)
        {
            var methodDeclarationSyntax = (MethodDeclarationSyntax)context.Node;

            foreach (AttributeListSyntax attributeListSyntax in methodDeclarationSyntax.AttributeLists)
            {
                foreach (AttributeSyntax attributeSyntax in attributeListSyntax.Attributes)
                {
                    var attributeSymbol = context.SemanticModel.GetSymbolInfo(attributeSyntax).Symbol as IMethodSymbol;
                    if (attributeSymbol == null)
                    {
                        continue;
                    }

                    INamedTypeSymbol attributeContainingTypeSymbol = attributeSymbol.ContainingType;
                    string fullName = attributeContainingTypeSymbol.ToDisplayString();

                    if (fullName == Parser.GenerateServiceRegistrationAttribute)
                    {
                        return methodDeclarationSyntax.Parent as ClassDeclarationSyntax;
                    }
                }
            }

            return null;
        }
    }
}