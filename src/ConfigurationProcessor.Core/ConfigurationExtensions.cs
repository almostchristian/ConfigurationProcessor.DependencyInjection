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

namespace ConfigurationProcessor.Core
{
   /// <summary>
   /// Contains extensions methods.
   /// </summary>
   public static class ConfigurationExtensions
   {
      /// <summary>
      /// Processes the configuration.
      /// </summary>
      /// <typeparam name="TContext">The object type that is transformed by the configuration.</typeparam>
      /// <param name="configuration">The configuration object.</param>
      /// <param name="context">The object that is processed by the configuration.</param>
      /// <param name="configSection">The section in the config that will be used in the configuration.</param>
      /// <param name="contextPaths">Additional paths that will be searched.</param>
      /// <param name="methodFilterFactory">Factory for filtering methods.</param>
      /// <param name="additionalMethods">Additional methods that can be used for matching.</param>
      /// <returns>The <paramref name="context"/> object for chaining.</returns>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
      public static TContext ProcessConfiguration<TContext>(
          this IConfiguration configuration,
          TContext context,
          string configSection,
          string[]? contextPaths = null,
          MethodFilterFactory? methodFilterFactory = null,
          MethodInfo[]? additionalMethods = null)
          where TContext : class
      {
         if (configuration == null)
         {
            throw new ArgumentNullException(nameof(configuration));
         }

         context.AddFromConfiguration(
             configuration,
             configuration.GetSection(configSection),
             contextPaths ?? new string?[] { string.Empty },
             methodFilterFactory,
             additionalMethods ?? Array.Empty<MethodInfo>(),
             AssemblyFinder.Auto());
         return context;
      }

      internal static TConfig AddFromConfiguration<TConfig>(
          this TConfig builder,
          IConfiguration rootConfiguration,
          IConfigurationSection configurationSection,
          string?[] servicePaths,
          MethodFilterFactory? methodFilterFactory,
          MethodInfo[] additionalMethods,
          AssemblyFinder assemblyFinder)
          where TConfig : class
      {
         var reader = new ConfigurationReader<TConfig>(configurationSection, assemblyFinder, additionalMethods, rootConfiguration);

         foreach (var servicePath in servicePaths)
         {
            if (string.IsNullOrEmpty(servicePath))
            {
               reader.AddServices(builder, null, true, methodFilterFactory);
            }
            else if (servicePath![0] == '^')
            {
               reader.AddServices(builder, servicePath.Substring(1), false, methodFilterFactory);
            }
            else
            {
               reader.AddServices(builder, servicePath, true, methodFilterFactory);
            }
         }

         return builder;
      }
   }
}
