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
      private readonly ConfigurationReaderOptions? options;

      public ConfigurationHelperImplementation(ResolutionContext resolutionContext, IConfigurationSection configurationSection, ConfigurationReaderOptions? options)
      {
         this.resolutionContext = resolutionContext;
         this.configurationSection = configurationSection;
         this.options = options;
      }

      public void Invoke<T>(T instance, string methodName, params object[] arguments)
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
            (arguments, methodInfo) =>
            {
               if (methodInfo.IsStatic)
               {
                  arguments.Insert(0, instance);
                  methodInfo.Invoke(null, arguments.ToArray());
               }
               else
               {
                  methodInfo.Invoke(instance, arguments.ToArray());
               }
            });
      }
   }
}
