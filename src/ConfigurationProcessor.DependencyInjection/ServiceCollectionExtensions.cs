// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
using ConfigurationProcessor.Core;
using Microsoft.Extensions.Configuration;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Contains extension methods.
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds services from configuration.
        /// </summary>
        /// <typeparam name="TServices">The service collection.</typeparam>
        /// <param name="services">The service collection.</param>
        /// <param name="configuration">The configuration to read from.</param>
        /// <param name="servicesSection">The config section.</param>
        /// <param name="servicePaths">Additional service paths.</param>
        /// <param name="candidateMethodNameSuffixes">Candidate method name suffixes for matching.</param>
        /// <param name="surrogateMethods">Additional methods that can be used for matching.</param>
        /// <returns>The service collection for chaining.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="configuration"/> is null.</exception>
        public static TServices AddFromConfiguration<TServices>(
            this TServices services,
            IConfiguration configuration,
            string servicesSection,
            string[]? servicePaths = null,
            string[]? candidateMethodNameSuffixes = null,
            MethodInfo[]? surrogateMethods = null)
            where TServices : class, IEnumerable<ServiceDescriptor>
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            return configuration.ProcessConfiguration(
                services,
                servicesSection,
                servicePaths,
                candidateMethodNameSuffixes,
                surrogateMethods);
        }
    }
}
