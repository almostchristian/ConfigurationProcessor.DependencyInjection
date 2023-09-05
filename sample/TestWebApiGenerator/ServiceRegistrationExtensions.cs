using ConfigurationProcessor.DependencyInjection;
using OpenTelemetry.Trace;

namespace TestWebApiGenerator;

internal static partial class ServiceRegistrationExtensions
{
    [GenerateServiceRegistration("Services")]
    public static partial void AddServicesFromConfiguration(this WebApplicationBuilder builder);

    // [GenerateServiceRegistration("Services")]
    // internal static partial void AddServicesFromConfiguration(this IServiceCollection services, IConfiguration configuration);
}
