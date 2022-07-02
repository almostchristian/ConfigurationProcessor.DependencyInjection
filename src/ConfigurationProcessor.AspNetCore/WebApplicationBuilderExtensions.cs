// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using Microsoft.AspNetCore.Builder;

namespace Microsoft.Extensions.DependencyInjection
{
    /// <summary>
    /// Contains extension methods.
    /// </summary>
    public static class WebApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds services from configuration.
        /// </summary>
        /// <param name="builder">The application builder.</param>
        /// <param name="servicesSection">The config section that contains the services.</param>
        /// <returns>The <paramref name="builder"/> for chaining.</returns>
        public static WebApplicationBuilder AddServicesFromConfiguration(this WebApplicationBuilder builder, string servicesSection)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Services.AddFromConfiguration(builder.Configuration, servicesSection);
            return builder;
        }
    }
}