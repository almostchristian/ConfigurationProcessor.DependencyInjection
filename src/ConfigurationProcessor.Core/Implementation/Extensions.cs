// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.Core.Implementation
{
    internal static class Extensions
    {
        public static readonly MethodInfo BindMappableValuesMethod = ReflectionUtil.GetMethodInfo<object>(o => BindMappableValues(default!, default!, default!, default!, default!));
        private const string GenericTypePattern = "(?<typename>[a-zA-Z][a-zA-Z0-9\\.]+)<(?<genparam>.+)>";
        private static readonly Regex GenericTypeRegex = new Regex(GenericTypePattern, RegexOptions.Compiled);
        private const char GenericTypeMarker = '`';
        private const char GenericTypeParameterSeparator = '|';

        public static T SingleOrDefault<T>(this IEnumerable<T> source, FormattableString message)
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

        public static Dictionary<string, (IConfigurationArgumentValue Value, IConfigurationSection Section)> Blank(this IConfigurationSection section)
        {
            return new Dictionary<string, (IConfigurationArgumentValue, IConfigurationSection)>
                {
                    { string.Empty, (new StringArgumentValue(section.Value), section) },
                };
        }

        public static IEnumerable<IConfigurationSection> GetArgs(this IConfigurationSection parent)
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

        public static IConfigurationArgumentValue GetArgumentValue(this IConfigurationSection argumentSection, IReadOnlyCollection<Assembly> configurationAssemblies)
        {
            IConfigurationArgumentValue argumentValue;

            // Reject configurations where an element has both scalar and complex
            // values as a result of reading multiple configuration sources.
            if (argumentSection.Value != null && argumentSection.GetChildren().Any())
            {
                throw new InvalidOperationException(
                    $"The value for the argument '{argumentSection.Path}' is assigned different value " +
                    "types in more than one configuration source. Ensure all configurations consistently " +
                    "use either a scalar (int, string, boolean) or a complex (array, section, list, " +
                    "POCO, etc.) type for this argument value.");
            }

            if (argumentSection.Value != null)
            {
                argumentValue = new StringArgumentValue(argumentSection.Value);
            }
            else
            {
                argumentValue = new ObjectArgumentValue(argumentSection, configurationAssemblies);
            }

            return argumentValue;
        }

        public static bool IsArray(this IEnumerable<string> suppliedArgumentNames)
        {
            int count = 0;
            return suppliedArgumentNames.Any() && suppliedArgumentNames.All(i => int.TryParse(i, out var current) && current == count++);
        }

        public static List<MethodInfo> FindConfigurationExtensionMethods(
            this ResolutionContext resolutionContext,
            Type configType,
            TypeResolver[] typeArgs,
            List<string> candidateNames)
        {
            IReadOnlyCollection<Assembly> configurationAssemblies = resolutionContext.ConfigurationAssemblies;

            var candidateMethods = configurationAssemblies
                .SelectMany(a => a.SafeGetExportedTypes()
                    .Select(t => t.GetTypeInfo())
                    .Where(t => t.IsSealed && t.IsAbstract && !t.IsNested))
                .Union(new[] { configType.GetTypeInfo() })
                .SelectMany(t => candidateNames.SelectMany(n => t.GetDeclaredMethods(n)))
                .Where(m => !m.IsDefined(typeof(CompilerGeneratedAttribute), false) && m.IsPublic && ((m.IsStatic && m.IsDefined(typeof(ExtensionAttribute), false)) || m.DeclaringType == configType))
                .Where(m => !m.IsStatic || m.SafeGetParameters().ElementAtOrDefault(0)?.ParameterType.IsAssignableFrom(configType) == true) // If static method, checks that the first parameter is same as the extension type
                .ToList();

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
            }
        }

        public static (string TypeName, TypeResolver[] Resolvers) ReadTypeName(
            this ResolutionContext resolutionContext,
            string name,
            IConfiguration rootConfiguration,
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
                    targs.Add(resolutionContext.ReadGenericType(argument, rootConfiguration, ambientConfiguration));
                }

                return (typeName, targs.ToArray());
            }
            else
            {
                return (typeName, Array.Empty<TypeResolver>());
            }
        }

        public static TypeResolver ReadGenericType(this ResolutionContext resolutionContext, string typeName, IConfiguration rootConfiguration, IConfiguration ambientConfiguration)
        {
            var match = GenericTypeRegex.Match(typeName);
            if (match.Success)
            {
                typeName = match.Groups["typename"].Value;
                var typeArgs = match.Groups["genparam"].Value;

                var args = typeArgs.Split(GenericTypeParameterSeparator);

                var openGenType = resolutionContext.GetType($"{typeName}`{args.Length}", rootConfiguration, ambientConfiguration);
                return (m, i) => openGenType(m, i).MakeGenericType(args.Select((x, j) => resolutionContext.ReadGenericType(x, rootConfiguration, ambientConfiguration)(m, j)).ToArray());
            }
            else
            {
                return resolutionContext.GetType(typeName, rootConfiguration, ambientConfiguration);
            }
        }

        public static bool IsConfigurationOptionsBuilder(this ParameterInfo paramInfo, [NotNullWhen(true)] out Type? argumentType)
        {
            var type = paramInfo.ParameterType;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Action<>))
            {
                argumentType = type.GenericTypeArguments[0];

                // we only accept class types that contain a parameterless public constructor
                return argumentType.IsClass;
            }
            else
            {
                argumentType = null;
                return false;
            }
        }

        public static bool ParameterTypeHasPropertyMatches(this Type parameterType, IEnumerable<string> suppliedNames)
        {
            var parameterProps = parameterType.GetProperties().Select(x => x.Name).ToList();
            return suppliedNames.All(suppliedName => parameterProps.Any(x => suppliedName.Equals(x, StringComparison.OrdinalIgnoreCase)));
        }

        public static void BindMappableValues(
            object target,
            Type targetType,
            MethodInfo method,
            ResolutionContext resolutionContext,
            Dictionary<string, IConfigurationArgumentValue> configurationValues)
        {
            var properties = targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
            foreach (var property in properties.Where(p => p.CanWrite))
            {
                if (configurationValues.TryGetValue(property.Name.ToUpperInvariant(), out var configValue))
                {
                    property.SetValue(target, configValue.ConvertTo(method, property.PropertyType, resolutionContext));
                }
            }
        }

        internal static MethodInfo? SelectConfigurationMethod(
            this ResolutionContext resolutionContext,
            IEnumerable<MethodInfo> candidateMethods,
            IEnumerable<string> suppliedArgumentNames)
        {
            // Per issue #111, it is safe to use case-insensitive matching on argument names. The CLR doesn't permit this type
            // of overloading, and the Microsoft.Extensions.Configuration keys are case-insensitive (case is preserved with some
            // config sources, but key-matching is case-insensitive and case-preservation does not appear to be guaranteed).
            var selectedMethods = candidateMethods
                .Where(m => m.GetParameters()
                            .Skip(1)
                            .All(p => p.HasImplicitValueWhenNotSpecified() ||
                                      p.IsConfigurationOptionsBuilder(out _) ||
                                      p.ParameterType!.ParameterTypeHasPropertyMatches(suppliedArgumentNames) ||
                                      ParameterNameMatches(p.Name!, suppliedArgumentNames)))
                .GroupBy(m =>
                {
                    var matchingArgs = m.GetParameters().Where(p => p.IsConfigurationOptionsBuilder(out _) || ParameterNameMatches(p.Name!, suppliedArgumentNames)).ToList();

                    // Prefer the configuration method with most number of matching arguments and of those the ones with
                    // the most string type parameters to predict best match with least type casting
                    return (
                        matchingArgs.Count,
                        matchingArgs.Count(p => p.ParameterType == typeof(string)));
                })
                .OrderByDescending(x => x.Key)
                .FirstOrDefault()?
                .AsEnumerable();

            MethodInfo? selectedMethod;
            if (selectedMethods?.Count() > 1)
            {
                // if no best match was found, use the one with a similar number of arguments based on the argument list
                selectedMethods = selectedMethods.Where(m =>
                {
                    var requiredParamCount = m.GetParameters().Count(x => !x.IsOptional);
                    return requiredParamCount == suppliedArgumentNames.Count() + (m.IsStatic ? 1 : 0);
                });

                if (selectedMethods.Count() > 1)
                {
                    selectedMethod = selectedMethods.OrderBy(m => m.IsStatic ? 1 : 0).FirstOrDefault();
                }
                else
                {
                    selectedMethod = selectedMethods.SingleOrDefault();
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

        public static bool HasImplicitValueWhenNotSpecified(this ParameterInfo paramInfo)
        {
            return paramInfo.HasDefaultValue

               // parameters of type IConfiguration are implicitly populated with provided Configuration
               || paramInfo.ParameterType == typeof(IConfiguration)
               || paramInfo.ParameterType == typeof(IConfigurationSection);
        }

        private static bool ParameterNameMatches(string actualParameterName, string suppliedName)
        {
            return suppliedName.Equals(actualParameterName, StringComparison.OrdinalIgnoreCase);
        }

        private static bool ParameterNameMatches(string actualParameterName, IEnumerable<string> suppliedNames)
        {
            return suppliedNames.Any(s => ParameterNameMatches(actualParameterName, s));
        }

        private static IEnumerable<Type> SafeGetExportedTypes(this Assembly assembly)
        {
            try
            {
                return assembly.ExportedTypes;
            }
            catch (FileNotFoundException)
            {
                return Array.Empty<Type>();
            }
        }

        private static ParameterInfo[] SafeGetParameters(this MethodInfo method)
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
}
