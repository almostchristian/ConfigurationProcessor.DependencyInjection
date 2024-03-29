﻿// -------------------------------------------------------------------------------------------------
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
          : base(new ResolutionContext(assemblyFinder, configuration, configSection, options.AdditionalMethods, options.OnExtensionMethodNotFound, null, typeof(TConfig)), configuration, configSection)
      {
      }

      public void AddServices(TConfig builder, string? sectionName, bool getChildren, ConfigurationReaderOptions options)
      {
         var builderDirective = string.IsNullOrEmpty(sectionName) ? ConfigurationSection : ConfigurationSection.GetSection(sectionName);
         if (!getChildren || builderDirective.GetChildren().Any())
         {
            ResolutionContext.CallConfigurationMethods(
               typeof(TConfig),
               builderDirective,
               getChildren,
               null,
               options.MethodFilterFactory,
               (arguments, methodInfo) => methodInfo.InvokeWithArguments(builder, arguments));
         }
      }
   }
}
