// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;
using ConfigLookup = System.ValueTuple<ConfigurationProcessor.Core.Implementation.TypeResolver[], Microsoft.Extensions.Configuration.IConfigurationSection, System.Collections.Generic.Dictionary<string, (ConfigurationProcessor.Core.Implementation.IConfigurationArgumentValue ArgName, Microsoft.Extensions.Configuration.IConfigurationSection ConfigSection)>>;

namespace ConfigurationProcessor.Core.Implementation
{
   internal static class Extensions
   {
      public static readonly MethodInfo BindMappableValuesMethod = ReflectionUtil.GetMethodInfo<object>(o => BindMappableValues(default!, default!, default!, default!, default!, default!));
      private const string GenericTypePattern = "(?<typename>[a-zA-Z][a-zA-Z0-9\\.]+)<(?<genparam>.+)>";
      private static readonly Regex GenericTypeRegex = new Regex(GenericTypePattern, RegexOptions.Compiled);
      private const char GenericTypeMarker = '`';
      private const char GenericTypeParameterSeparator = '|';

      public static void CallConfigurationMethod(
         this ResolutionContext resolutionContext,
         Type extensionArgumentType,
         string methodName,
         IConfigurationSection configSection,
         MethodFilterFactory? methodFilterFactory,
         TypeResolver[] typeArgs,
         Dictionary<string, (IConfigurationArgumentValue ArgName, IConfigurationSection ConfigSection)>? paramArgs,
         Func<List<object>>? argumentFactory,
         Action<List<object>, MethodInfo> invoker)
      {
         methodFilterFactory ??= MethodFilterFactories.DefaultMethodFilterFactory;

         var (methodFilter, candidateNames) = methodFilterFactory(methodName);
         IEnumerable<MethodInfo> configurationMethods = resolutionContext
            .FindConfigurationExtensionMethods(methodName, extensionArgumentType, typeArgs, candidateNames, methodFilter);
         configurationMethods = configurationMethods.Union(resolutionContext.AdditionalMethods.Where(m => candidateNames.Contains(m.Name) && methodFilter(m, methodName))).ToList();

         var suppliedArgumentNames = paramArgs?.Keys.ToArray() ?? Array.Empty<string>();

         var isCollection = suppliedArgumentNames.IsArray();
         MethodInfo? configurationMethod;

         var args = argumentFactory?.Invoke();

         if (isCollection)
         {
            configurationMethod = configurationMethods
                .Where(m =>
                {
                   var parameters = m.GetParameters();
                   if (parameters.Length != (m.IsStatic ? 2 : 1))
                   {
                      return false;
                   }

                   var paramType = parameters[m.IsStatic ? 1 : 0].ParameterType;
                   var isCollection = paramType.IsArray || (paramType.IsGenericType && typeof(List<>) == paramType.GetGenericTypeDefinition());

                   if (isCollection)
                   {
#pragma warning disable CA1031 // Do not catch general exception types
                      try
                      {
#pragma warning disable S1481 // Unused local variables should be removed
                         var collection = GetCollection(resolutionContext, configSection!, m);
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
                     var parameters = m.GetParameters();
                     System.Diagnostics.Debug.WriteLine(parameters.Count(p => !p.HasDefaultValue));
                     if (parameters.Count(p => !p.HasDefaultValue && !p.HasImplicitValueWhenNotSpecified()) != (m.IsStatic ? 2 : 1))
                     {
                        return false;
                     }

                     var paramType = m.GetParameters().Where(p => !p.HasImplicitValueWhenNotSpecified()).ElementAt(m.IsStatic ? 1 : 0).ParameterType;
                     var isCollection = paramType.IsArray || (paramType.IsGenericType && typeof(List<>) == paramType.GetGenericTypeDefinition());
                     if (isCollection)
                     {
                        return false;
                     }

#pragma warning disable CA1031 // Do not catch general exception types
                     try
                     {
                        var argValue = new StringArgumentValue(configSection, argvalue);
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
                  configurationMethods.SingleOrDefault(m => m.GetParameters().Count(p => !p.HasDefaultValue) == (m.IsStatic ? 1 : 0));
            }
            else if (args != null)
            {
               configurationMethod = configurationMethods.SelectConfigurationMethod(args.Select(a => a.GetType()));
            }
            else
            {
               configurationMethod = configurationMethods.SelectConfigurationMethod(suppliedArgumentNames);
            }

            if (configurationMethod == null)
            {
               // if the method could still not be found, look method that accepts a single dictionary
               configurationMethod = configurationMethods
                   .Where(m =>
                   {
                      var parameters = m.GetParameters();
                      if (parameters.Length != (m.IsStatic ? 2 : 1))
                      {
                         return false;
                      }

                      var paramType = parameters[m.IsStatic ? 1 : 0].ParameterType;
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

         if (configurationMethod != null)
         {
            if (argumentFactory != null && args != null)
            {
               invoker(args, configurationMethod);
            }
            else if (isCollection)
            {
               var collection = GetCollection(resolutionContext, configSection!, configurationMethod);
               invoker(new List<object> { collection! }, configurationMethod);
            }
            else
            {
               var parameters = configurationMethod.GetParameters().Skip(configurationMethod.IsStatic ? 1 : 0).ToArray();
               args = new List<object>();

               for (int i = 0; i < parameters.Length; i++)
               {
                  var p = parameters[i];
                  var directive = paramArgs.FirstOrDefault(s => string.IsNullOrEmpty(s.Key) || ParameterNameMatches(p.Name!, s.Key));
                  var arg = (directive.Key == null || (string.IsNullOrEmpty(directive.Key) && i > 0)) ?
                     resolutionContext.GetImplicitValueForNotSpecifiedKey(p, configurationMethod, paramArgs.FirstOrDefault().Value.ConfigSection, methodName)! :
                     directive.Value.ArgName.ConvertTo(configurationMethod, p.ParameterType, resolutionContext)!;
                  args.Add(arg);
               }

               invoker(args, configurationMethod);
            }
         }
         else
         {
            if (!configurationMethods.Any())
            {
               configurationMethods = resolutionContext
                  .FindConfigurationExtensionMethods(methodName, extensionArgumentType, typeArgs, null, methodFilter)
                  .Distinct();
            }

            var errorEventArgs = new ExtensionMethodNotFoundEventArgs(
               configurationMethods,
               candidateNames,
               extensionArgumentType,
               paramArgs.ToDictionary(x => x.Key, x => x.Value.ConfigSection));

            resolutionContext.OnExtensionMethodNotFound(errorEventArgs);

            if (!errorEventArgs.Handled)
            {
               ThrowMissingMethodException(errorEventArgs);
            }
         }
      }

      public static void CallConfigurationMethods(
          this ResolutionContext resolutionContext,
          Type extensionArgumentType,
          IConfigurationSection directive,
          bool getChildren,
          IEnumerable<string>? exclude,
          MethodFilterFactory? methodFilterFactory,
          Action<List<object>, MethodInfo> invoker)
      {
         var methods = resolutionContext.GetMethodCalls(directive, getChildren, exclude);
         foreach (var (methodName, (typeArgs, configSection, configArgs)) in methods.SelectMany(g => g.Select(x => (MethodName: g.Key, Config: x))))
         {
            var paramArgs = configArgs;
            CallConfigurationMethod(
               resolutionContext,
               extensionArgumentType,
               methodName,
               configSection,
               methodFilterFactory,
               typeArgs,
               paramArgs,
               null,
               invoker);
         }
      }

      private static IEnumerable? GetCollection(ResolutionContext resolutionContext, IConfigurationSection configSection, MethodInfo method)
      {
         var argValue = new ObjectArgumentValue(configSection);
         var collectionType = method.GetParameters().ElementAt(method.IsStatic ? 1 : 0).ParameterType;
         return argValue.ConvertTo(method, collectionType, resolutionContext) as ICollection;
      }

      private static void ThrowMissingMethodException(ExtensionMethodNotFoundEventArgs args)
      {
         string message;
         var methods = args.CandidateMethods
                   .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Skip(1).Select(p => p.Name))})")
                   .ToList();
         message = $"Unable to find methods called \"{string.Join(", ", args.CandidateNames)}\" for type '{args.ExtensionMethodType}' "
               + (args.SuppliedArguments.Any()
                   ? "for supplied named arguments: " + string.Join(", ", args.SuppliedArguments.Keys)
                   : "with no supplied arguments")
               + ". Candidate methods are:"
               + Environment.NewLine
               + string.Join(Environment.NewLine, methods);

         throw new MissingMethodException(message);
      }

      private static List<MethodInfo> FindConfigurationExtensionMethods(
          this ResolutionContext resolutionContext,
          string key,
          Type configType,
          TypeResolver[] typeArgs,
          IEnumerable<string>? candidateNames,
          MethodFilter? filter)
      {
         IReadOnlyCollection<Assembly> configurationAssemblies = resolutionContext.ConfigurationAssemblies;
         var interfaces = configType.GetInterfaces();
         var candidateMethods = configurationAssemblies
             .SelectMany(a => SafeGetExportedTypes(a)
                 .Select(t => t.GetTypeInfo())
                 .Where(t => t.IsSealed && t.IsAbstract && !t.IsNested))
             .Union(new[] { configType.GetTypeInfo() })
             .Concat(interfaces.Select(t => t.GetTypeInfo()))
             .SelectMany(t => candidateNames != null ? candidateNames.SelectMany(n => t.GetDeclaredMethods(n)) : t.DeclaredMethods)
             .Where(m => filter == null || filter(m, key))
             .Where(m => !m.IsDefined(typeof(CompilerGeneratedAttribute), false) && m.IsPublic && ((m.IsStatic && m.IsDefined(typeof(ExtensionAttribute), false)) || m.DeclaringType == configType || interfaces.Contains(m.DeclaringType)))
             .Where(m => !m.IsStatic || SafeGetParameters(m).ElementAtOrDefault(0)?.ParameterType.IsAssignableFrom(configType) == true) // If static method, checks that the first parameter is same as the extension type
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

      private static (string TypeName, TypeResolver[] Resolvers) ReadTypeName(
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

            var openGenType = resolutionContext.GetType($"{typeName}`{args.Length}", rootConfiguration, ambientConfiguration);
            return (m, i) => openGenType(m, i).MakeGenericType(args.Select((x, j) => resolutionContext.ReadGenericType(x, rootConfiguration, ambientConfiguration)(m, j)).ToArray());
         }
         else
         {
            return resolutionContext.GetType(typeName, rootConfiguration, ambientConfiguration);
         }
      }

      public static void BindMappableValues(
          this ResolutionContext resolutionContext,
          object target,
          Type targetType,
          MethodInfo configurationMethod,
          IConfigurationSection sourceConfigurationSection,
          params string[] excludedKeys)
      {
         var properties = targetType.GetProperties(BindingFlags.Instance | BindingFlags.Public);
         var configurationValues = sourceConfigurationSection.GetChildren().ToDictionary(x => x.Key.ToUpperInvariant(), x => x.GetArgumentValue(resolutionContext));

         foreach (var property in properties.Where(p => p.CanWrite))
         {
            if (configurationValues.TryGetValue(property.Name.ToUpperInvariant(), out var configValue))
            {
               property.SetValue(target, configValue.ConvertTo(configurationMethod, property.PropertyType, resolutionContext));
            }
         }

         var excludeKeys = new HashSet<string>(properties.Select(x => x.Name).Union(excludedKeys), StringComparer.OrdinalIgnoreCase);

         resolutionContext.CallConfigurationMethods(
            targetType,
            sourceConfigurationSection,
            true,
            excludeKeys,
            null,
            (arguments, methodInfo) =>
            {
               if (methodInfo.IsStatic)
               {
                  var nargs = arguments.ToList();
                  nargs.Insert(0, target);
                  methodInfo.Invoke(null, nargs.ToArray());
               }
               else
               {
                  methodInfo.Invoke(target, arguments.ToArray());
               }
            });
      }

      private static Delegate GenerateLambda(
         this ResolutionContext resolutionContext,
         MethodInfo configurationMethod,
         IConfigurationSection? sourceConfigurationSection,
         Type argumentType,
         string originalKey)
      {
         var typeParameter = Expression.Parameter(argumentType);
         Expression bodyExpression;
         if (sourceConfigurationSection?.Exists() == true)
         {
            var methodExpressions = new List<Expression>();

            var childResolutionContext = new ResolutionContext(resolutionContext.AssemblyFinder, resolutionContext.RootConfiguration, sourceConfigurationSection, resolutionContext.AdditionalMethods, resolutionContext.OnExtensionMethodNotFound, argumentType);

            var keysToExclude = new List<string> { originalKey };
            if (int.TryParse(sourceConfigurationSection.Key, out _))
            {
               // integer key indicates that this is from an array
               keysToExclude.Add("Name");
            }

            // we want to return a generic lambda that calls bind c => configuration.Bind(c)
            Expression<Action<object>> bindExpression = c => sourceConfigurationSection.Bind(c);
            var bindMethodExpression = (MethodCallExpression)bindExpression.Body;
            methodExpressions.Add(Expression.Call(bindMethodExpression.Method, bindMethodExpression.Arguments[0], typeParameter));

            methodExpressions.Add(
               Expression.Call(
                  BindMappableValuesMethod,
                  Expression.Constant(childResolutionContext),
                  typeParameter,
                  Expression.Constant(argumentType),
                  Expression.Constant(configurationMethod),
                  Expression.Constant(sourceConfigurationSection),
                  Expression.Constant(keysToExclude.ToArray())));

            bodyExpression = Expression.Block(methodExpressions);
         }
         else
         {
            bodyExpression = Expression.Empty();
         }

         var lambda = Expression.Lambda(typeof(Action<>).MakeGenericType(argumentType), bodyExpression, typeParameter).Compile();
         return lambda;
      }

      private static object? GetImplicitValueForNotSpecifiedKey(
         this ResolutionContext resolutionContext,
         ParameterInfo parameter,
         MethodInfo configurationMethod,
         IConfigurationSection? sourceConfigurationSection,
         string originalKey)
      {
         if (parameter.IsConfigurationOptionsBuilder(out var argumentType))
         {
            return resolutionContext.GenerateLambda(configurationMethod, sourceConfigurationSection, argumentType, originalKey);
         }

         if (!parameter.HasImplicitValueWhenNotSpecified())
         {
            var parameterInstance = Activator.CreateInstance(parameter.ParameterType);
            BindMappableValues(resolutionContext, parameterInstance, parameter.ParameterType, configurationMethod, sourceConfigurationSection!, originalKey);
            return parameterInstance;
         }

         if (parameter.ParameterType == typeof(IConfiguration))
         {
            if (resolutionContext.HasAppConfiguration)
            {
               return resolutionContext.AppConfiguration;
            }

            if (parameter.HasDefaultValue)
            {
               return parameter.DefaultValue;
            }

            throw new InvalidOperationException("Trying to invoke a configuration method accepting a `IConfiguration` argument. " +
                                                          $"This is not supported when only a `IConfigSection` has been provided. (method '{configurationMethod}')");
         }
         else if (parameter.ParameterType == typeof(IConfigurationSection))
         {
            return sourceConfigurationSection;
         }
         else if (parameter.ParameterType == typeof(IConfigurationProcessor))
         {
            return new ConfigurationHelperImplementation(resolutionContext, sourceConfigurationSection!, null);
         }

         return parameter.DefaultValue;
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
            return resolutionContext.ReadTypeName(value, s);
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

      internal static ILookup<string, ConfigLookup> GetMethodCalls(
         this ConfigurationReader configurationReader,
         IConfigurationSection directive,
         bool getChildren = true,
         IEnumerable<string>? exclude = null)
         => configurationReader.ResolutionContext.GetMethodCalls(directive, getChildren, exclude);

      private static T SingleOrDefault<T>(this IEnumerable<T> source, FormattableString message)
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

      private static MethodInfo? SelectConfigurationMethod(
          this IEnumerable<MethodInfo> candidateMethods,
          IEnumerable<Type> suppliedArgumentTypes)
      {
         return candidateMethods
            .FirstOrDefault(m =>
            {
               IEnumerable<ParameterInfo> parameters = m.GetParameters();
               if (m.IsStatic)
               {
                  parameters = parameters.Skip(1);
               }

               return parameters.Select(x => x.ParameterType).SequenceEqual(suppliedArgumentTypes);
            });
      }

      private static MethodInfo? SelectConfigurationMethod(
          this IEnumerable<MethodInfo> candidateMethods,
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
                  var requiredParamCount = m.GetParameters().Count(x => !x.IsOptional);
                  return requiredParamCount <= suppliedArgumentNames.Count() + (m.IsStatic ? 1 : 0);
               })
               .ToList();

            if (selectedMethods.Count() > 1)
            {
               selectedMethod = selectedMethods.OrderBy(m => m.IsStatic ? 1 : 0).ThenByDescending(m => m.GetParameters().Length).FirstOrDefault();
            }
            else
            {
               selectedMethod = selectedMethods.SingleOrDefault() ?? candidateMethods.SingleOrDefault(m => !m.GetParameters().Skip(1).Any());
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

      private static bool IsArray(this IEnumerable<string> suppliedArgumentNames)
      {
         int count = 0;
         return suppliedArgumentNames.Any() && suppliedArgumentNames.All(i => int.TryParse(i, out var current) && current == count++);
      }

      private static Dictionary<string, (IConfigurationArgumentValue Value, IConfigurationSection Section)> Blank(this IConfigurationSection section)
      {
         return new Dictionary<string, (IConfigurationArgumentValue, IConfigurationSection)>
                {
                    { string.Empty, (new StringArgumentValue(section, section.Value), section) },
                };
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

      public static IConfigurationArgumentValue GetArgumentValue(this IConfigurationSection argumentSection, ResolutionContext resolutionContext)
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
            argumentValue = new StringArgumentValue(argumentSection, argumentSection.Value);
         }
         else
         {
            argumentValue = new ObjectArgumentValue(argumentSection);
         }

         return argumentValue;
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

      private static bool HasImplicitValueWhenNotSpecified(this ParameterInfo paramInfo)
      {
         return paramInfo.HasDefaultValue

            // parameters of type IConfiguration are implicitly populated with provided Configuration
            || paramInfo.ParameterType == typeof(IConfiguration)
            || paramInfo.ParameterType == typeof(IConfigurationSection)
            || paramInfo.ParameterType == typeof(IConfigurationProcessor);
      }

      private static bool IsConfigurationOptionsBuilder(this ParameterInfo paramInfo, [NotNullWhen(true)] out Type? argumentType)
         => IsConfigurationOptionsBuilder(paramInfo.ParameterType, out argumentType);

      private static bool IsConfigurationOptionsBuilder(this Type type, [NotNullWhen(true)] out Type? argumentType)
      {
         if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Action<>))
         {
            argumentType = type.GenericTypeArguments[0];

            // we only accept class types that contain a parameterless public constructor
            return true;
         }
         else
         {
            argumentType = null;
            return false;
         }
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
   }
}
