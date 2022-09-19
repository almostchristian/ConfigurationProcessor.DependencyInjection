// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Linq;
using System.Reflection;
using ConfigurationProcessor.Core.Assemblies;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.Core.Implementation
{
   internal class ConfigurationReader<TConfig> : ConfigurationReader, IConfigurationReader<TConfig>
        where TConfig : class
   {
      public ConfigurationReader(IConfiguration configuration, IConfigurationSection configSection, AssemblyFinder assemblyFinder, ConfigurationReaderOptions options)
          : base(new ResolutionContext(assemblyFinder, configuration, configSection, options.AdditionalMethods, options.OnExtensionMethodNotFound, typeof(TConfig)), configuration, assemblyFinder, configSection)
      {
      }

      public void AddServices(TConfig builder, string? sectionName, bool getChildren, ConfigurationReaderOptions options)
      {
         var builderDirective = string.IsNullOrEmpty(sectionName) ? ConfigurationSection : ConfigurationSection.GetSection(sectionName);
         if (!getChildren || builderDirective.GetChildren().Any())
         {
            var methodCalls = ResolutionContext.GetMethodCalls(builderDirective, getChildren);
            ResolutionContext.CallConfigurationMethods(
               typeof(TConfig),
               methodCalls,
               options.MethodFilterFactory,
               (arguments, methodInfo) =>
               {
                  if (methodInfo.IsStatic)
                  {
                     arguments.Insert(0, builder);
                     methodInfo.Invoke(null, arguments.ToArray());
                  }
                  else
                  {
                     methodInfo.Invoke(builder, arguments.ToArray());
                  }
               });
         }
      }
   }
}
