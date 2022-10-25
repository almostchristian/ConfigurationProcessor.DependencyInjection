// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Data.Common;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.Core.Implementation
{
   internal class ObjectArgumentValue : IConfigurationArgumentValue
   {
      private readonly IConfigurationSection section;

      public ObjectArgumentValue(IConfigurationSection section)
      {
         this.section = section ?? throw new ArgumentNullException(nameof(section));
      }

      public object ConvertTo(MethodInfo configurationMethod, Type toType, ResolutionContext resolutionContext, string? providedKey = null)
      {
         // return the entire section for internal processing
         if (toType == typeof(IConfigurationSection))
         {
            return section;
         }

         if (section.Value == null && !section.Exists())
         {
            return default!;
         }

         if (toType.IsArray)
         {
            return CreateArray();
         }

         if (toType.IsConfigurationOptionsBuilder(out var argumentType))
         {
            return resolutionContext.GenerateLambda(configurationMethod, section, argumentType, null);
         }

         if (IsContainer(toType, out var elementType) && TryCreateContainer(out var result))
         {
            return result!;
         }

         var newInstance = Activator.CreateInstance(toType);
         resolutionContext.BindMappableValues(newInstance, toType, configurationMethod, section);
         return newInstance;

         object CreateArray()
         {
            var elementType = toType.GetElementType();
            var configurationElements = section.GetChildren().ToArray();
            var result = Array.CreateInstance(elementType!, configurationElements.Length);
            for (int i = 0; i < configurationElements.Length; ++i)
            {
               var argumentValue = configurationElements[i].GetArgumentValue(resolutionContext);
               var value = argumentValue.ConvertTo(configurationMethod, elementType!, resolutionContext);
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
                  var keyValue = new StringArgumentValue(configurationElements[i], configurationElements[i].Key, string.Empty);
                  var argumentValue = configurationElements[i].GetArgumentValue(resolutionContext);
                  var key = keyValue.ConvertTo(configurationMethod, keyType, resolutionContext);
                  var value = argumentValue.ConvertTo(configurationMethod, valueType, resolutionContext);
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
                  var argumentValue = configurationElements[i].GetArgumentValue(resolutionContext);
                  var value = argumentValue.ConvertTo(configurationMethod, elementType, resolutionContext);
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
