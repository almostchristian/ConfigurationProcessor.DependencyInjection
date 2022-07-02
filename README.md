# ConfigurationProcessor.DependencyInjection

This library registers and configures services in a service collection using .NET's' configuration library.

## Example

Given an application with a `ConfigureServices` section like below:

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

The `ConfigureServices` method above can be moved to the configuration using the code and the appsettings.config configuration as in below:

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

Since we are using `IConfiguration`, we aren't limited to the appsettings.json for configuring our services. We can also have the configuration in environment variables, in command-line arguments or with custom configuration providers such as AWS Secrets Manager.

## Basics

The library works by using reflection and scanning all currently loaded assemblies for extension methods for `IServiceCollection`.

### Extension method mapping and overload resolution
Given a configuration named `Logging`, an extension method named `AddLogging` or `Logging` will be filtered from the candidate extension methods. If multiple candidates are found, the best overload will be chosen based on the name of the input parameters.

Given the following extension methods:
```csharp
public IServiceCollection AddMyPlugin(this IServiceCollection services);
public IServiceCollection AddMyPlugin(this IServiceCollection services, string name);
public IServiceCollection AddMyPlugin(this IServiceCollection services, int count);
```

When given the configuration below, the extension method `AddMyPlugin(IServiceCollection, int)` is chosen.
```json
{
   "Services": {
      "MyPlugin" : {
         "Count": 23
      }
   }
}
```

If the extension method is parameterless, use `true` instead of an object. The configuration method below will choose `AddMyPlugin(IServiceCollection)`
```json
{
   "Services": {
      "MyPlugin" : true
   }
}

```

### Action Delegate mapping
ConfigurationProcessor can be used with extension methods that use an action delegate.

Given the extension method below:
```csharp
public IServiceCollection AddMyService(this IServiceCollection services, Action<MyServiceOptions> configureOptions);

public class MyServiceOptions
{
   public string Title { get; set; }
}
```

The configuration below is equivalent to calling `services.AddMyService(options => {});`:
```json
{
   "MyPlugin" : true
}
```

The configuration below is equivalent to calling `services.AddMyService(options => { options.Title = "abc" });`:
```json
{
   "MyPlugin" : {
      "Title": "abc"
   }
}
```

### Generic extension method mapping
Generic extension methods can be mapped by supplying the generic parameter via the angle brackets `<>`. The full name of the type must be supplied.
```csharp
public IServiceCollection AddMyService<T>(this IServiceCollection services, T value);
```
```json
{
   "MyService<System.String>" : {
      "Value": "hello world"
   }
}
```

### Mapping to extension methods with a single array parameter
Extension methods that have a single array parameter can be mapped with json arrays.
```csharp
public IServiceCollection AddMyService(this IServiceCollection services, params string[] values);
```
```json
{
   "MyService" : [
      "salut",
      "hi",
      "konichiwa"
   ]
}
```

### Mapping to extension methods with a single dictionary parameter
Extension methods that have a single dictionary parameter with ***NO OVERLOADS*** can be mapped with json objects.
```csharp
public IServiceCollection AddMyService(this IServiceCollection services, Dictionary<string, int> values);
```
```json
{
   "MyService" : {
      "Value1": 1,
      "Value2": 2
   }
}
```