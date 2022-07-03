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

The library works by using reflection and scanning all currently loaded assemblies for extension methods for `IServiceCollection`. This project was inspied by the [`Serilog.Settings.Configuration`](https://github.com/serilog/serilog-settings-configuration/) project.

### Extension method mapping and overload resolution
Given a configuration named `Logging`, an extension method named `AddLogging` or `Logging` will be filtered from the candidate extension methods. If multiple candidates are found, the best overload will be chosen based on the name of the input parameters.

Given the following extension methods:
```csharp
public IServiceCollection AddMyService(this IServiceCollection services);
public IServiceCollection AddMyService(this IServiceCollection services, string name);
public IServiceCollection AddMyService(this IServiceCollection services, int count);
```

When given the configuration below, the extension method `AddMyService(IServiceCollection, int)` is chosen.
```json
{
   "Services": {
      "MyService" : {
         "Count": 23
      }
   }
}
```

If the extension method is parameterless, use `true` instead of an object. The configuration method below will choose `AddMyService(IServiceCollection)`. A `false` value will prevent registration.
```json
{
   "Services": {
      "MyService" : true
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
   "MyService" : true
}
```

The configuration below is equivalent to calling `services.AddMyService(options => { options.Title = "abc" });`:
```json
{
   "MyService" : {
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
Extension methods that have a single array parameter can be mapped with json arrays. This will work with or without the `params` keyword. This can also work with a `List<>` parameter type.
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
Extension methods that have a single dictionary parameter can be mapped with json objects. Currently, only the parameter type `Dictionary<,>` is supported. *If one of the property names matches with a parameter name in an extension method overload, the overload with the matching parameter name is chosen instead of the dictionary overload.*
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

### Supported string mappings
Some .NET types can be mapped from a string in configuration. These additional mappings will not work in some binding scenarios.


|.NET Type                   |Example Configuration|C# Equivalent                   |
|----------------------------|---------------------|--------------------------------|
|`System.Uri`                |`"http://github.com"`|`new Uri("http://github.com")`
|`System.TimeSpan`           |`"00:00:30"`         |`TimeSpan.Parse("00:00:30", CultureInfo.InvariantCulture)`
|`System.Type`               |`"System.String"`    |`Type.GetType("System.String")`*|
|`System.Reflection.Assembly`|`"MyCorp.MyLib"`     |`Assembly.Load("MyCorp.MyLib")`*|

*For `Type` and `Assembly`, the loaded types and assemblies are searched first before resorting to `Type.GetType` or `Assembly.Load`.

#### Delegate mapping and static members
A parameter/property type that is a delegate can be mapped to a static method.

```csharp
namespace MyProject
{
   public static class Helpers
   {
      public IServiceCollection AddMyService(
         this IServiceCollection services,
         Action<MyConfiguration> configureAction);

      public static void LogOnError(ILogger instance, MyConfiguration configuration);
   }

   public class MyConfiguration
   {
      public Action<ILogger, MyConfiguration> OnError { get; set; }
   }
}
```

The configuration below maps to `services.AddMyService(config => config.OnError = Helpers.LogOnError)`
```json
{
   "MyService" : {
      "OnError": "MyProject.Helpers::LogOnError"
   }
}
```
