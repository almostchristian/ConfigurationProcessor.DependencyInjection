﻿using ConfigurationProcessor;
using OpenTelemetry.Trace;

namespace TestWebApiGenerator;

internal static partial class ServiceRegistrationExtensions
{
    [GenerateConfiguration("Services", ExcludedSections = new[] { "Hsts" }, ImplicitSuffixes = new[] { "Instrumentation", "Exporter" }, ConfigurationPath = nameof(WebApplicationBuilder.Configuration))]
    public static partial void AddServicesFromConfiguration(this WebApplicationBuilder builder);

    // [GenerateServiceRegistration("Services")]
    // internal static partial void AddServicesFromConfiguration(this IServiceCollection services, IConfiguration configuration);
}
