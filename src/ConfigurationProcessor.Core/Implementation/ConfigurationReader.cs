// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ConfigurationProcessor.Core.Assemblies;
using Microsoft.Extensions.Configuration;
using ConfigLookup = System.ValueTuple<ConfigurationProcessor.Core.Implementation.TypeResolver[], Microsoft.Extensions.Configuration.IConfigurationSection, System.Collections.Generic.Dictionary<string, (ConfigurationProcessor.Core.Implementation.IConfigurationArgumentValue ArgName, Microsoft.Extensions.Configuration.IConfigurationSection ConfigSection)>>;

namespace ConfigurationProcessor.Core.Implementation
{
   internal abstract class ConfigurationReader
   {
      private readonly IConfigurationSection section;
      private readonly MethodInfo[] additionalMethods;
      private readonly AssemblyFinder assemblyFinder;
      private readonly ResolutionContext resolutionContext;
      private readonly IConfiguration rootConfiguration;

      protected ConfigurationReader(
         ResolutionContext resolutionContext,
         IConfiguration rootConfiguration,
         AssemblyFinder assemblyFinder,
         IConfigurationSection configSection,
         MethodInfo[] additionalMethods)
      {
         this.resolutionContext = resolutionContext;
         this.rootConfiguration = rootConfiguration;
         this.assemblyFinder = assemblyFinder;
         this.section = configSection;
         this.additionalMethods = additionalMethods;
      }

      protected ResolutionContext ResolutionContext => this.resolutionContext;

      protected IConfigurationSection ConfigurationSection => this.section;

      internal ILookup<string, ConfigLookup> GetMethodCalls(
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
                     let childName = FromPlainString(child, this.resolutionContext)
                     select (childName.Name, new ConfigLookup(childName.TypeArgs, child, new Dictionary<string, (IConfigurationArgumentValue, IConfigurationSection)>()));
         }

         result = result.Union(from child in children
                               where string.IsNullOrEmpty(child.Value) || (bool.TryParse(child.Value, out var flag) && flag)
                               let n = GetSectionName(child, this.resolutionContext)
                               let callArgs = (from argument in child.GetArgs()
                                               select new
                                               {
                                                  Name = argument.Key,
                                                  Value = argument.GetArgumentValue(this.resolutionContext.ConfigurationAssemblies),
                                                  Source = child,
                                               }).ToDictionary(p => p.Name, p => (p.Value, p.Source))
                               select (n.Name, new ConfigLookup(n.TypeArgs, child, callArgs)));

         result = result.Union(from child in nonarrayChildren
                               where !string.IsNullOrEmpty(child.Value) && !bool.TryParse(child.Value, out var flag)
                               select (child.Key, new ConfigLookup(Array.Empty<TypeResolver>(), child, child.Blank())));

         return result
                 .Where(x => exclude == null || !exclude.Contains(x.Item1, StringComparer.OrdinalIgnoreCase))
                 .ToLookup(p => p.Item1, p => p.Item2);
      }

      private (string Name, TypeResolver[] TypeArgs) FromPlainString(IConfigurationSection s, ResolutionContext resolutionContext)
      {
         var value = s.Value;
         return resolutionContext.ReadTypeName(value, rootConfiguration, s);
      }

      private (string Name, TypeResolver[] TypeArgs) GetSectionName(IConfigurationSection s, ResolutionContext resolutionContext)
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

