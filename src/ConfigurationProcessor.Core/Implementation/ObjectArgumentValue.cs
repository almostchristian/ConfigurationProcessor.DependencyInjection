// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.Core.Implementation
{
   internal class ObjectArgumentValue : IConfigurationArgumentValue
   {
      private readonly IConfigurationSection section;
      private readonly IReadOnlyCollection<Assembly> configurationAssemblies;

      public ObjectArgumentValue(IConfigurationSection section, IReadOnlyCollection<Assembly> configurationAssemblies)
      {
         this.section = section ?? throw new ArgumentNullException(nameof(section));

         // used by nested logger configurations to feed a new pass by ConfigurationReader
         this.configurationAssemblies = configurationAssemblies ?? throw new ArgumentNullException(nameof(configurationAssemblies));
      }

      public object ConvertTo(MethodInfo method, Type toType, ResolutionContext resolutionContext)
      {
         // return the entire section for internal processing
         if (toType == typeof(IConfigurationSection))
         {
            return section;
         }

         if (toType.IsArray)
         {
            return CreateArray();
         }

         if (IsContainer(toType, out var elementType) && TryCreateContainer(out var result))
         {
            return result!;
         }

         // MS Config binding can work with a limited set of primitive types and collections
         return section.Get(toType);

         object CreateArray()
         {
            var elementType = toType.GetElementType();
            var configurationElements = section.GetChildren().ToArray();
            var result = Array.CreateInstance(elementType!, configurationElements.Length);
            for (int i = 0; i < configurationElements.Length; ++i)
            {
               var argumentValue = configurationElements[i].GetArgumentValue(configurationAssemblies);
               var value = argumentValue.ConvertTo(method, elementType!, resolutionContext);
               result.SetValue(value, i);
            }

            return result;
         }

         bool TryCreateContainer(out object? result)
         {
            result = null;

            if (toType.GetConstructor(Type.EmptyTypes) == null)
            {
               return false;
            }

            // Is dictionary
            if (elementType.IsGenericType && elementType.GetGenericTypeDefinition() == typeof(KeyValuePair<,>))
            {
               var keyType = elementType.GetGenericArguments()[0];
               var valueType = elementType.GetGenericArguments()[1];

               var addMethod = toType.GetMethods().FirstOrDefault(m => !m.IsStatic && m.Name == "Add" && m.GetParameters()?.Length == 2 && m.GetParameters()[0].ParameterType == keyType && m.GetParameters()[1].ParameterType == valueType);
               if (addMethod == null)
               {
                  return false;
               }

               var configurationElements = section.GetChildren().ToArray();
               result = Activator.CreateInstance(toType);

               for (int i = 0; i < configurationElements.Length; ++i)
               {
                  var keyValue = new StringArgumentValue(configurationElements[i].Key);
                  var argumentValue = configurationElements[i].GetArgumentValue(configurationAssemblies);
                  var key = keyValue.ConvertTo(method, keyType, resolutionContext);
                  var value = argumentValue.ConvertTo(method, valueType, resolutionContext);
                  addMethod.Invoke(result, new object[] { key!, value! });
               }
            }
            else
            {
               // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/classes-and-structs/object-and-collection-initializers#collection-initializers
               var addMethod = toType.GetMethods().FirstOrDefault(m => !m.IsStatic && m.Name == "Add" && m.GetParameters()?.Length == 1 && m.GetParameters()[0].ParameterType == elementType);
               if (addMethod == null)
               {
                  return false;
               }

               var configurationElements = section.GetChildren().ToArray();
               result = Activator.CreateInstance(toType);

               for (int i = 0; i < configurationElements.Length; ++i)
               {
                  var argumentValue = configurationElements[i].GetArgumentValue(configurationAssemblies);
                  var value = argumentValue.ConvertTo(method, elementType, resolutionContext);
                  addMethod.Invoke(result, new object[] { value! });
               }
            }

            return true;
         }
      }

      private static bool IsContainer(Type type, [NotNullWhen(true)] out Type? elementType)
      {
         elementType = null;
         foreach (var iface in type.GetInterfaces())
         {
            if (iface.IsGenericType && iface.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
               elementType = iface.GetGenericArguments()[0];
               return true;
            }
         }

         return false;
      }
   }
}
