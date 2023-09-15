using System.Collections.Immutable;
using System.Data;
using System.Diagnostics;
using ConfigurationProcessor.DependencyInjection.SourceGeneration.Parsing;
using ConfigurationProcessor.DependencyInjection.SourceGeneration.Utility;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace ConfigurationProcessor.DependencyInjection.SourceGeneration;

internal class Parser
{
    internal const string DefaultConfigurationFile = "appsettings.json";
    internal const string GenerateServiceRegistrationAttribute = "ConfigurationProcessor.DependencyInjection.GenerateServiceRegistrationAttribute";
    internal const string ServiceCollectionTypeName = "Microsoft.Extensions.DependencyInjection.IServiceCollection";
    private readonly GeneratorExecutionContext context;
    private readonly Action<Diagnostic> reportDiagnostic;
    private readonly CancellationToken cancellationToken;

    public Parser(GeneratorExecutionContext context, Action<Diagnostic> reportDiagnostic, CancellationToken cancellationToken)
    {
        this.context = context;
        this.reportDiagnostic = reportDiagnostic;
        this.cancellationToken = cancellationToken;
    }

    internal IReadOnlyList<ServiceRegistrationClass> GetServiceRegistrationClasses(IEnumerable<ClassDeclarationSyntax> classes)
    {
        INamedTypeSymbol? generateServiceRegistrationAttribute = context.Compilation.GetBestTypeByMetadataName(GenerateServiceRegistrationAttribute);
        if (generateServiceRegistrationAttribute == null)
        {
            // nothing to do if this type isn't available
            return Array.Empty<ServiceRegistrationClass>();
        }

        INamedTypeSymbol? serviceCollectionSymbol = context.Compilation.GetBestTypeByMetadataName(ServiceCollectionTypeName);
        if (serviceCollectionSymbol == null)
        {
            // nothing to do if this type isn't available
            return Array.Empty<ServiceRegistrationClass>();
        }

        INamedTypeSymbol? configurationSymbol = context.Compilation.GetBestTypeByMetadataName("Microsoft.Extensions.Configuration.IConfiguration");
        if (configurationSymbol == null)
        {
            // nothing to do if this type isn't available
            return Array.Empty<ServiceRegistrationClass>();
        }

        var results = new List<ServiceRegistrationClass>();
        var eventIds = new HashSet<int>();
        var eventNames = new HashSet<string>();

        // we enumerate by syntax tree, to minimize the need to instantiate semantic models (since they're expensive)
        foreach (IGrouping<SyntaxTree, ClassDeclarationSyntax> group in classes.GroupBy(x => x.SyntaxTree))
        {
            SyntaxTree syntaxTree = group.Key;
            SemanticModel sm = context.Compilation.GetSemanticModel(syntaxTree);

            foreach (ClassDeclarationSyntax classDec in group)
            {
                // stop if we're asked to
                cancellationToken.ThrowIfCancellationRequested();

                ServiceRegistrationClass? lc = null;
                string nspace = string.Empty;
                string? serviceCollectionField = null;
                bool multipleServiceCollectionFields = false;

                // events ids and names should be unique in a class
                eventIds.Clear();
                eventNames.Clear();

                foreach (MemberDeclarationSyntax member in classDec.Members)
                {
                    var method = member as MethodDeclarationSyntax;
                    if (method == null)
                    {
                        // we only care about methods
                        continue;
                    }

                    IMethodSymbol? configurationMethodSymbol = sm.GetDeclaredSymbol(method, cancellationToken)!;
                    Debug.Assert(configurationMethodSymbol != null, "configuration method is present.");
                    (string configurationSection, string? configurationFile) = (string.Empty, null);
                    string[] excluded = Array.Empty<string>();
                    foreach (AttributeListSyntax mal in method.AttributeLists)
                    {
                        foreach (AttributeSyntax ma in mal.Attributes)
                        {
                            IMethodSymbol? attrCtorSymbol = sm.GetSymbolInfo(ma, cancellationToken).Symbol as IMethodSymbol;
                            if (attrCtorSymbol == null || !generateServiceRegistrationAttribute.Equals(attrCtorSymbol.ContainingType, SymbolEqualityComparer.Default))
                            {
                                // badly formed attribute definition, or not the right attribute
                                continue;
                            }

                            bool hasMisconfiguredInput = false;
                            ImmutableArray<AttributeData> boundAttributes = configurationMethodSymbol!.GetAttributes();

                            if (boundAttributes.Length == 0)
                            {
                                continue;
                            }

                            foreach (AttributeData attributeData in boundAttributes)
                            {
                                if (!SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, generateServiceRegistrationAttribute))
                                {
                                    continue;
                                }

                                // supports: [GenerateServiceRegistration("Services")]
                                // supports: [GenerateServiceRegistration(configurationSection: "Services")]
                                if (attributeData.ConstructorArguments.Any())
                                {
                                    foreach (TypedConstant typedConstant in attributeData.ConstructorArguments)
                                    {
                                        if (typedConstant.Kind == TypedConstantKind.Error)
                                        {
                                            hasMisconfiguredInput = true;
                                            break; // if a compilation error was found, no need to keep evaluating other args
                                        }
                                    }

                                    ImmutableArray<TypedConstant> items = attributeData.ConstructorArguments;

                                    switch (items.Length)
                                    {
                                        case 1:
                                            // GenerateServiceRegistration(string configurationSection)
                                            configurationSection = (string)GetItem(items[0])!;
                                            break;

                                        default:
                                            Debug.Assert(false, "Unexpected number of arguments in attribute constructor.");
                                            break;
                                    }
                                }

                                // argument syntax takes parameters. e.g. EventId = 0
                                // supports: e.g. [GenerateServiceRegistration("Services", ConfigurationFile = "appsettings.dev.json")]
                                if (attributeData.NamedArguments.Any())
                                {
                                    foreach (KeyValuePair<string, TypedConstant> namedArgument in attributeData.NamedArguments)
                                    {
                                        TypedConstant typedConstant = namedArgument.Value;
                                        if (typedConstant.Kind == TypedConstantKind.Error)
                                        {
                                            hasMisconfiguredInput = true;
                                            break; // if a compilation error was found, no need to keep evaluating other args
                                        }
                                        else
                                        {
                                            TypedConstant value = namedArgument.Value;
                                            switch (namedArgument.Key)
                                            {
                                                case "ConfigurationFile":
                                                    configurationFile = (string?)GetItem(value);
                                                    break;
                                                case "ExcludedSections":
                                                    var values = (ImmutableArray<TypedConstant>)GetItem(value)!;
                                                    excluded = values.Select(x => $"{configurationSection}:{x.Value}").ToArray();
                                                    break;
                                            }
                                        }
                                    }
                                }
                            }

                            if (hasMisconfiguredInput)
                            {
                                // skip further generator execution and let compiler generate the errors
                                break;
                            }

                            var configFile = configurationFile ?? DefaultConfigurationFile;
                            IDictionary<string, string?> configurationValues;
                            var jsonFilePath = context.AdditionalFiles.FirstOrDefault(x => Path.GetFileName(x.Path) == configFile)?.Path;

                            if (jsonFilePath == null &&
                                context.AnalyzerConfigOptions.GlobalOptions.TryGetValue("build_property.projectdir", out var projectDir) &&
                                File.Exists(Path.Combine(projectDir, configFile)))
                            {
                                jsonFilePath = Path.Combine(projectDir, configFile);
                            }

                            if (jsonFilePath == null)
                            {
                                Diag(DiagnosticDescriptors.ConfigurationFileNotFound, method.GetLocation(), configFile);
                                continue;
                            }
                            else
                            {
                                configurationValues = JsonConfigurationFileParser.Parse(File.OpenRead(jsonFilePath));

                                if (excluded.Length > 0)
                                {
                                    configurationValues = configurationValues
                                        .Where(x => !excluded.Any(z => x.Key.StartsWith(z)))
                                        .ToDictionary(x => x.Key, x => x.Value);
                                }
                            }

                            var methodSignature = string.Join(", ", configurationMethodSymbol.Parameters.Select(ToDisplay));
                            if (configurationMethodSymbol.IsExtensionMethod)
                            {
                                methodSignature = "this " + methodSignature;
                            }

                            var lm = new ServiceRegistrationMethod(configurationMethodSymbol.Name, methodSignature, method.Modifiers.ToString(), configurationValues, configurationSection);

                            static string ToDisplay(IParameterSymbol parameter)
                            {
                                return $"global::{parameter.Type} {parameter.Name}";
                            }

                            bool keepMethod = true;   // whether or not we want to keep the method definition or if it's got errors making it so we should discard it instead

                            if (lm.Name[0] == '_')
                            {
                                // can't have generate configuration method names that start with _ since that can lead to conflicting symbol names
                                // because the generated symbols start with _
                                Diag(DiagnosticDescriptors.InvalidGenerateConfigurationMethodName, method.Identifier.GetLocation());
                                keepMethod = false;
                            }

                            if (!configurationMethodSymbol.ReturnsVoid)
                            {
                                // generate configuration methods must return void
                                Diag(DiagnosticDescriptors.GenerateConfigurationMethodMustReturnVoid, method.ReturnType.GetLocation());
                                keepMethod = false;
                            }

                            if (method.Arity > 0)
                            {
                                // we don't support generic methods
                                Diag(DiagnosticDescriptors.GenerateConfigurationMethodIsGeneric, method.Identifier.GetLocation());
                                keepMethod = false;
                            }

                            bool isStatic = false;
                            bool isPartial = false;
                            foreach (SyntaxToken mod in method.Modifiers)
                            {
                                if (mod.IsKind(SyntaxKind.PartialKeyword))
                                {
                                    isPartial = true;
                                }
                                else if (mod.IsKind(SyntaxKind.StaticKeyword))
                                {
                                    isStatic = true;
                                }
                            }

                            if (!isPartial)
                            {
                                Diag(DiagnosticDescriptors.GenerateConfigurationMethodMustBePartial, method.GetLocation());
                                keepMethod = false;
                            }

                            CSharpSyntaxNode? methodBody = method.Body as CSharpSyntaxNode ?? method.ExpressionBody;
                            if (methodBody != null)
                            {
                                Diag(DiagnosticDescriptors.GenerateConfigurationMethodHasBody, methodBody.GetLocation());
                                keepMethod = false;
                            }

                            bool foundServiceCollection = false;
                            bool foundConfiguration = false;
                            foreach (IParameterSymbol paramSymbol in configurationMethodSymbol.Parameters)
                            {
                                string paramName = paramSymbol.Name;

                                if (string.IsNullOrWhiteSpace(paramName))
                                {
                                    // semantic problem, just bail quietly
                                    keepMethod = false;
                                    break;
                                }

                                ITypeSymbol paramTypeSymbol = paramSymbol.Type;
                                if (paramTypeSymbol is IErrorTypeSymbol)
                                {
                                    // semantic problem, just bail quietly
                                    keepMethod = false;
                                    break;
                                }

                                if (paramName[0] == '_')
                                {
                                    // can't have generate configuration method parameter names that start with _ since that can lead to conflicting symbol names
                                    // because all generated symbols start with _
                                    Diag(DiagnosticDescriptors.InvalidGenerateConfigurationMethodParameterName, paramSymbol.Locations[0]);
                                }

                                var matchesServiceCollection = IsBaseOrIdentity(paramTypeSymbol, serviceCollectionSymbol);
                                var matchesConfiguration = IsBaseOrIdentity(paramTypeSymbol, configurationSymbol);
                                if (foundServiceCollection && matchesServiceCollection)
                                {
                                    keepMethod = false;
                                    Diag(DiagnosticDescriptors.MultipleServiceCollectionParameter, paramSymbol.Locations[0]);
                                    break;
                                }
                                else if (matchesServiceCollection)
                                {
                                    foundServiceCollection = matchesServiceCollection;
                                    lm.ServiceCollectionField = paramName;
                                }

                                if (foundConfiguration && matchesConfiguration)
                                {
                                    keepMethod = false;
                                    Diag(DiagnosticDescriptors.MultipleConfigurationParameter, paramSymbol.Locations[0]);
                                    break;
                                }
                                else if (matchesConfiguration)
                                {
                                    foundConfiguration = matchesConfiguration;
                                    lm.ConfigurationField = paramName;
                                }
                            }

                            if (keepMethod && !foundServiceCollection && !foundConfiguration && configurationMethodSymbol.Parameters.Length == 1)
                            {
                                // we check if the single parameter has public properties that are assignable to serviceCollection and configuration
                                var paramSymbol = configurationMethodSymbol.Parameters[0];
                                string paramName = paramSymbol.Name;

                                if (string.IsNullOrWhiteSpace(paramName))
                                {
                                    // semantic problem, just bail quietly
                                    keepMethod = false;
                                }
                                else
                                {
                                    ITypeSymbol paramTypeSymbol = paramSymbol.Type;
                                    if (paramTypeSymbol is IErrorTypeSymbol)
                                    {
                                        // semantic problem, just bail quietly
                                        keepMethod = false;
                                    }
                                    else if (paramName[0] == '_')
                                    {
                                        // can't have generate configuration method parameter names that start with _ since that can lead to conflicting symbol names
                                        // because all generated symbols start with _
                                        Diag(DiagnosticDescriptors.InvalidGenerateConfigurationMethodParameterName, paramSymbol.Locations[0]);
                                    }
                                    else
                                    {
                                        var properties = paramTypeSymbol.GetMembers().OfType<IPropertySymbol>().ToArray();
                                        foreach (var property in properties)
                                        {
                                            var matchesServiceCollection = IsBaseOrIdentity(property.Type, serviceCollectionSymbol);
                                            var matchesConfiguration = IsBaseOrIdentity(property.Type, configurationSymbol);
                                            if (foundServiceCollection && matchesServiceCollection)
                                            {
                                                keepMethod = false;
                                                Diag(DiagnosticDescriptors.MultipleServiceCollectionParameter, paramSymbol.Locations[0]);
                                                break;
                                            }
                                            else if (matchesServiceCollection)
                                            {
                                                foundServiceCollection = matchesServiceCollection;
                                                lm.ServiceCollectionField = $"{paramName}.{property.Name}";
                                            }

                                            if (foundConfiguration && matchesConfiguration)
                                            {
                                                keepMethod = false;
                                                Diag(DiagnosticDescriptors.MultipleConfigurationParameter, paramSymbol.Locations[0]);
                                                break;
                                            }
                                            else if (matchesConfiguration)
                                            {
                                                foundConfiguration = matchesConfiguration;
                                                lm.ConfigurationField = $"{paramName}.{property.Name}";
                                            }
                                        }
                                    }
                                }
                            }

                            if (keepMethod)
                            {
                                if (isStatic && !foundServiceCollection)
                                {
                                    Diag(DiagnosticDescriptors.MissingGenerateConfigurationArgument, method.GetLocation(), lm.Name);
                                    keepMethod = false;
                                }
                                else if (!isStatic && foundServiceCollection)
                                {
                                    Diag(DiagnosticDescriptors.GenerateConfigurationMethodShouldBeStatic, method.GetLocation());
                                }
                                else if (!isStatic && !foundServiceCollection)
                                {
                                    if (serviceCollectionField == null)
                                    {
                                        (serviceCollectionField, multipleServiceCollectionFields) = FindServiceCollectionField(sm, classDec, serviceCollectionSymbol);
                                    }

                                    if (multipleServiceCollectionFields)
                                    {
                                        Diag(DiagnosticDescriptors.MultipleServiceCollectionParameter, method.GetLocation(), classDec.Identifier.Text);
                                        keepMethod = false;
                                    }
                                    else if (serviceCollectionField == null)
                                    {
                                        Diag(DiagnosticDescriptors.MissingServiceCollectionParameter, method.GetLocation(), classDec.Identifier.Text);
                                        keepMethod = false;
                                    }
                                    else
                                    {
                                        lm.ServiceCollectionField = serviceCollectionField;
                                    }
                                }
                                else if (!foundConfiguration)
                                {
                                    Diag(DiagnosticDescriptors.MissingConfigurationParameter, method.GetLocation(), classDec.Identifier.Text);
                                    keepMethod = false;
                                }
                            }

                            if (lc == null)
                            {
                                // determine the namespace the class is declared in, if any
                                SyntaxNode? potentialNamespaceParent = classDec.Parent;
                                while (potentialNamespaceParent != null &&
                                       potentialNamespaceParent is not NamespaceDeclarationSyntax &&
                                       potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
                                {
                                    potentialNamespaceParent = potentialNamespaceParent.Parent;
                                }

                                if (potentialNamespaceParent is FileScopedNamespaceDeclarationSyntax fileScopedNamespace)
                                {
                                    nspace = fileScopedNamespace.Name.ToString();
                                }
                                else if (potentialNamespaceParent is NamespaceDeclarationSyntax namespaceParent)
                                {
                                    nspace = namespaceParent.Name.ToString();
                                    while (true)
                                    {
                                        if (namespaceParent.Parent is not NamespaceDeclarationSyntax namespaceDeclaration)
                                        {
                                            break;
                                        }
                                        else
                                        {
                                            namespaceParent = namespaceDeclaration;
                                        }

                                        nspace = $"{namespaceParent.Name}.{nspace}";
                                    }
                                }
                            }

                            if (string.IsNullOrEmpty(nspace))
                            {
                                keepMethod = false;
                                Diag(DiagnosticDescriptors.TopLevelClassNotSupported, method.GetLocation());
                            }

                            if (keepMethod)
                            {
                                lc ??= new ServiceRegistrationClass
                                {
                                    Keyword = classDec.Keyword.ValueText,
                                    Namespace = nspace,
                                    Name = classDec.Identifier.ToString() + classDec.TypeParameterList,
                                    ParentClass = null,
                                };

                                ServiceRegistrationClass currentServiceRegistrationClass = lc;
                                var parentServiceRegistrationClass = classDec.Parent as TypeDeclarationSyntax;

                                static bool IsAllowedKind(SyntaxKind kind) =>
                                    kind == SyntaxKind.ClassDeclaration ||
                                    kind == SyntaxKind.StructDeclaration ||
                                    kind == SyntaxKind.RecordDeclaration;

                                while (parentServiceRegistrationClass != null && IsAllowedKind(parentServiceRegistrationClass.Kind()))
                                {
                                    currentServiceRegistrationClass.ParentClass = new ServiceRegistrationClass
                                    {
                                        Keyword = parentServiceRegistrationClass.Keyword.ValueText,
                                        Namespace = nspace,
                                        Name = parentServiceRegistrationClass.Identifier.ToString() + parentServiceRegistrationClass.TypeParameterList,
                                        ParentClass = null,
                                    };

                                    currentServiceRegistrationClass = currentServiceRegistrationClass.ParentClass;
                                    parentServiceRegistrationClass = parentServiceRegistrationClass.Parent as TypeDeclarationSyntax;
                                }

                                lc.Methods.Add(lm);
                            }
                        }
                    }
                }

                if (lc != null)
                {
                    // once we've collected all methods for the given class, check for overloads
                    // and provide unique names for generate configuration methods
                    var methods = new Dictionary<string, int>(lc.Methods.Count);
                    foreach (var lm in lc.Methods)
                    {
                        if (methods.TryGetValue(lm.Name, out int currentCount))
                        {
                            lm.UniqueName = $"{lm.Name}{currentCount}";
                            methods[lm.Name] = currentCount + 1;
                        }
                        else
                        {
                            lm.UniqueName = lm.Name;
                            methods[lm.Name] = 1; // start from 1
                        }
                    }

                    results.Add(lc);
                }
            }

            static object? GetItem(TypedConstant arg) => arg.Kind == TypedConstantKind.Array ? arg.Values : arg.Value;
        }

        if (results.Count > 0 && context.Compilation is CSharpCompilation { LanguageVersion: LanguageVersion version and < LanguageVersion.CSharp8 })
        {
            // we only support C# 8.0 and above
            Diag(DiagnosticDescriptors.GenerateConfigurationUnsupportedLanguageVersion, null, version.ToDisplayString(), LanguageVersion.CSharp8.ToDisplayString());
            return Array.Empty<ServiceRegistrationClass>();
        }

        return results;
    }

