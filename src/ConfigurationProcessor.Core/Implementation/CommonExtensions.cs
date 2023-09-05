using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using ConfigLookup = System.ValueTuple<ConfigurationProcessor.Core.Implementation.TypeResolver[], Microsoft.Extensions.Configuration.IConfigurationSection, System.Collections.Generic.Dictionary<string, (ConfigurationProcessor.Core.Implementation.IConfigurationArgumentValue ArgName, Microsoft.Extensions.Configuration.IConfigurationSection ConfigSection)>>;

namespace ConfigurationProcessor.Core.Implementation;

internal static class CommonExtensions
{
    private const string GenericTypePattern = "(?<typename>[a-zA-Z][a-zA-Z0-9\\.]+)<(?<genparam>.+)>";
    private static readonly Regex GenericTypeRegex = new Regex(GenericTypePattern, RegexOptions.Compiled);
    private const char GenericTypeMarker = '`';
    private const char GenericTypeParameterSeparator = '|';

    public static (string TypeName, TypeResolver[] Resolvers) ReadTypeName(
        this ResolutionContext resolutionContext,
        string name,
        IConfiguration ambientConfiguration)
    {
        var match = GenericTypeRegex.Match(name);
        string typeName;
        string typeArgs;

        if (match.Success)
        {
            typeName = match.Groups["typename"].Value;
            typeArgs = match.Groups["genparam"].Value;
        }
        else
        {
            var typeSplit = name.Split(GenericTypeMarker);
            typeName = typeSplit.First();
            typeArgs = typeSplit.ElementAtOrDefault(1)!;
        }

        if (typeArgs != null)
        {
            var args = typeArgs.Split(GenericTypeParameterSeparator);

            List<TypeResolver> targs = new List<TypeResolver>();

            foreach (var argument in args)
            {
                targs.Add(resolutionContext.ReadGenericType(argument, resolutionContext.RootConfiguration, ambientConfiguration));
            }

            return (typeName, targs.ToArray());
        }
        else
        {
            return (typeName, Array.Empty<TypeResolver>());
        }
    }

    private static TypeResolver ReadGenericType(this ResolutionContext resolutionContext, string typeName, IConfiguration rootConfiguration, IConfiguration ambientConfiguration)
    {
        var match = GenericTypeRegex.Match(typeName);
        if (match.Success)
        {
            typeName = match.Groups["typename"].Value;
            var typeArgs = match.Groups["genparam"].Value;

            var args = typeArgs.Split(GenericTypeParameterSeparator);

            var openGenType = resolutionContext.CreateTypeResolver($"{typeName}`{args.Length}", rootConfiguration, ambientConfiguration);
            return (m, i) => openGenType(m, i).MakeGenericType(args.Select((x, j) => resolutionContext.ReadGenericType(x, rootConfiguration, ambientConfiguration)(m, j)).ToArray());
        }
        else
        {
            return resolutionContext.CreateTypeResolver(typeName, rootConfiguration, ambientConfiguration);
        }
    }

    public static bool IsValueTupleCompatible(this Type type, Type other)
    {
        if (type.IsGenericType &&
           type.Name.StartsWith("ValueTuple`", StringComparison.Ordinal) &&
           other.IsGenericType &&
           other.Name.StartsWith("ValueTuple`", StringComparison.Ordinal) &&
           other != type &&
           type.GenericTypeArguments.Length == other.GenericTypeArguments.Length)
        {
            // compare each element if they're compatible
            return type.GenericTypeArguments
               .Zip(other.GenericTypeArguments, (t1, t2) => (t1, t2))
               .All(args => IsTypeCompatible(args.t1, args.t2));
        }

        return false;
    }

    public static bool IsTypeCompatible(this Type configType, Type? targetType)
    {
        if (targetType != null && IsValueTupleCompatible(configType, targetType))
        {
            return true;
        }

        return targetType != null && targetType.IsAssignableFrom(configType);
    }

