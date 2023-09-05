using ConfigurationProcessor.DependencyInjection;
using OpenTelemetry.Trace;

namespace TestWebApiGenerator;

internal static partial class ServiceRegistrationExtensions
{
    [GenerateServiceRegistration("Services")]
    public static partial void AddServicesFromConfiguration(this WebApplicationBuilder builder);

    //[GenerateServiceRegistration("Services")]
    //internal static partial void AddServicesFromConfiguration(this IServiceCollection services, IConfiguration configurationb);
}

/// <summary>
/// Target codegen output.
/// </summary>
static partial class ServiceRegistrationExtensions
{
    //public static partial void AddServicesFromConfiguration(this WebApplicationBuilder app)
    //    => app.Services.AddServicesFromConfiguration(app.Configuration);

    public static void AddServicesFromConfigurationX(this IServiceCollection services, IConfiguration configuration)
    {
        var servicesSection = configuration.GetSection("Services");
        if (!servicesSection.Exists())
        {
            return;
        }

        if (servicesSection.GetValue<bool>("Logging"))
        {
            services.AddLogging();
        }

        var hstsSection = servicesSection.GetSection("Hsts");
        if (hstsSection.Exists())
        {
            services.AddHsts(x =>
            {
                if (hstsSection.GetValue<bool>("ExcludedHosts:Clear"))
                {
                    x.ExcludedHosts.Clear();
                }

                x.Preload = hstsSection.GetValue<bool>("Preload");
                x.IncludeSubDomains = hstsSection.GetValue<bool>("IncludeSubDomains");
                x.MaxAge = TimeSpan.Parse(hstsSection.GetValue<string>("MaxAge"));
            });
        }

        var configureSection = servicesSection.GetSection("Configure<Microsoft.AspNetCore.Builder.CookiePolicyOptions>");
        if (configureSection.Exists())
        {
            services.Configure<CookiePolicyOptions>(options =>
            {
                options.HttpOnly = configureSection.GetValue<Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy>("HttpOnly");
                options.Secure = configureSection.GetValue<CookieSecurePolicy>("Secure");
            });
        }

        if (servicesSection.GetValue<bool>("Controllers"))
        {
            services.AddControllers();
        }

        if (servicesSection.GetValue<bool>("EndpointsApiExplorer"))
        {
            services.AddEndpointsApiExplorer();
        }

        if (servicesSection.GetValue<bool>("SwaggerGen"))
        {
            services.AddSwaggerGen();
        }

        var openTelemetrySection = servicesSection.GetSection("OpenTelemetryTracing");
        services.AddOpenTelemetryTracing(options =>
        {
            if (openTelemetrySection.GetValue<bool>("AspNetCoreInstrumentation"))
            {
                options.AddAspNetCoreInstrumentation();
            }

            if (openTelemetrySection.GetValue<bool>("HttpClientInstrumentation"))
            {
                options.AddHttpClientInstrumentation();
            }

            if (openTelemetrySection.GetValue<bool>("SqlClientInstrumentation"))
            {
                options.AddSqlClientInstrumentation();
            }

            if (openTelemetrySection.GetValue<bool>("JaegerExporter"))
            {
                options.AddJaegerExporter();
            }
        });
    }
}
