# ConfigurationProcessor.DependencyInjection

[![NuGet](https://img.shields.io/nuget/v/ConfigurationProcessor.DependencyInjection.svg?label=ConfigurationProcessor.DependencyInjection)](https://www.nuget.org/packages/ConfigurationProcessor.DependencyInjection)
[![NuGet](https://img.shields.io/nuget/dt/ConfigurationProcessor.DependencyInjection.svg)](https://www.nuget.org/packages/ConfigurationProcessor.DependencyInjection)


[![NuGet](https://img.shields.io/nuget/v/ConfigurationProcessor.AspNetCore.svg?label=ConfigurationProcessor.AspNetCore)](https://www.nuget.org/packages/ConfigurationProcessor.AspNetCore)
[![NuGet](https://img.shields.io/nuget/dt/ConfigurationProcessor.AspNetCore.svg)](https://www.nuget.org/packages/ConfigurationProcessor.AspNetCore)

This library registers and configures services in a service collection using .NET's' configuration library.

## Example

Given an application with the following `ConfigureServices` section:

```csharp
services.AddLogging();
services.AddHsts(options =>
{
   options.ExcludedHosts.Clear();
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

The `ConfigureServices` method above can be moved into the configuration using the following code and appsettings.config configuration:

```csharp
// .NET 5.0, .NET Core 3.1 and below using ConfigurationProcessor.DependencyInjection
services.AddFromConfiguration(Configuration, "Services");

// .NET 6.0 with ConfigurationProcessor.AspNetCore
var builder = WebApplication.CreateBuilder(args);
builder.AddFromConfiguration("Services");
```

```json
{
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
      }
   }
}
```

Since we are using `IConfiguration`, we aren't limited to the appsettings.json for configuring our services. We can also have the configuration in environment variables, in command-line arguments or with custom configuration providers such as AWS Secrets Manager.

## Basics

The library works by using reflection and scanning all currently loaded assemblies for extension methods for `IServiceCollection`. This project was inspired by the [`Serilog.Settings.Configuration`](https://github.com/serilog/serilog-settings-configuration/) project.

### Extension method mapping and overload resolution
Given a configuration named `MyService`, an extension method named `AddMyService` or `MyService` will be filtered from the candidate extension methods. If multiple candidates are found, the best overload will be chosen based on the name of the input parameters.

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
ConfigurationProcessor can be used with extension methods that use a generic action delegate of up to 7 argument types. An generic argument type of `System.Object` is not supported.

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

The configuration below is equivalent to calling `services.AddMyService(options => { options.Title = "Mr" });`:
```json
{
   "MyService" : {
      "Title": "Mr"
   }
}
```

#### Action delegates with more than one argument
When the action delegates with more than one argument, all matching configuration methods will be called

Given the extension method below:
```csharp
public IServiceCollection AddMyService(this IServiceCollection services, Action<MyServiceOptions, MyOtherServiceOptions> configureOptions);

public class MyServiceOptions
{
   public string Title { get; set; }
   public string Name
}

public class MyOtherServiceOptions
{
   public string Title { get; set; }
}
```

The configuration below is equivalent to calling `services.AddMyService((options, other) => { options.Title = "Mr"; options.Name = "John"; other.Title = "Mr"; });`:
```json
{
   "MyService" : {
      "Title": "Mr",
      "Name": "John"
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
      "konnichiwa"
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
Some .NET types can be mapped from a string in configuration.


|.NET Type                   |Example Configuration|C# Equivalent                   |
|----------------------------|---------------------|--------------------------------|
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

### Execution Order
ConfigurationProcessor uses `IConfiguration.GetChildren()` to retrieve the methods to execute. The configuration key names are sorted alphabetically, and there is no supported mechanism to retrieve the original sort order. 

Given the configuration below:
```json
{
   "MyService" : {
      "ConfigureB": { "Value": 1 },
      "ConfigureZ": { "Value": 5 },
      "ConfigureA": { "Value": 4 }
   }
}
```

The order of the methods executed will be `ConfigureA`, `ConfigureB` and then `ConfigureZ`;

### ConnectionString handling
Parameters or properties that are named 'ConnectionString' are given special handling where the value will come from an element from `ConnectionStrings` section with the same key if it exists.

Given the configuration below:
```json
{
   "ConnectionStrings": {
      "Default": "abcd"
   },
   "Services": {
      "DbConnection": {
         "ConnectionString" : "Default"
      }
   }
}
```

```csharp
public static IServiceCollection AddDbConnection(this IServiceCollection services, string connectionString)
```

The configuration above is equivalent to calling `services.AddDbConnection("abcd");`

## Credits
[`Serilog.Settings.Configuration`](https://github.com/serilog/serilog-settings-configuration/) which is the basis for this project.