    public static List<MethodInfo> FindConfigurationExtensionMethods(
        this ResolutionContext resolutionContext,
        string key,
        Type configType,
        TypeResolver[] typeArgs,
        IEnumerable<string>? candidateNames,
        MethodFilter? filter)
    {
        IReadOnlyCollection<Assembly> configurationAssemblies = resolutionContext.ConfigurationAssemblies;
        var interfaces = configType.GetInterfaces();
        var scannedTypes = configurationAssemblies
            .SelectMany(a => SafeGetExportedTypes(a)
                .Select(t => t.GetTypeInfo())
                .Where(t => t.IsSealed && t.IsAbstract && !t.IsNested));
        var candidateMethods = scannedTypes
            .Union(new[] { configType.GetTypeInfo() })
            .Concat(interfaces.Select(t => t.GetTypeInfo()))
            .SelectMany(t => candidateNames != null ? candidateNames.SelectMany(n => t.GetDeclaredMethods(n)) : t.DeclaredMethods)
            .Where(m => filter == null || filter(m, key))
#if Generator
            .Where(m => m.IsPublic && (m.IsStatic || IsTypeCompatible(configType, m.DeclaringType) || interfaces.Contains(m.DeclaringType)))
#else
            .Where(m => !m.IsDefined(typeof(CompilerGeneratedAttribute), false) && m.IsPublic && ((m.IsStatic && m.IsDefined(typeof(ExtensionAttribute), false)) || IsTypeCompatible(configType, m.DeclaringType) || interfaces.Contains(m.DeclaringType)))
#endif
            .Where(m => !m.IsStatic || configType.IsTypeCompatible(SafeGetParameters(m).ElementAtOrDefault(0)?.ParameterType)) // If static method, checks that the first parameter is same as the extension type
            .ToList();

        if (configType.FullName.StartsWith("System.Action`", StringComparison.Ordinal))
        {
            var genericArgs = configType.GetGenericArguments();
            candidateMethods.AddRange(scannedTypes
               .SelectMany(t => candidateNames != null ? candidateNames.SelectMany(n => t.GetDeclaredMethods(n)) : t.DeclaredMethods)
               .Where(m => m.GetParameters().Select(p => p.ParameterType).Take(genericArgs.Length).Zip(genericArgs, (a, b) => (a, b)).All(values => values.a.IsAssignableFrom(values.b))));
        }

        if (typeArgs == null || typeArgs.Length == 0)
        {
            return candidateMethods.Where(m => !m.IsGenericMethod).ToList();
        }
        else
        {
            return candidateMethods
                .Where(m => m.IsGenericMethod && CanMakeGeneric(m))
                .Select(m => m.MakeGenericMethod(typeArgs.Select((t, i) => t(m, i)).ToArray()))
                .ToList();
        }

        static IEnumerable<Type> SafeGetExportedTypes(Assembly assembly)
        {
            try
            {
                return assembly.ExportedTypes;
            }
            catch (FileNotFoundException)
            {
                return Array.Empty<Type>();
            }
            catch (TypeLoadException)
            {
                return Array.Empty<Type>();
            }
        }

        bool CanMakeGeneric(MethodInfo method)
        {
            var genArgs = method.GetGenericArguments();
            if (genArgs.Length == typeArgs.Length)
            {
                try
                {
                    method.MakeGenericMethod(typeArgs.Select((t, i) => t(method, i)).ToArray());
                    return true;
                }
                catch (ArgumentException)
                {
                    return false;
                }
            }
            else
            {
                return false;
            }
        }

        static ParameterInfo[] SafeGetParameters(MethodInfo method)
        {
            try
            {
                return method.GetParameters();
            }
            catch (FileNotFoundException)
            {
                return Array.Empty<ParameterInfo>();
            }
        }
    }

    internal static ILookup<string, ConfigLookup> GetMethodCalls(
       this ResolutionContext resolutionContext,
       IConfigurationSection directive,
       bool getChildren = true,
       IEnumerable<string>? exclude = null)
    {
        IEnumerable<IConfigurationSection> children;
        if (getChildren)
        {
            children = directive.GetChildren()
                .Where(x => exclude == null || !exclude.Contains(x.Key, StringComparer.OrdinalIgnoreCase))
                .ToList();
        }
        else
        {
            children = new[] { directive };
        }

        int count = 0;
        var arrayChildren = children.TakeWhile(x => int.TryParse(x.Key, out var current) && current == count++).ToList();
        var nonarrayChildren = children.Except(arrayChildren);

        IEnumerable<(string, ConfigLookup)>? result = Enumerable.Empty<(string, ConfigLookup)>();
        if (arrayChildren.Any())
        {
            result = from child in arrayChildren
                     where !string.IsNullOrEmpty(child.Value) && !bool.TryParse(child.Value, out var _) // Plain string
                     let childName = FromPlainString(child, resolutionContext)
                     select (childName.Name, new ConfigLookup(childName.TypeArgs, child, new Dictionary<string, (IConfigurationArgumentValue, IConfigurationSection)>()));
        }

        result = result.Union(from child in children
                              where string.IsNullOrEmpty(child.Value) || (bool.TryParse(child.Value, out var flag) && flag)
                              let n = GetSectionName(child, resolutionContext)
                              let callArgs = (from argument in child.GetArgs()
                                              select new
                                              {
                                                  Name = argument.Key,
                                                  Value = argument.GetArgumentValue(resolutionContext),
                                                  Source = child,
                                              }).ToDictionary(p => p.Name, p => (p.Value, p.Source))
                              select (n.Name, new ConfigLookup(n.TypeArgs, child, callArgs)));

        result = result.Union(from child in nonarrayChildren
                              where !string.IsNullOrEmpty(child.Value) && !bool.TryParse(child.Value, out var flag)
                              select (child.Key, new ConfigLookup(Array.Empty<TypeResolver>(), child, child.Blank())));

        return result
                .Where(x => exclude == null || !exclude.Contains(x.Item1, StringComparer.OrdinalIgnoreCase))
                .ToLookup(p => p.Item1, p => p.Item2);

        (string Name, TypeResolver[] TypeArgs) FromPlainString(IConfigurationSection s, ResolutionContext resolutionContext)
        {
            var value = s.Value;
            return resolutionContext.ReadTypeName(value!, s);
        }

        (string Name, TypeResolver[] TypeArgs) GetSectionName(IConfigurationSection s, ResolutionContext resolutionContext)
        {
            string name;

            if (int.TryParse(s.Key, out var result))
            {
                // parent uses an array json notation
                var nsection = s.GetSection("Name");

                if (nsection.Value == null)
                {
                    throw new InvalidOperationException($"The configuration value in {nsection.Path} has no 'Name' element.");
                }

                name = nsection.Value;
            }
            else
            {
                // parent uses an object json notation. We use the property name as the
                name = s.Key;
            }

            return resolutionContext.ReadTypeName(name, s);
        }
    }