         return resolutionContext.ReadTypeName(name, rootConfiguration, s);
      }

      protected void CallConfigurationMethods(
          ResolutionContext resolutionContext,
          Type extensionArgumentType,
          ILookup<string, ConfigLookup> methods,
          MethodFilterFactory? methodFilterFactory,
          Action<List<object>, MethodInfo> invoker)
      {
         foreach (var method in methods.SelectMany(g => g.Select(x => new { g.Key, Value = x })))
         {
            var typeArgs = method.Value.Item1;
            var paramArgs = method.Value.Item3;
            methodFilterFactory ??= MethodFilterFactories.DefaultMethodFilterFactory;
            var (methodFilter, candidateNames) = methodFilterFactory(method.Key);
            IEnumerable<MethodInfo> configurationMethods = resolutionContext
               .FindConfigurationExtensionMethods(method.Key, extensionArgumentType, typeArgs, candidateNames, methodFilter);
            configurationMethods = configurationMethods.Union(additionalMethods.Where(m => candidateNames.Contains(m.Name) && methodFilter(m, method.Key))).ToList();
            var suppliedArgumentNames = paramArgs.Keys;

            var isCollection = suppliedArgumentNames.IsArray();
            MethodInfo? methodInfo;
            if (isCollection)
            {
               methodInfo = configurationMethods
                   .Where(m =>
                   {
                      var parameters = m.GetParameters();
                      if (parameters.Length != (m.IsStatic ? 2 : 1))
                      {
                         return false;
                      }

                      var paramType = parameters[m.IsStatic ? 1 : 0].ParameterType;
                      return paramType.IsArray || (paramType.IsGenericType && typeof(List<>) == paramType.GetGenericTypeDefinition());
                   })
                   .SingleOrDefault($"Ambigous match while searching for a method that accepts a list or array.");
            }
            else
            {
               methodInfo = resolutionContext.SelectConfigurationMethod(configurationMethods, suppliedArgumentNames);

               if (methodInfo == null)
               {
                  // if the method could still not be found, look method that accepts a single dictionary
                  methodInfo = configurationMethods
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

                  if (methodInfo != null)
                  {
                     paramArgs = new Dictionary<string, (IConfigurationArgumentValue ArgName, IConfigurationSection ConfigSection)>
                            {
                                { string.Empty, (new ObjectArgumentValue(method.Value.Item2, this.resolutionContext.ConfigurationAssemblies), method.Value.Item2) },
                            };
                  }
               }
            }

            if (methodInfo != null)
            {
               if (isCollection)
               {
                  var argValue = new ObjectArgumentValue(method.Value.Item2, this.resolutionContext.ConfigurationAssemblies);
                  var collectionType = methodInfo.GetParameters().ElementAt(1).ParameterType;
                  var collection = argValue.ConvertTo(methodInfo, collectionType, this.resolutionContext);
                  invoker(new List<object> { collection! }, methodInfo);
               }
               else
               {
                  var call = (from p in methodInfo.GetParameters().Skip<ParameterInfo>(methodInfo.IsStatic ? 1 : 0)
                              let directive = paramArgs.FirstOrDefault<KeyValuePair<string, (IConfigurationArgumentValue ArgName, IConfigurationSection ConfigSection)>>(s => string.IsNullOrEmpty(s.Key) || ParameterNameMatches(p.Name!, s.Key))
                              select directive.Key == null
                                  ? GetImplicitValueForNotSpecifiedKey(p, methodInfo, resolutionContext.RootConfiguration, paramArgs.FirstOrDefault().Value.ConfigSection, method.Key)
                                  : directive.Value.ArgName.ConvertTo(methodInfo, p.ParameterType, this.resolutionContext)).ToList<object>();
                  invoker(call, methodInfo);
               }
            }
            else
            {
               var methodsByName = configurationMethods
                   .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Skip(1).Select(p => p.Name))})")
                   .ToList();

               if (!methodsByName.Any())
               {
                  throw new MissingMethodException($"Unable to find methods called \"{string.Join(", ", candidateNames)}\". Candidate methods are:{Environment.NewLine}{string.Join(Environment.NewLine, configurationMethods)}");
               }
               else
               {
                  throw new MissingMethodException($"Unable to find methods called \"{string.Join(", ", candidateNames)}\" "
                  + (suppliedArgumentNames.Any()
                      ? "for supplied arguments: " + string.Join(", ", suppliedArgumentNames)
                      : "with no supplied arguments")
                  + ". Candidate methods are:"
                  + Environment.NewLine
                  + string.Join(Environment.NewLine, methodsByName));
               }
            }
         }
      }

      private object? GetImplicitValueForNotSpecifiedKey(
          ParameterInfo parameter,
          MethodInfo methodToInvoke,
          IConfiguration rootConfiguration,
          IConfigurationSection? sourceConfigurationSection,
          string originalKey)
      {
         if (parameter.IsConfigurationOptionsBuilder(out var argumentType))
         {
            var typeParameter = Expression.Parameter(argumentType);
            Expression bodyExpression;
            if (sourceConfigurationSection?.Exists() == true)
            {
               var methodExpressions = new List<Expression>();
               var currentResolutionContext = new ResolutionContext(assemblyFinder, rootConfiguration, sourceConfigurationSection, argumentType);

               // only do the binding if the argument type has a parameterless constructor
               if (argumentType.GetConstructor(Type.EmptyTypes) != null)
               {
                  // we want to return a generic lambda that calls bind c => configuration.Bind(c)
                  Expression<Action<object>> bindExpression = c => sourceConfigurationSection.Bind(c);
                  var bindMethodExpression = (MethodCallExpression)bindExpression.Body;
                  methodExpressions.Add(Expression.Call(bindMethodExpression.Method, bindMethodExpression.Arguments[0], typeParameter));

                  var argumentValues = sourceConfigurationSection.GetChildren().ToDictionary(x => x.Key.ToUpperInvariant(), x => x.GetArgumentValue(this.resolutionContext.ConfigurationAssemblies));
                  methodExpressions.Add(Expression.Call(Extensions.BindMappableValuesMethod, typeParameter, Expression.Constant(argumentType), Expression.Constant(methodToInvoke), Expression.Constant(currentResolutionContext), Expression.Constant(argumentValues)));
               }

               var excludeKeys = new HashSet<string>(argumentType.GetProperties().Select(x => x.Name).Union(new[] { originalKey }), StringComparer.OrdinalIgnoreCase);
               if (int.TryParse(sourceConfigurationSection.Key, out _))
               {
                  // integer key indicates that this is from an array
                  excludeKeys.Add("Name");
               }

               var methodCalls = GetMethodCalls(sourceConfigurationSection, true, excludeKeys);

               CallConfigurationMethods(currentResolutionContext, argumentType, methodCalls, null, (arguments, methodInfo) =>
               {
                  var parameters = methodInfo.GetParameters();

                  if (methodInfo.IsStatic)
                  {
                     var narguments = arguments.Select((x, i) => (Expression)Expression.Constant(x, parameters[i + 1].ParameterType)).ToList();
                     narguments.Insert(0, typeParameter);
                     methodExpressions.Add(Expression.Call(methodInfo, narguments));
                  }
                  else
                  {
                     var narguments = arguments.Select((x, i) => (Expression)Expression.Constant(x, parameters[i].ParameterType)).ToList();
                     methodExpressions.Add(Expression.Call(typeParameter, methodInfo, narguments));
                  }
               });

               bodyExpression = Expression.Block(methodExpressions);
            }
            else
            {
               bodyExpression = Expression.Empty();
            }

            var lambda = Expression.Lambda(typeof(Action<>).MakeGenericType(argumentType), bodyExpression, typeParameter).Compile();
            return lambda;
         }

         if (!parameter.HasImplicitValueWhenNotSpecified())
         {
            var parameterInstance = Activator.CreateInstance(parameter.ParameterType);

            sourceConfigurationSection.Bind(parameterInstance);

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
                                                          $"This is not supported when only a `IConfigSection` has been provided. (method '{methodToInvoke}')");
         }
         else if (parameter.ParameterType == typeof(IConfigurationSection))
         {
            return sourceConfigurationSection;
         }

         return parameter.DefaultValue;
      }

      private static bool ParameterNameMatches(string actualParameterName, string suppliedName)
      {
         return suppliedName.Equals(actualParameterName, StringComparison.OrdinalIgnoreCase);
      }
   }
}
