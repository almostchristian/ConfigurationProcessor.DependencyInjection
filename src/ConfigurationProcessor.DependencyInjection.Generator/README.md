# ConfigurationProcessor.DependencyInjection.Generator

[![NuGet](https://img.shields.io/nuget/v/ConfigurationProcessor.DependencyInjection.Generator.svg?label=ConfigurationProcessor.DependencyInjection.Generator)](https://www.nuget.org/packages/ConfigurationProcessor.DependencyInjection.Generator)

This packages uses source generation to generate dependency injection registration methods based on the `appsettings.config` configuration. This is still in beta and the current version partially supports the configuration mechanisms available in the [ConfigurationProcessor.DependencyInjection](https://www.nuget.org/packages/ConfigurationProcessor.DependencyInjection/) package.

## Usage

### Code

```csharp
internal static partial class ServiceRegistrations
{
   [GenerateServiceRegistration("Services")]
   internal static partial void RegisterServices(this IServiceCollection services, IConfiguration configuration);

   [GenerateServiceRegistration("Services")]
   public static partial void AddServicesFromConfiguration(this WebApplicationBuilder builder);
}
```

Create a static partial method in a partial class decorated with `GenerateServiceRegistrationAttribute` and that contains an `IServiceCollection` and `IConfiguration` parameters or a single parameter with a parameter type that contains a single `IServiceCollection` and `IConfiguration` property.


The `GenerateServiceRegistrationAttribute` requires a single argument , `configurationSection` which will be the configuration section in the `appsettings.json` to read from.

### Sample configuration

```json
{
   "Logging": {
      "LogLevel": {
         "Default": "Information",
         "Microsoft.AspNetCore": "Warning"
      }
   },
   "AllowedHosts": "*",
   "Services": {
      "Logging": true,
      "Hsts": {
         "ExcludedHosts": {
            "Clear": true
         },
         "Preload": true,
         "IncludeSubDomains": true,
         "MaxAge": "356.00:00:00"
      },
      "Configure<Microsoft.AspNetCore.Builder.CookiePolicyOptions>": {
         "HttpOnly": "Always",
         "Secure": "Always"
      },
      "Controllers": true,
      "EndpointsApiExplorer": true,
      "SwaggerGen": true
   }
}
```

### Sample generated code

```csharp
public static partial void AddServicesFromConfiguration(this global::Microsoft.AspNetCore.Builder.WebApplicationBuilder builder)
{
    var servicesSection = builder.Configuration.GetSection("Services");
    if (!servicesSection.Exists())
    {
        return;
    }
         
    var sectionConfigure = servicesSection.GetSection("Configure<Microsoft.AspNetCore.Builder.CookiePolicyOptions>");
    if (sectionConfigure.Exists())
    {
        builder.Services.Configure<Microsoft.AspNetCore.Builder.CookiePolicyOptions>(options =>
        {
            options.HttpOnly = sectionConfigure.GetValue<Microsoft.AspNetCore.CookiePolicy.HttpOnlyPolicy>("HttpOnly");
            options.Secure = sectionConfigure.GetValue<Microsoft.AspNetCore.Http.CookieSecurePolicy>("Secure");
        });
    }
         
    if (servicesSection.GetValue<bool>("Controllers"))
    {
        builder.Services.AddControllers();
    }
         
    if (servicesSection.GetValue<bool>("EndpointsApiExplorer"))
    {
        builder.Services.AddEndpointsApiExplorer();
    }
         
    var sectionHsts = servicesSection.GetSection("Hsts");
    if (sectionHsts.Exists())
    {
        builder.Services.AddHsts(options =>
        {
            if (sectionHsts.GetValue<bool>("ExcludedHosts:Clear"))
            {
                options.ExcludedHosts.Clear();
            }
            options.IncludeSubDomains = sectionHsts.GetValue<System.Boolean>("IncludeSubDomains");
            options.Preload = sectionHsts.GetValue<System.Boolean>("Preload");
            options.MaxAge = sectionHsts.GetValue<System.TimeSpan>("MaxAge");
        });
    }
         
    if (servicesSection.GetValue<bool>("Logging"))
    {
        builder.Services.AddLogging();
    }
         
    var sectionOpenTelemetryTracing = servicesSection.GetSection("OpenTelemetryTracing");
         
    if (servicesSection.GetValue<bool>("SwaggerGen"))
    {
        builder.Services.AddSwaggerGen();
    }
}
```

## Issues and Contributions
Contributions are welcome. Please open an issue if you encounter a problem or have a use case you want to support that is not currently supported.