using ConfigurationProcessor.DependencyInjection;
using OpenTelemetry.Trace;

namespace TestWebApiGenerator;

internal static partial class ServiceRegistrationExtensions
{
    [GenerateServiceRegistration("Services", ExcludedSections = new[] { "Hsts" })]
    public static partial void AddServicesFromConfiguration(this WebApplicationBuilder builder);

    // [GenerateServiceRegistration("Services")]
    // internal static partial void AddServicesFromConfiguration(this IServiceCollection services, IConfiguration configuration);
}
