// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System.Text;
using ConfigurationProcessor.Gen.DependencyInjection.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace ConfigurationProcessor.Gen.DependencyInjection;

/// <summary>
/// Generates method for registration based on an appsetting.json file.
/// </summary>
[Generator]
public class ConfigurationRegistrationGenerator : ISourceGenerator
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

        var p = new Parser(context.Compilation, context.ReportDiagnostic, context.CancellationToken);
        IReadOnlyList<ServiceRegistrationClass> registrationClasses = p.GetServiceRegistrationClasses(receiver.ClassDeclarations);
        if (registrationClasses.Count > 0)
        {
            var e = new Emitter(context, context.ReportDiagnostic);
            string result = e.Emit(registrationClasses, context.CancellationToken);

            context.AddSource("RegisterServices.g.cs", SourceText.From(result, Encoding.UTF8));
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