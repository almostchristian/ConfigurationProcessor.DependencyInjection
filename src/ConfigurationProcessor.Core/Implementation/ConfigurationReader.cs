// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using ConfigurationProcessor.Core.Assemblies;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.Core.Implementation
{
   internal abstract class ConfigurationReader
   {
      private readonly IConfigurationSection section;
      private readonly ResolutionContext resolutionContext;

      protected ConfigurationReader(
         ResolutionContext resolutionContext,
         IConfiguration rootConfiguration,
         IConfigurationSection configSection)
      {
         this.resolutionContext = resolutionContext;
         this.section = configSection;
         RootConfiguration = rootConfiguration;
      }

      public IConfiguration RootConfiguration { get; }

      public ResolutionContext ResolutionContext => this.resolutionContext;

      public IConfigurationSection ConfigurationSection => this.section;
   }
}
