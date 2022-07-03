// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
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
      /// <typeparam name="TServices">The object type that is transformed by the configuration.</typeparam>
      /// <param name="configuration">The configuration object.</param>
      /// <param name="context">The object that is processed by the configuration.</param>
      /// <param name="configSection">The section in the config that will be used in the configuration.</param>
      /// <param name="contextPaths">Additional paths that will be searched.</param>
      /// <param name="candidateMethodNameSuffixes">Candidate method name suffixes for matching.</param>
      /// <param name="surrogateMethods">Additional methods that can be used for matching.</param>
      /// <returns>The <paramref name="context"/> object for chaining.</returns>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
      public static TServices ProcessConfiguration<TServices>(
          this IConfiguration configuration,
          TServices context,
          string configSection,
          string[]? contextPaths = null,
          string[]? candidateMethodNameSuffixes = null,
          MethodInfo[]? surrogateMethods = null)
          where TServices : class
      {
         if (configuration == null)
         {
            throw new ArgumentNullException(nameof(configuration));
         }

         context.AddFromConfiguration(
             configuration,
             configuration.GetSection(configSection),
             contextPaths ?? new string?[] { string.Empty },
             candidateMethodNameSuffixes ?? Array.Empty<string>(),
             surrogateMethods ?? Array.Empty<MethodInfo>(),
             AssemblyFinder.Auto());
         return context;
      }

      internal static TConfig AddFromConfiguration<TConfig>(
          this TConfig builder,
          IConfiguration rootConfiguration,
          IConfigurationSection configurationSection,
          string?[] servicePaths,
          string[] candidateMethodNameSuffixes,
          MethodInfo[] surrogateMethods,
          AssemblyFinder assemblyFinder)
          where TConfig : class
      {
         var reader = new ConfigurationReader<TConfig>(configurationSection, assemblyFinder, surrogateMethods, rootConfiguration);

         foreach (var servicePath in servicePaths)
         {
            if (string.IsNullOrEmpty(servicePath))
            {
               reader.AddServices(builder, null, true, candidateMethodNameSuffixes);
            }
            else if (servicePath![0] == '^')
            {
               reader.AddServices(builder, servicePath.Substring(1), false, candidateMethodNameSuffixes);
            }
            else
            {
               reader.AddServices(builder, servicePath, true, candidateMethodNameSuffixes);
            }
         }

         return builder;
      }
   }
}
