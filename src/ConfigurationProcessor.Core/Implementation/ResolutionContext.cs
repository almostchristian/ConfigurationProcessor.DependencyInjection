// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ConfigurationProcessor.Core.Assemblies;
using ConfigurationProcessor.Core.Implementation;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.Core.Implementation
{
   internal delegate Type TypeResolver(MethodInfo? method, int index);

   /// <summary>
   /// Keeps track of available elements that are useful when resolving values in the settings system.
   /// </summary>
   internal sealed class ResolutionContext
   {
      private readonly IConfiguration? appConfiguration;
      private readonly IConfiguration? rootConfiguration;

      public ResolutionContext(
         AssemblyFinder assemblyFinder,
         IConfiguration rootConfiguration,
         IConfigurationSection appConfiguration,
         MethodInfo[] additionalMethods,
         Action<ExtensionMethodNotFoundEventArgs> onExtensionMethodNotFound,
         params Type[] markerTypes)
      {
         if (assemblyFinder != null && appConfiguration != null)
         {
            ConfigurationAssemblies = LoadConfigurationAssemblies(appConfiguration, markerTypes, assemblyFinder);
         }
         else
         {
            ConfigurationAssemblies = new List<Assembly>();
         }

         OnExtensionMethodNotFound = onExtensionMethodNotFound;
         this.appConfiguration = appConfiguration;
         this.rootConfiguration = rootConfiguration;
         AssemblyFinder = assemblyFinder!;
         AdditionalMethods = additionalMethods;
      }

      public Action<ExtensionMethodNotFoundEventArgs> OnExtensionMethodNotFound { get; }

      public MethodInfo[] AdditionalMethods { get; }

      public AssemblyFinder AssemblyFinder { get; }

      public bool HasAppConfiguration => appConfiguration != null;

      public IConfiguration AppConfiguration
      {
         get
         {
            if (!HasAppConfiguration)
            {
               throw new InvalidOperationException("AppConfiguration is not available");
            }

            return appConfiguration!;
         }
      }

      public IConfiguration RootConfiguration
      {
         get
         {
            if (rootConfiguration == null)
            {
               throw new InvalidOperationException("RootConfiguration is not available");
            }

            return rootConfiguration!;
         }
      }

      public IReadOnlyCollection<Assembly> ConfigurationAssemblies { get; set; }

      public TypeResolver GetType(string typeName, IConfiguration rootConfiguration, IConfiguration ambientConfiguration)
      {
         if (typeName[0] == '!' || typeName[0] == '@')
         {
            var newTypeName = typeName.Substring(1).ToString();
            return (method, argIndex) =>
            {
               if (newTypeName.Contains('@'))
               {
                  var split = newTypeName.Split('@');
                  return ReflectionUtil.CreateType(split[0], GetType(split[1], rootConfiguration, ambientConfiguration)(method, argIndex));
               }
               else
               {
                  if (method == null)
                  {
                     throw new InvalidOperationException("Method cannot be null");
                  }

                  return ReflectionUtil.CreateType(newTypeName);
               }
            };
         }
         else
         {
            return (method, _) => GetTypeInternal(typeName);
         }
      }

      private Type GetTypeInternal(string typeName)
      {
         var find = Type.GetType(typeName);

         if (find == null)
         {
            find = (from asm in ConfigurationAssemblies
                    let t = asm.GetType(typeName)
                    where t != null
                    select t)
                    .Distinct() // Forwarded types can repeat
                    .SingleOrDefault();
         }

         if (find == null)
         {
            throw new InvalidOperationException($"Cannot find type {typeName}");
         }

         return find;
      }

      public Assembly FindAssembly(string assemblyName)
      {
         Assembly? find = (from asm in ConfigurationAssemblies
                           where asm.FullName == assemblyName
                           select asm).SingleOrDefault();

         if (find == null)
         {
            find = Assembly.Load(assemblyName);
         }

         return find;
      }

      private static IReadOnlyCollection<Assembly> LoadConfigurationAssemblies(IConfigurationSection section, Type[] markerTypes, AssemblyFinder assemblyFinder)
      {
         var startupAssembly = Assembly.GetEntryAssembly();
         var markerAssemblies = markerTypes.Select(x => x.Assembly).ToArray();
         var assemblies = markerAssemblies.ToDictionary(x => x.FullName!);

         if (startupAssembly != null)
         {
            assemblies[startupAssembly.FullName!] = startupAssembly;
         }

         var usingSection = section.GetSection("Using");
         if (usingSection.GetChildren().Any())
         {
            foreach (var simpleName in usingSection.GetChildren().Select(c => c.Value))
            {
               if (string.IsNullOrWhiteSpace(simpleName))
               {
                  throw new InvalidOperationException(
                      "A zero-length or whitespace assembly name was supplied to a FhirEngine.Using configuration statement.");
               }

               try
               {
                  var assembly = Assembly.Load(new AssemblyName(simpleName));
                  if (!assemblies.ContainsKey(assembly.FullName!))
                  {
                     assemblies.Add(assembly.FullName!, assembly);
                  }
               }
               catch (BadImageFormatException)
               {
                  // skip
               }
            }
         }

         foreach (var assemblyName in assemblyFinder.FindAssembliesReferencingAssembly(markerAssemblies))
         {
            try
            {
               var assumed = Assembly.Load(assemblyName);
               if (assumed != null && !assemblies.ContainsKey(assumed.FullName!))
               {
                  assemblies.Add(assumed.FullName!, assumed);
               }
            }
            catch (BadImageFormatException)
            {
               // skip
            }
         }

         return assemblies.Values.ToList().AsReadOnly();
      }
   }
}