    private static IEnumerable<IConfigurationSection> GetArgs(this IConfigurationSection parent)
    {
        // integer key indicate this section is a member of an array
        var excludeName = int.TryParse(parent.Key, out _);
        var args = parent.GetSection("Args");
        if (args.Exists())
        {
            return args.GetChildren();
        }
        else
        {
            return parent.GetChildren().Where(x => !excludeName || x.Key != "Name");
        }
    }

    public static bool IsArray(this IEnumerable<string> suppliedArgumentNames)
    {
        int count = 0;
        return suppliedArgumentNames.Any() && suppliedArgumentNames.All(i => int.TryParse(i, out var current) && current == count++);
    }

    internal static T SingleOrDefault<T>(this IEnumerable<T> source, FormattableString message)
    {
        T result = default!;
        var enumerator = source.GetEnumerator();
        if (!enumerator.MoveNext())
        {
            return result;
        }
        else
        {
            result = enumerator.Current!;
        }

        if (enumerator.MoveNext())
        {
            throw new InvalidOperationException($"{message} Matches are :{Environment.NewLine}{string.Join(Environment.NewLine, source)}");
        }
        else
        {
            return result;
        }
    }

    internal static MethodInfo? SelectConfigurationMethod(
        this IEnumerable<MethodInfo> candidateMethods,
        Type[] suppliedArgumentTypes)
    {
        return candidateMethods
           .OrderByDescending(m => m.GetParameters().Length)
           .FirstOrDefault(m =>
           {
               Span<ParameterInfo> parameters = m.GetParameters();
               if (m.IsStatic)
               {
                   parameters = parameters.Slice(1);
               }

               for (int i = 0; i < parameters.Length; i++)
               {
                   var parameter = parameters[i];
                   if (!parameter.IsOptional && parameter.ParameterType != suppliedArgumentTypes.ElementAtOrDefault(i))
                   {
                       return false;
                   }
               }

               return true;
           });
    }

    internal static MethodInfo? SelectConfigurationMethod(
        this IEnumerable<MethodInfo> candidateMethods,
        IEnumerable<string> suppliedArgumentNames,
        int? parameterSkip)
    {
        // Per issue #111, it is safe to use case-insensitive matching on argument names. The CLR doesn't permit this type
        // of overloading, and the Microsoft.Extensions.Configuration keys are case-insensitive (case is preserved with some
        // config sources, but key-matching is case-insensitive and case-preservation does not appear to be guaranteed).
        var selectedMethods = candidateMethods
            .Where(m => m.GetParameters()
                        .Skip(1 + (parameterSkip ?? 0))
                        .All(p => p.HasImplicitValueWhenNotSpecified() ||
                                  p.IsConfigurationOptionsBuilder(out _) ||
                                  p.ParameterType!.ParameterTypeHasPropertyMatches(suppliedArgumentNames) ||
                                  ParameterNameMatches(p.Name!, suppliedArgumentNames)))
            .GroupBy(m =>
            {
                var matchingArgs = m.GetParameters().Skip(parameterSkip ?? 0).Where(p => p.IsConfigurationOptionsBuilder(out _) || ParameterNameMatches(p.Name!, suppliedArgumentNames)).ToList();

                // Prefer the configuration method with most number of matching arguments and of those the ones with
                // the most string type parameters to predict best match with least type casting
                return (
                        matchingArgs.Count,
                        matchingArgs.Sum(p => p.ParameterType == typeof(string) ? 1 : p.GetConfigurationMatchCount(suppliedArgumentNames)));
            })
            .OrderByDescending(x => x.Key)
            .FirstOrDefault()?
            .AsEnumerable();

        MethodInfo? selectedMethod;
        if (selectedMethods?.Count() > 1)
        {
            // if no best match was found, use the one with a similar number of arguments based on the argument list
            selectedMethods = selectedMethods
               .Where(m =>
               {
                   var requiredParamCount = m.GetParameters().Skip(parameterSkip ?? 0).Count(x => !x.IsOptional);
                   return requiredParamCount <= suppliedArgumentNames.Count() + (m.IsStatic ? 1 : 0);
               })
               .ToList();

            if (selectedMethods.Count() > 1)
            {
                selectedMethod = selectedMethods.OrderBy(m => m.IsStatic ? 1 : 0).ThenByDescending(m => m.GetParameters().Skip(parameterSkip ?? 0).Count()).FirstOrDefault();
            }
            else
            {
                selectedMethod = selectedMethods.SingleOrDefault() ?? candidateMethods.SingleOrDefault(m => !m.GetParameters().Skip(1 + (parameterSkip ?? 0)).Any());
            }
        }
        else
        {
            selectedMethod = selectedMethods?.SingleOrDefault();
        }

        if (selectedMethod == null && suppliedArgumentNames.Count() == 1 && suppliedArgumentNames.All(string.IsNullOrEmpty))
        {
            selectedMethod = candidateMethods
                .FirstOrDefault(m => m.GetParameters().Length == 2);
        }

        return selectedMethod;
    }

