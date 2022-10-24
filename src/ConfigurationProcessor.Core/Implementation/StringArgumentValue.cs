// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.Core.Implementation
{
   internal class StringArgumentValue : IConfigurationArgumentValue
   {
      private readonly IConfigurationSection section;
      private readonly string providedValue;

      private static readonly Regex StaticMemberAccessorRegex = new Regex("^(?<shortTypeName>[^:]+)::(?<memberName>[A-Za-z][A-Za-z0-9]*)(?<typeNameExtraQualifiers>[^:]*)$");

      private static readonly Dictionary<Type, Func<string, ResolutionContext, object>> ExtendedTypeConversions = new Dictionary<Type, Func<string, ResolutionContext, object>>
        {
            { typeof(Type), (s, c) => c.GetType(s, c.RootConfiguration, c.AppConfiguration)(default, 0) },
            { typeof(Assembly), (s, c) => c.FindAssembly(s)! },
        };

      public StringArgumentValue(IConfigurationSection section, string providedValue)
      {
         this.section = section;
         this.providedValue = providedValue ?? throw new ArgumentNullException(nameof(providedValue));
      }

      public object? ConvertTo(MethodInfo configurationMethod, Type toType, ResolutionContext resolutionContext)
      {
         var argumentValue = Environment.ExpandEnvironmentVariables(providedValue);

         if (toType == typeof(string))
         {
            if (providedValue.StartsWith("$", StringComparison.Ordinal) && providedValue.EndsWith("$", StringComparison.Ordinal))
            {
               var key = providedValue.Substring(1, providedValue.Length - 2);
               return resolutionContext.RootConfiguration.GetValue<string>(key);
            }
            else
            {
               return providedValue;
            }
         }

         if (string.IsNullOrEmpty(argumentValue))
         {
            return null;
         }

         var toTypeInfo = toType.GetTypeInfo();
         if (toTypeInfo.IsGenericType && toType.GetGenericTypeDefinition() == typeof(Nullable<>))
         {
            if (string.IsNullOrEmpty(argumentValue))
            {
               return null;
            }

            // unwrap Nullable<> type since we're not handling null situations
            toType = toTypeInfo.GenericTypeArguments[0];
            toTypeInfo = toType.GetTypeInfo();
         }

         if (toTypeInfo.IsEnum)
         {
            return Enum.Parse(toType, argumentValue, true);
         }

         var convertor = ExtendedTypeConversions
             .Where(t => t.Key.GetTypeInfo().IsAssignableFrom(toTypeInfo))
             .Select(t => t.Value)
             .FirstOrDefault();

         if (convertor != null)
         {
            return convertor(argumentValue, resolutionContext);
         }

         if (toType == typeof(TimeSpan) && decimal.TryParse(section.Value, out _))
         {
            throw new FormatException("Invalid conversion from numeric to TimeSpan. Only strings are allowed.");
         }

         if ((toTypeInfo.IsInterface || toTypeInfo.IsAbstract || typeof(Delegate).IsAssignableFrom(toType) || typeof(MethodInfo) == toType) && !string.IsNullOrWhiteSpace(argumentValue))
         {
            // check if value looks like a static property or field directive
            // like "Namespace.TypeName::StaticProperty, AssemblyName"
            if (TryParseStaticMemberAccessor(argumentValue, out var accessorTypeName, out var memberName))
            {
               var accessorType = resolutionContext.GetType(accessorTypeName, resolutionContext.RootConfiguration, resolutionContext.AppConfiguration)(configurationMethod, 0);

               // if delegate, look for a method and then construct a delegate
               if (typeof(Delegate).IsAssignableFrom(toType) || typeof(MethodInfo) == toType)
               {
                  var methodCandidates = accessorType.GetTypeInfo().DeclaredMethods
                      .Where(x => x.Name == memberName)
                      .Where(x => x.IsPublic)
                      .Where(x => !x.IsGenericMethod)
                      .Where(x => x.IsStatic)
                      .ToList();

                  if (methodCandidates.Count > 1 && typeof(Delegate).IsAssignableFrom(toType))
                  {
                     // filter possible method overloads
                     var delegateSig = toType.GetMethod("Invoke");
                     var delegateParameters = delegateSig!.GetParameters().Select(x => x.ParameterType);
                     methodCandidates = methodCandidates
                         .Where(x => x.ReturnType == delegateSig.ReturnType && x.GetParameters().Select(y => y.ParameterType).SequenceEqual(delegateParameters))
                         .ToList();
                  }

                  var methodCandidate = methodCandidates.SingleOrDefault();

                  if (methodCandidate != null)
                  {
                     if (typeof(MethodInfo) == toType)
                     {
                        return methodCandidate;
                     }
                     else
                     {
                        return methodCandidate.CreateDelegate(toType);
                     }
                  }
               }

               // is there a public static property with that name ?
               var publicStaticPropertyInfo = accessorType.GetTypeInfo().DeclaredProperties
                   .Where(x => x.Name == memberName)
                   .Where(x => x.GetMethod != null)
                   .Where(x => x.GetMethod!.IsPublic)
                   .FirstOrDefault(x => x.GetMethod!.IsStatic);

               if (publicStaticPropertyInfo != null)
               {
                  return publicStaticPropertyInfo.GetValue(null); // static property, no instance to pass
               }

               // no property ? look for a public static field
               var publicStaticFieldInfo = accessorType.GetTypeInfo().DeclaredFields
                   .Where(x => x.Name == memberName)
                   .Where(x => x.IsPublic)
                   .FirstOrDefault(x => x.IsStatic);

               if (publicStaticFieldInfo != null)
               {
                  return publicStaticFieldInfo.GetValue(null); // static field, no instance to pass
               }

               throw new InvalidOperationException($"Could not find a public static non-generic method, property or field with name `{memberName}` on type `{accessorTypeName}`");
            }

            // maybe it's the assembly-qualified type name of a concrete implementation
            // with a default constructor
            var type = resolutionContext.GetType(argumentValue.Trim(), resolutionContext.RootConfiguration, resolutionContext.AppConfiguration)(configurationMethod, 0);

            var ctor = type.GetTypeInfo().DeclaredConstructors.FirstOrDefault(ci =>
            {
               var parameters = ci.GetParameters();
               return parameters.Length == 0 || parameters.All(pi => pi.HasDefaultValue);
            });

            if (ctor == null)
            {
               throw new InvalidOperationException($"A default constructor was not found on {type.FullName}.");
            }

            var call = ctor.GetParameters().Select(pi => pi.DefaultValue).ToArray();
            return ctor.Invoke(call);
         }

         return section.Get(toType);
      }

      internal static bool TryParseStaticMemberAccessor(string input, [NotNullWhen(true)] out string? accessorTypeName, [NotNullWhen(true)] out string? memberName)
      {
         if (input == null)
         {
            accessorTypeName = null;
            memberName = null;
            return false;
         }

         if (StaticMemberAccessorRegex.IsMatch(input))
         {
            var match = StaticMemberAccessorRegex.Match(input);
            var shortAccessorTypeName = match.Groups["shortTypeName"].Value;
            var rawMemberName = match.Groups["memberName"].Value;
            var extraQualifiers = match.Groups["typeNameExtraQualifiers"].Value;

            memberName = rawMemberName.Trim();
            accessorTypeName = shortAccessorTypeName.Trim() + extraQualifiers.TrimEnd();
            return true;
         }

         accessorTypeName = null;
         memberName = null;
         return false;
      }
   }
}
