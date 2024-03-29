﻿using System.Diagnostics;
using System.Reflection;
using ConfigurationProcessor.Core.Implementation;
using ConfigurationProcessor.SourceGeneration.Parsing;
using Microsoft.CodeAnalysis;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.SourceGeneration.Utility;

internal static class Helpers
{
    public static INamedTypeSymbol? GetBestTypeByMetadataName(this Compilation compilation, string fullyQualifiedMetadataName)
    {
        // Try to get the unique type with this name, ignoring accessibility
        var type = compilation.GetTypeByMetadataName(fullyQualifiedMetadataName);

        // Otherwise, try to get the unique type with this name originally defined in 'compilation'
        type ??= compilation.Assembly.GetTypeByMetadataName(fullyQualifiedMetadataName);

        // Otherwise, try to get the unique accessible type with this name from a reference
        if (type is null)
        {
            foreach (var module in compilation.Assembly.Modules)
            {
                foreach (var referencedAssembly in module.ReferencedAssemblySymbols)
                {
                    var currentType = referencedAssembly.GetTypeByMetadataName(fullyQualifiedMetadataName);
                    if (currentType is null)
                    {
                        continue;
                    }

                    switch (currentType.GetResultantVisibility())
                    {
                        case SymbolVisibility.Public:
                        case SymbolVisibility.Internal when referencedAssembly.GivesAccessTo(compilation.Assembly):
                            break;

                        default:
                            continue;
                    }

                    if (type is object)
                    {
                        // Multiple visible types with the same metadata name are present
                        return null;
                    }

                    type = currentType;
                }
            }
        }

        return type;
    }

    private static SymbolVisibility GetResultantVisibility(this ISymbol symbol)
    {
        // Start by assuming it's visible.
        SymbolVisibility visibility = SymbolVisibility.Public;

        switch (symbol.Kind)
        {
            case SymbolKind.Alias:
                // Aliases are uber private.  They're only visible in the same file that they
                // were declared in.
                return SymbolVisibility.Private;

            case SymbolKind.Parameter:
                // Parameters are only as visible as their containing symbol
                return symbol.ContainingSymbol.GetResultantVisibility();

            case SymbolKind.TypeParameter:
                // Type Parameters are private.
                return SymbolVisibility.Private;
        }

        while (symbol != null && symbol.Kind != SymbolKind.Namespace)
        {
            switch (symbol.DeclaredAccessibility)
            {
                // If we see anything private, then the symbol is private.
                case Accessibility.NotApplicable:
                case Accessibility.Private:
                    return SymbolVisibility.Private;

                // If we see anything internal, then knock it down from public to
                // internal.
                case Accessibility.Internal:
                case Accessibility.ProtectedAndInternal:
                    visibility = SymbolVisibility.Internal;
                    break;

                    // For anything else (Public, Protected, ProtectedOrInternal), the
                    // symbol stays at the level we've gotten so far.
            }

            symbol = symbol.ContainingSymbol;
        }

        return visibility;
    }

    public static void Deconstruct<TKey, TValue>(this KeyValuePair<TKey, TValue> pair, out TKey key, out TValue value)
    {
        key = pair.Key;
        value = pair.Value;
    }

    public static IEnumerable<KeyValuePair<string, string?>> CleanUpKeys(this IConfigurationSection source, string parentSection)
    {
        return source.AsEnumerable().Skip(1).Select(x => new KeyValuePair<string, string?>(x.Key.Substring(parentSection.Length + 1), x.Value));
    }

