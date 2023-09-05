// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.Extensions.Configuration;
using ConfigLookup = System.ValueTuple<ConfigurationProcessor.Core.Implementation.TypeResolver[], Microsoft.Extensions.Configuration.IConfigurationSection, System.Collections.Generic.Dictionary<string, (ConfigurationProcessor.Core.Implementation.IConfigurationArgumentValue ArgName, Microsoft.Extensions.Configuration.IConfigurationSection ConfigSection)>>;

namespace ConfigurationProcessor.Core.Implementation
{
    internal static class Extensions
    {
        public static readonly MethodInfo BindMappableValuesMethod = ReflectionUtil.GetMethodInfo<object>(o => BindMappableValues(default!, default!, default!, default!, default!, default!));

        public static void CallConfigurationMethod(
           this ResolutionContext resolutionContext,
           Type extensionArgumentType,
           string methodName,
           IConfigurationSection configSection,
           MethodFilterFactory? methodFilterFactory,
           TypeResolver[] typeArgs,
           Dictionary<string, (IConfigurationArgumentValue ArgName, IConfigurationSection ConfigSection)>? paramArgs,
           Func<List<object?>>? argumentFactory,
           Action<List<object?>, MethodInfo> invoker)
        {
            methodFilterFactory ??= MethodFilterFactories.DefaultMethodFilterFactory;

            var (methodFilter, candidateNames) = methodFilterFactory(methodName);
            IEnumerable<MethodInfo> configurationMethods = resolutionContext
               .FindConfigurationExtensionMethods(methodName, extensionArgumentType, typeArgs, candidateNames, methodFilter);
            configurationMethods = configurationMethods
               .Union(resolutionContext.AdditionalMethods.Where(m => candidateNames.Contains(m.Name) && methodFilter(m, methodName)))
               .Union(extensionArgumentType.GetMethods().Where(m => candidateNames.Contains(m.Name) && methodFilter(m, methodName)))
               .ToList();

            var suppliedArgumentNames = paramArgs?.Keys.ToArray() ?? Array.Empty<string>();

            var isCollection = suppliedArgumentNames.IsArray();
            MethodInfo? configurationMethod;

            var args = argumentFactory?.Invoke();
            int? parameterSkip = null;
            if (extensionArgumentType.FullName.StartsWith("System.Action`", StringComparison.Ordinal))
            {
                parameterSkip = extensionArgumentType.GetGenericArguments().Length;
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
                else if (args != null)
                {
                    configurationMethod = configurationMethods.SelectConfigurationMethod(args.Select(a => a?.GetType() ?? typeof(object)).ToArray());
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

            if (configurationMethod != null)
            {
                if (argumentFactory != null && args != null)
                {
                    invoker(args, configurationMethod);
                }
                else if (isCollection)
                {
                    var collection = resolutionContext.GetCollection(configSection!, configurationMethod, parameterSkip);
                    invoker(new List<object?> { collection }, configurationMethod);
                }
                else
                {
                    var parameters = configurationMethod.GetParameters().Skip(parameterSkip ?? (configurationMethod.IsStatic && parameterSkip == null ? 1 : 0)).ToArray();
                    args = new List<object?>();

                    for (int i = 0; i < parameters.Length; i++)
                    {
                        var p = parameters[i];
                        var directive = paramArgs.FirstOrDefault(s => string.IsNullOrEmpty(s.Key) || ParameterNameMatches(p.Name!, s.Key));
                        var arg = (directive.Key == null || (string.IsNullOrEmpty(directive.Key) && i > 0)) ?
                           resolutionContext.GetImplicitValueForNotSpecifiedKey(p, configurationMethod, paramArgs.FirstOrDefault().Value.ConfigSection, methodName)! :
                           directive.Value.ArgName.ConvertTo(configurationMethod, p.ParameterType, resolutionContext, p.Name)!;
                        args.Add(arg);
                    }

                    invoker(args, configurationMethod);
                }

                resolutionContext.OnExtensionMethodFound?.Invoke(methodName);
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
                   methodName,
                   extensionArgumentType,
                   paramArgs?.ToDictionary(x => x.Key, x => x.Value.ConfigSection));

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
            Action<List<object?>, MethodInfo> invoker)
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

        internal static void ThrowMissingMethodException(ExtensionMethodNotFoundEventArgs args)
        {
            string message;
            var methods = args.CandidateMethods
                      .Select(m => $"{m.Name}({string.Join(", ", m.GetParameters().Skip(1).Select(p => p.Name))})")
                      .ToList();

            if (args.SuppliedArguments != null)
            {
                message = $"Unable to find methods called \"{string.Join(", ", args.CandidateNames)}\" for type '{args.ExtensionMethodType}' "
                      + (args.SuppliedArguments.Any()
                          ? "for supplied named arguments: " + string.Join(", ", args.SuppliedArguments.Keys)
                          : "with no supplied arguments")
                      + ". Candidate methods are:"
                      + Environment.NewLine
                      + string.Join(Environment.NewLine, methods);
            }
            else
            {
                message = $"Unable to find methods called \"{string.Join(", ", args.CandidateNames)}\" for type '{args.ExtensionMethodType}' "
                      + ". Candidate methods are:"
                      + Environment.NewLine
                      + string.Join(Environment.NewLine, methods);
            }

            throw new MissingMethodException(message);
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

            Action<List<object?>, MethodInfo> invoker = (arguments, methodInfo) => methodInfo.InvokeWithArguments(target, arguments);
            if (target is object[] args && targetType.FullName.StartsWith("System.Action`", StringComparison.Ordinal))
            {
                invoker = (arguments, methodInfo) =>
                {
                    arguments.InsertRange(0, args);
                    methodInfo.InvokeWithArguments(null, arguments);
                };
            }

            resolutionContext.CallConfigurationMethods(
               targetType,
               sourceConfigurationSection,
               true,
               excludeKeys,
               null,
               invoker);
        }

        internal static void InvokeWithArguments(this MethodInfo methodInfo, object? target, List<object?> arguments)
        {
            if (methodInfo.IsStatic && target != null)
            {
                arguments.Insert(0, target);
                target = null;
            }

            var parameters = methodInfo.GetParameters();
            for (int i = 0; i < parameters.Length; i++)
            {
                object? argument;
                if (arguments.Count > i)
                {
                    argument = arguments[i];
                }
                else
                {
                    argument = null;
                    arguments.Add(null);
                }

                var expectedArgType = parameters[i].ParameterType;

                if (argument != null && argument.GetType().IsValueTupleCompatible(expectedArgType))
                {
                    // we convert the tuple to a compatible type
                    arguments[i] = Activator.CreateInstance(expectedArgType, GetElements());

                    object[] GetElements()
                    {
#if NETSTANDARD2_1_OR_GREATER
                  var tuple = (ITuple)argument;
                  return Enumerable.Range(0, tuple.Length).Select(i => tuple[i]).ToArray();
#else
                        var elements = argument.GetType().GetFields();
                        return elements.Select(e => e.GetValue(argument)).ToArray();
#endif
                    }
                }
            }

            try
            {
                methodInfo.Invoke(target, arguments.ToArray());
            }
            catch (TargetInvocationException invocationEx)
            {
                throw invocationEx.InnerException;
            }
        }

        internal static Delegate GenerateLambda(
           this ResolutionContext resolutionContext,
           MethodInfo configurationMethod,
           IConfigurationSection? sourceConfigurationSection,
           Type argumentType,
           string? originalKey)
        {
            if (argumentType.FullName.StartsWith("System.Action`", StringComparison.Ordinal))
            {
                var tracker = new ChildContextMissingMethodTracker(resolutionContext);
                var childResolutionContext = new ResolutionContext(
                   resolutionContext.AssemblyFinder,
                   resolutionContext.RootConfiguration,
                   sourceConfigurationSection!,
                   resolutionContext.AdditionalMethods,
                   tracker.OnExtensionMethodNotFound,
                   tracker.OnExtensionMethodFound,
                   argumentType);

                var validateExpression = Expression.Call(Expression.Constant(tracker), nameof(ChildContextMissingMethodTracker.Validate), Type.EmptyTypes);
                var genArgs = argumentType.GetGenericArguments();

                var parameterExpressions = new List<ParameterExpression>();
                var bodyExpressions = new List<Expression>();

                foreach (var genArg in genArgs)
                {
                    var (parameterExpression, bodyExpression) = BuildExpressionWithParam(childResolutionContext, genArg);
                    parameterExpressions.Add(parameterExpression);
                    bodyExpressions.Add(bodyExpression);
                }

                var combinedArg = Expression.NewArrayInit(typeof(object), parameterExpressions.ToArray());
                var combinedExpression = BuildExpression(childResolutionContext, combinedArg, argumentType);
                bodyExpressions.Add(combinedExpression);
                bodyExpressions.Add(validateExpression);
                var combinedBody = Expression.Block(bodyExpressions.ToArray());
                var lambda = Expression.Lambda(argumentType, combinedBody, parameterExpressions.ToArray()).Compile();
                return lambda;
            }
            else
            {
                var childResolutionContext = new ResolutionContext(
                   resolutionContext.AssemblyFinder,
                   resolutionContext.RootConfiguration,
                   sourceConfigurationSection!,
                   resolutionContext.AdditionalMethods,
                   resolutionContext.OnExtensionMethodNotFound,
                   null,
                   argumentType);
                var (parameterExpression, bodyExpression) = BuildExpressionWithParam(childResolutionContext, argumentType);

                var lambda = Expression.Lambda(typeof(Action<>).MakeGenericType(argumentType), bodyExpression, parameterExpression).Compile();
                return lambda;
            }

            (ParameterExpression, Expression) BuildExpressionWithParam(ResolutionContext childResolutionContext, Type argumentType)
            {
                var parameterExpression = Expression.Parameter(argumentType);
                return (parameterExpression, BuildExpression(childResolutionContext, parameterExpression, argumentType));
            }

            Expression BuildExpression(ResolutionContext childResolutionContext, Expression parameterExpression, Type argumentType)
            {
                Expression bodyExpression;
                if (sourceConfigurationSection?.Exists() == true)
                {
                    var methodExpressions = new List<Expression>();

                    var keysToExclude = originalKey != null ? new List<string> { originalKey } : new List<string>();
                    if (int.TryParse(sourceConfigurationSection.Key, out _))
                    {
                        // integer key indicates that this is from an array
                        keysToExclude.Add("Name");
                    }

                    // we want to return a generic lambda that calls bind c => configuration.Bind(c)
                    if (!parameterExpression.Type.IsValueType)
                    {
                        Expression<Action<object>> bindExpression = c => sourceConfigurationSection.Bind(c);
                        var bindMethodExpression = (MethodCallExpression)bindExpression.Body;
                        methodExpressions.Add(Expression.Call(bindMethodExpression.Method, bindMethodExpression.Arguments[0], parameterExpression));
                    }

                    methodExpressions.Add(
                       Expression.Call(
                          BindMappableValuesMethod,
                          Expression.Constant(childResolutionContext),
                          parameterExpression,
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

                return bodyExpression;
            }
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
                return new ConfigurationHelperImplementation(resolutionContext, sourceConfigurationSection!);
            }

            return parameter.DefaultValue;
        }

        internal static ILookup<string, ConfigLookup> GetMethodCalls(
           this ConfigurationReader configurationReader,
           IConfigurationSection directive,
           bool getChildren = true,
           IEnumerable<string>? exclude = null)
           => configurationReader.ResolutionContext.GetMethodCalls(directive, getChildren, exclude);

        internal static Dictionary<string, (IConfigurationArgumentValue Value, IConfigurationSection Section)> Blank(this IConfigurationSection section)
        {
            return new Dictionary<string, (IConfigurationArgumentValue, IConfigurationSection)>
                {
                    { string.Empty, (new StringArgumentValue(section, section.Value, section.Key), section) },
                };
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
                argumentValue = new StringArgumentValue(argumentSection, argumentSection.Value, argumentSection.Key);
            }
            else
            {
                argumentValue = new ObjectArgumentValue(argumentSection);
            }

            return argumentValue;
        }

        private static bool ParameterNameMatches(string actualParameterName, string suppliedName)
           => suppliedName.Equals(actualParameterName, StringComparison.OrdinalIgnoreCase);
    }
}
