// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.Core.Implementation
{
   internal class ConfigurationHelperImplementation : IConfigurationProcessor
   {
      private readonly ResolutionContext resolutionContext;
      private readonly IConfigurationSection configurationSection;

      public ConfigurationHelperImplementation(ResolutionContext resolutionContext, IConfigurationSection configurationSection)
      {
         this.resolutionContext = resolutionContext;
         this.configurationSection = configurationSection;
      }

      public IConfiguration RootConfiguration => resolutionContext.RootConfiguration;

      public void Invoke<T>(T instance, string methodName, params object?[] arguments)
         where T : class
      {
         resolutionContext.CallConfigurationMethod(
            typeof(T),
            methodName,
            configurationSection,
            null,
            Array.Empty<TypeResolver>(),
            null!,
            () => arguments.ToList(),
            (arguments, methodInfo) => methodInfo.InvokeWithArguments(instance, arguments));
      }
   }
}