    public static void EmitComplex(
        this EmitContext emitContext,
        MethodInfo chosenMethod,
        string configSectionVariableName,
        string configKey,
        string targetVariableName,
        IConfigurationSection configSection,
        string newSectionName,
        IConfiguration rootConfiguration,
        Dictionary<string, IConfigurationSection> paramArgs)
    {
        var methodName = chosenMethod.GetNameWithGenericArguments() ?? configKey;
        emitContext.AddNamespace(chosenMethod.DeclaringType.Namespace);

        // we check if this is an extension method and get the non extension parameter type
        var paramType = chosenMethod.IsStatic ? chosenMethod.GetParameters()[1].ParameterType : chosenMethod.GetParameters()[0].ParameterType;

        if (string.IsNullOrEmpty(configSection.Value))
        {
            emitContext.Write($$"""

            var {{newSectionName}} = {{configSectionVariableName}}.GetSection("{{configKey}}");
            if ({{newSectionName}}.Exists())
            {
            """);
            emitContext.IncreaseIndent();

            // we check if this is an extension method with the Action<T> parameter
            if (paramType!.IsGenericType && paramType.BaseType?.FullName == typeof(MulticastDelegate).FullName)
            {
                emitContext.Write($$"""
                    {{targetVariableName}}.{{methodName}}(options =>
                    {
                    """);
                emitContext.IncreaseIndent();
                emitContext.EmitValues(rootConfiguration, configSection, configKey, newSectionName, paramType.GetGenericArguments()[0].FullName, "options");
                emitContext.DecreaseIndent();
                emitContext.Write("});");
            }
            else
            {
                if ((paramType == typeof(string) || paramType.IsValueType) && paramArgs.Count == 1)
                {
                    emitContext.Write($"{targetVariableName}.{methodName}({newSectionName}.GetValue<{paramType.GetCSharpFullName()}>(\"{paramArgs.Keys.Single()}\"));");
                }
                else
                {
                    emitContext.Write($"{targetVariableName}.{methodName}({newSectionName}.Get<{paramType.GetCSharpFullName()}>());");
                }
            }

            emitContext.DecreaseIndent();
            emitContext.Write("}");
        }
        else if (StringArgumentValue.TryParseStaticMemberAccessor(configSection.Value!, out var accessorTypeName, out var memberName))
        {
            emitContext.Write($$"""

            if ({{configSectionVariableName}}.GetValue<string>("{{configKey}}") == "{{configSection.Value}}")
            {
                {{targetVariableName}}.{{methodName}}({{accessorTypeName}}.{{memberName}});
            }
            """);
        }
        else
        {
            emitContext.Write(string.Empty);
            emitContext.Write($"{targetVariableName}.{methodName}({configSectionVariableName}.GetValue<{paramType.GetCSharpFullName()}>(\"{configKey}\"));");
        }
    }

