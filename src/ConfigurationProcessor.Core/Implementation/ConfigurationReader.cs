// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using ConfigurationProcessor.Core.Assemblies;
using Microsoft.Extensions.Configuration;
using ConfigLookup = System.ValueTuple<ConfigurationProcessor.Core.Implementation.TypeResolver[], Microsoft.Extensions.Configuration.IConfigurationSection, System.Collections.Generic.Dictionary<string, (ConfigurationProcessor.Core.Implementation.IConfigurationArgumentValue ArgName, Microsoft.Extensions.Configuration.IConfigurationSection ConfigSection)>>;

namespace ConfigurationProcessor.Core.Implementation
{
   internal abstract class ConfigurationReader
   {
      private readonly IConfigurationSection section;
      private readonly AssemblyFinder assemblyFinder;
      private readonly ResolutionContext resolutionContext;

      protected ConfigurationReader(
         ResolutionContext resolutionContext,
         IConfiguration rootConfiguration,
         AssemblyFinder assemblyFinder,
         IConfigurationSection configSection)
      {
         this.resolutionContext = resolutionContext;
         this.assemblyFinder = assemblyFinder;
         this.section = configSection;
         RootConfiguration = rootConfiguration;
      }

      public IConfiguration RootConfiguration { get; }

      public ResolutionContext ResolutionContext => this.resolutionContext;

      public IConfigurationSection ConfigurationSection => this.section;
   }
}
