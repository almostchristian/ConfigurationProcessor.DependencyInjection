// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reflection;
using ConfigurationProcessor.Core;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
   /// <summary>
   /// Contains extension methods.
   /// </summary>
   public static class ConfigurationProcessorServiceCollectionExtensions
   {
      /// <summary>
      /// Adds services from configuration.
      /// </summary>
      /// <param name="services">The service collection.</param>
      /// <param name="configuration">The configuration to read from.</param>
      /// <param name="servicesSection">The config section.</param>
      /// <returns>The service collection for chaining.</returns>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
      public static IServiceCollection AddFromConfiguration(
          this IServiceCollection services,
          IConfiguration configuration,
          string servicesSection)
         => services.AddFromConfiguration(configuration, servicesSection, null, default(MethodFilterFactory), default);

      /// <summary>
      /// Adds services from configuration.
      /// </summary>
      /// <param name="services">The service collection.</param>
      /// <param name="configuration">The configuration to read from.</param>
      /// <param name="servicesSection">The config section.</param>
      /// <param name="servicePaths">Additional service paths.</param>
      /// <param name="candidateMethodNameSuffixes">Candidate method name suffixes for matching.</param>
      /// <param name="additionalMethods">Additional methods that can be used for matching.</param>
      /// <returns>The service collection for chaining.</returns>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
      public static IServiceCollection AddFromConfiguration(
          this IServiceCollection services,
          IConfiguration configuration,
          string servicesSection,
          string[]? servicePaths,
          string[]? candidateMethodNameSuffixes,
          MethodInfo[]? additionalMethods = null)
         => services.AddFromConfiguration(
            configuration,
            servicesSection,
            servicePaths,
            candidateMethodNameSuffixes != null ? MethodFilterFactories.WithSuffixes(candidateMethodNameSuffixes) : null,
            additionalMethods);

      /// <summary>
      /// Adds services from configuration.
      /// </summary>
      /// <param name="services">The service collection.</param>
      /// <param name="configuration">The configuration to read from.</param>
      /// <param name="servicesSection">The config section.</param>
      /// <param name="servicePaths">Additional service paths.</param>
      /// <param name="methodFilterFactory">Factory for creating method filters.</param>
      /// <param name="additionalMethods">Additional methods that can be used for matching.</param>
      /// <returns>The service collection for chaining.</returns>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
      public static IServiceCollection AddFromConfiguration(
          this IServiceCollection services,
          IConfiguration configuration,
          string servicesSection,
          string[]? servicePaths,
          MethodFilterFactory? methodFilterFactory,
          MethodInfo[]? additionalMethods = null)
         => services.AddFromConfiguration(configuration, options =>
         {
            options.ConfigSection = servicesSection;
            options.ContextPaths = servicePaths;
            options.MethodFilterFactory = methodFilterFactory;
            options.AdditionalMethods = additionalMethods ?? Enumerable.Empty<MethodInfo>();
         });

      /// <summary>
      /// Adds services from configuration.
      /// </summary>
      /// <param name="services">The service collection.</param>
      /// <param name="configuration">The configuration to read from.</param>
      /// <param name="configureOptions">The config options.</param>
      /// <returns>The service collection for chaining.</returns>
      /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
      public static IServiceCollection AddFromConfiguration(
          this IServiceCollection services,
          IConfiguration configuration,
          Action<ConfigurationReaderOptions> configureOptions)
      {
         if (configuration == null)
         {
            throw new ArgumentNullException(nameof(configuration));
         }

         return configuration.ProcessConfiguration(
             services,
             configureOptions);
      }
   }
}