    private static int GetConfigurationMatchCount(this ParameterInfo paramInfo, IEnumerable<string> parameterNames)
    {
        if (IsConfigurationOptionsBuilder(paramInfo, out var argumentType))
        {
            return parameterNames.Intersect(argumentType.GetProperties().Select(x => x.Name)).Count();
        }
        else
        {
            return 0;
        }
    }

    internal static bool HasImplicitValueWhenNotSpecified(this ParameterInfo paramInfo)
    {
        return paramInfo.HasDefaultValue

           // parameters of type IConfiguration are implicitly populated with provided Configuration
           || paramInfo.ParameterType == typeof(IConfiguration)
#if Generator
           || paramInfo.ParameterType == typeof(IConfigurationSection);
#else
           || paramInfo.ParameterType == typeof(IConfigurationSection)
           || paramInfo.ParameterType == typeof(IConfigurationProcessor);
#endif
    }

    internal static bool IsConfigurationOptionsBuilder(this ParameterInfo paramInfo, [NotNullWhen(true)] out Type? argumentType)
       => IsConfigurationOptionsBuilder(paramInfo.ParameterType, out argumentType);

    internal static bool IsConfigurationOptionsBuilder(this Type type, [NotNullWhen(true)] out Type? argumentType)
    {
        if (type.IsGenericType && type.FullName.StartsWith("System.Action`", StringComparison.Ordinal))
        {
            var genArgs = type.GetGenericArguments();
            if (genArgs.Length == 1)
            {
                argumentType = genArgs[0];
                return true;
            }
            else if (genArgs.Any(t => t == typeof(object)))
            {
                throw new NotSupportedException("An Action delegate with an argument type of System.Object is not supported");
            }
            else
            {
                argumentType = type;
                return true;
            }
        }

        argumentType = null;
        return false;
    }

    private static bool ParameterTypeHasPropertyMatches(this Type parameterType, IEnumerable<string> suppliedNames)
    {
        var parameterProps = parameterType.GetProperties().Select(x => x.Name).ToList();
        return suppliedNames.All(suppliedName => parameterProps.Any(x => suppliedName.Equals(x, StringComparison.OrdinalIgnoreCase)));
    }

    private static bool ParameterNameMatches(string actualParameterName, string suppliedName)
       => suppliedName.Equals(actualParameterName, StringComparison.OrdinalIgnoreCase);

    private static bool ParameterNameMatches(string actualParameterName, IEnumerable<string> suppliedNames)
       => suppliedNames.Any(s => ParameterNameMatches(actualParameterName, s));

    internal static IEnumerable? GetCollection(this ResolutionContext resolutionContext, IConfigurationSection configSection, MethodInfo method, int? parameterSkip)
    {
        var argValue = new ObjectArgumentValue(configSection);
        var collectionType = method.GetParameters().Skip(parameterSkip ?? 0).ElementAt(method.IsStatic && parameterSkip == null ? 1 : 0).ParameterType;
        return argValue.ConvertTo(method, collectionType, resolutionContext) as ICollection;
    }

    internal static string GetNameWithGenericArguments(this MethodInfo methodInfo)
    {
        if (methodInfo.IsGenericMethod)
        {
            return $"{methodInfo.Name}<{string.Join(", ", methodInfo.GetGenericArguments().Select(x => x.FullName))}>";
        }
        else
        {
            return methodInfo.Name;
        }
    }
}