    public static void EmitSimpleBoolean(
        this EmitContext emitContext,
        List<MethodInfo> configurationMethods,
        string configSectionVariableName,
        string key,
        string targetVariableName)
    {
        // simple configuration
        var foundMethod = configurationMethods.FirstOrDefault();
        if (foundMethod != null)
        {
            emitContext.AddNamespace(foundMethod.DeclaringType.Namespace);
        }

        emitContext.Write(
            $@"
if ({configSectionVariableName}.GetValue<bool>(""{key}""))
{{
   {targetVariableName}.{foundMethod?.GetNameWithGenericArguments() ?? key}();
}}");
    }

    public static void EmitValues(
        this EmitContext emitContext,
        IConfiguration rootConfiguration,
        IConfigurationSection directive,
        string sectionName,
        string configSectionVariableName,
        string targetTypeName,
        string targetVariableName)
    {
        var resCtx = new ResolutionContext(emitContext, rootConfiguration);
        var methods = resCtx.GetMethodCalls(directive, true);

        foreach (var (methodName, (typeArgs, configSection, configArgs)) in methods.SelectMany(g => g.Select(x => (MethodName: g.Key, Config: x))))
        {
            var paramArgs = configArgs;
            emitContext.EmitValues(
               methodName,
               rootConfiguration,
               configSection,
               typeArgs,
               paramArgs,
               configSectionVariableName,
               targetTypeName,
               targetVariableName);
        }
    }

    private static void EmitValues(
        this EmitContext emitContext,
        string methodName,
        IConfiguration rootConfiguration,
        IConfigurationSection configSection,
        TypeResolver[] typeArgs,
        Dictionary<string, (IConfigurationArgumentValue ArgName, IConfigurationSection ConfigSection)> paramArgs,
        string configSectionVariableName,
        string targetTypeName,
        string targetVariableName)
    {
        Type targetType = emitContext.TypeMap[targetTypeName].Single();
        var resolutionContext = new ResolutionContext(emitContext, rootConfiguration);

        var origCandidateNames = new[] { methodName, $"Add{methodName}", $"set_{methodName}" };
        var candidateNames = new List<string>(origCandidateNames);
        if (emitContext.ImplicitSuffixes != null && emitContext.ImplicitSuffixes.Length > 0)
        {
            foreach (var suffix in emitContext.ImplicitSuffixes)
            {
                candidateNames.AddRange(origCandidateNames.Select(x => $"{x}{suffix}"));
            }
        }

        IEnumerable<MethodInfo> configurationMethods = resolutionContext
            .FindConfigurationExtensionMethods(methodName, targetType, typeArgs, candidateNames.ToArray(), (m, n) => candidateNames.Contains(n));

        var suppliedArgumentNames = paramArgs?.Keys.ToArray() ?? Array.Empty<string>();

        var isCollection = suppliedArgumentNames.IsArray();
        MethodInfo? configurationMethod;
        int? parameterSkip = null;
        if (targetType.FullName.StartsWith("System.Action`", StringComparison.Ordinal))
        {
            parameterSkip = targetType.GetGenericArguments().Length;
        }

        if (isCollection)
        {
            configurationMethod = configurationMethods
                .Where(m =>
                {
                    var parameters = m.GetParameters().Skip(parameterSkip ?? 0).ToArray();
                    if (parameters.Length != (m.IsStatic && parameterSkip == null ? 2 : 1))
                    {
                        return false;
                    }

                    var paramType = parameters[m.IsStatic && parameterSkip == null ? 1 : 0].ParameterType;
                    var isCollection = paramType.IsArray || (paramType.IsGenericType && typeof(List<>) == paramType.GetGenericTypeDefinition());

                    if (isCollection)
                    {
#pragma warning disable CA1031 // Do not catch general exception types
                        try
                        {
#pragma warning disable S1481 // Unused local variables should be removed
                            var collection = resolutionContext.GetCollection(configSection!, m, parameterSkip);
#pragma warning restore S1481 // Unused local variables should be removed
                        }
                        catch
                        {
                            return false;
                        }
#pragma warning restore CA1031 // Do not catch general exception types
                    }

                    return isCollection;
                })
                .SingleOrDefault($"Ambiguous match while searching for a method that accepts a list or array.");
        }
        else
        {
            // for single property, choose the best configuration method by attempting to convert the parameter value
            if (suppliedArgumentNames.Length == 1 && string.IsNullOrEmpty(suppliedArgumentNames.Single()) && configSection.Value != null)
            {
                var argvalue = configSection.Value;

                configurationMethod = configurationMethods
                   .Where(m =>
                   {
                       var parameters = m.GetParameters().Skip(parameterSkip ?? 0);
                       var hasExtensionParam = m.IsStatic && !parameterSkip.HasValue;
                       System.Diagnostics.Debug.WriteLine(parameters.Count(p => !p.HasDefaultValue));
                       if (parameters.Count(p => !p.HasDefaultValue && !p.HasImplicitValueWhenNotSpecified()) != (hasExtensionParam ? 2 : 1))
                       {
                           return false;
                       }

                       var parameter = m.GetParameters().Skip(parameterSkip ?? 0).Where(p => !p.HasImplicitValueWhenNotSpecified()).ElementAt(hasExtensionParam ? 1 : 0);
                       var paramType = parameter.ParameterType;
                       var isCollection = paramType.IsArray || (paramType.IsGenericType && typeof(List<>) == paramType.GetGenericTypeDefinition());
                       if (isCollection)
                       {
                           return false;
                       }

#pragma warning disable CA1031 // Do not catch general exception types
                       try
                       {
                           var argValue = new StringArgumentValue(configSection, argvalue, parameter.Name);
                           argValue.ConvertTo(m, paramType, resolutionContext);

                           return true;
                       }
                       catch
                       {
                           return false;
                       }
#pragma warning restore CA1031 // Do not catch general exception types
                   })
                   .SingleOrDefault($"Ambiguous match while searching for a method that accepts a single value.") ??

                   // if no match found, choose the parameterless overload
                   configurationMethods.SingleOrDefault(m => m.GetParameters().Skip(parameterSkip ?? 0).Count(p => !p.HasDefaultValue) == (m.IsStatic && parameterSkip == 0 ? 1 : 0));
            }
            else
            {
                configurationMethod = configurationMethods.SelectConfigurationMethod(suppliedArgumentNames, parameterSkip);
            }

            if (configurationMethod == null)
            {
                // if the method could still not be found, look method that accepts a single dictionary
                configurationMethod = configurationMethods
                    .Where(m =>
                    {
                        var parameters = m.GetParameters().Skip(parameterSkip ?? 0).ToArray();
                        if (parameters.Length != (m.IsStatic && parameterSkip == null ? 2 : 1))
                        {
                            return false;
                        }

                        var paramType = parameters[m.IsStatic && parameterSkip == null ? 1 : 0].ParameterType;
                        return paramType.IsGenericType && typeof(Dictionary<,>) == paramType.GetGenericTypeDefinition();
                    })
                    .SingleOrDefault($"Ambigous match while searching for a method that accepts Dictionary<,>.");

                if (configurationMethod != null)
                {
                    paramArgs = new Dictionary<string, (IConfigurationArgumentValue ArgName, IConfigurationSection ConfigSection)>
                    {
                        { string.Empty, (new ObjectArgumentValue(configSection), configSection) },
                    };
                }
            }
        }

        var propertyInfo = targetType.GetProperty(methodName);

        Debug.Assert(propertyInfo != null || configurationMethod != null, "Configuration method not found and not a property");

        if (propertyInfo != null)
        {
            if (paramArgs?.Count(x => !string.IsNullOrEmpty(x.Key)) > 0)
            {
                // check if the property has an accessible setter and parameterless constructor
                if (propertyInfo.CanWrite && propertyInfo.PropertyType.GetConstructor(Type.EmptyTypes) != null)
                {
                    emitContext.Write(
                        $@"{targetVariableName}.{propertyInfo.Name} = new {propertyInfo.PropertyType.GetCSharpFullName()}();");
                }

                foreach (var param in paramArgs)
                {
                    if (propertyInfo.PropertyType.GetProperty(param.Key) is PropertyInfo subProperty)
                    {
                        var value = param.Value.ConfigSection.GetSection(param.Key).Value;
                        if (subProperty.PropertyType == typeof(Type))
                        {
                            emitContext.Write($$"""
                                if ({{configSectionVariableName}}.GetValue<string>("{{param.Value.ConfigSection.Key}}:{{param.Key}}") == "{{value}}")
                                {
                                   {{targetVariableName}}.{{propertyInfo.Name}}.{{subProperty.Name}} = typeof({{value}});
                                }
                                else
                                {
                                   {{targetVariableName}}.{{propertyInfo.Name}}.{{subProperty.Name}} = global::System.Type.GetType({{configSectionVariableName}}.GetValue<string>("{{param.Value.ConfigSection.Key}}:{{param.Key}}"));
                                }

                                """);
                        }
                        else if (StringArgumentValue.TryParseStaticMemberAccessor(value!, out var accessorTypeName, out var memberName))
                        {
                            emitContext.Write($$"""
                                if ({{configSectionVariableName}}.GetValue<string>("{{param.Value.ConfigSection.Key}}:{{param.Key}}") == "{{value}}")
                                {
                                   {{targetVariableName}}.{{propertyInfo.Name}}.{{subProperty.Name}} = {{accessorTypeName}}.{{memberName}};
                                }

                                """);
                        }
                        else
                        {
                            emitContext.Write(
                                $@"{targetVariableName}.{propertyInfo.Name}.{subProperty.Name} = {configSectionVariableName}.GetValue<{subProperty.PropertyType.GetCSharpFullName()}>(""{param.Value.ConfigSection.Key}:{param.Key}"");");
                        }
                    }
                    else if (
                        (propertyInfo.PropertyType.GetMethods().SingleOrDefault(x => x.Name == param.Key) ??
                        propertyInfo.PropertyType.GetInterfaces().SelectMany(x => x.GetMethods()).FirstOrDefault(x => x.Name == param.Key)) != null)
                    {
                        emitContext.Write(
                            $@"if ({configSectionVariableName}.GetValue<bool>(""{param.Value.ConfigSection.Key}:{param.Key}""))
{{
   {targetVariableName}.{propertyInfo.Name}.{param.Key}();
}}");
                    }
                    else
                    {
                        emitContext.Write(
                            $@"// unsupported property/method {param.Key}");
                    }
                }
            }
            else
            {
                emitContext.Write(
                    $@"{targetVariableName}.{propertyInfo.Name} = {configSectionVariableName}.GetValue<{propertyInfo.PropertyType.GetCSharpFullName()}>(""{methodName}"");");
            }
        }
        else if (paramArgs?.Count > 0)
        {
            emitContext.EmitComplex(
                configurationMethod!,
                configSectionVariableName,
                configSection.Key,
                targetVariableName,
                configSection,
                $"section{methodName}",
                rootConfiguration,
                paramArgs.ToDictionary(x => x.Key, x => x.Value.ConfigSection));
        }
        else
        {
            emitContext.EmitSimpleBoolean(new List<MethodInfo> { configurationMethod! }, configSectionVariableName, methodName, targetVariableName);
        }
    }

    public static string GetCSharpFullName(this Type propertyType)
    {
        if (propertyType.IsGenericType && propertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            return $"{propertyType.GetGenericArguments()[0].FullName}?";
        }
        else
        {
            return propertyType.FullName.Replace('+', '.');
        }
    }
}