    private (string? ServiceCollectionField, bool MultipleServiceCollectionFields) FindServiceCollectionField(SemanticModel sm, TypeDeclarationSyntax classDec, ITypeSymbol serviceCollectionSymbol)
    {
        string? serviceCollectionField = null;

        INamedTypeSymbol? classType = sm.GetDeclaredSymbol(classDec, cancellationToken);

        bool onMostDerivedType = true;

        while (classType is { SpecialType: not SpecialType.System_Object })
        {
            foreach (IFieldSymbol fs in classType.GetMembers().OfType<IFieldSymbol>())
            {
                if (!onMostDerivedType && fs.DeclaredAccessibility == Accessibility.Private)
                {
                    continue;
                }

                if (IsBaseOrIdentity(fs.Type, serviceCollectionSymbol))
                {
                    if (serviceCollectionField == null)
                    {
                        serviceCollectionField = fs.Name;
                    }
                    else
                    {
                        return (null, true);
                    }
                }
            }

            onMostDerivedType = false;
            classType = classType.BaseType;
        }

        return (serviceCollectionField, false);
    }

    private void Diag(DiagnosticDescriptor desc, Location? location, params object?[]? messageArgs)
    {
        reportDiagnostic(Diagnostic.Create(desc, location, messageArgs));
    }

    private bool IsBaseOrIdentity(ITypeSymbol source, ITypeSymbol dest)
    {
        Conversion conversion = context.Compilation.ClassifyConversion(source, dest);
        return conversion.IsIdentity || (conversion.IsReference && conversion.IsImplicit);
    }
}