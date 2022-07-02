# ConfigurationProcessor.DependencyInjection

This library registers and configures services in an `IServiceCollection` using .NET's' configuration library.

## Example

Given an application with a configure services section like below:

```csharp
services.AddLogging();
services.AddHsts(options =>
{
   options.Preload = true;
   options.IncludeSubDomains = true;
   options.MaxAge = TimeSpan.FromDays(365);
});
services.Configure<CookiePolicyOptions>(options =>
{
   options.HttpOnly = HttpOnlyPolicy.Always;
   options.Secure = CookieSecurePolicy.Always;
});
```

The configure services method above can be moved to the configuration as in below:

```csharp
services.AddFromConfiguration(Configuration, "Services");
```

```json
{
   "Services": {
      "Logging": true,
      "Hsts": {
      "Preload": true,
         "IncludeSubDomains": true,
         "MaxAge": "356.00:00:00"
      },
      "Configure<Microsoft.AspNetCore.Builder.CookiePolicyOptions>": {
         "HttpOnly": "Always",
         "Secure": "Always"
      }
   }
}
```

## Features
List down the features and limitations

Under construction