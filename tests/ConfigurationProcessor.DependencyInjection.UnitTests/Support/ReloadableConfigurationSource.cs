// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.DependencyInjection.UnitTests.Support
{
   internal class ReloadableConfigurationSource : IConfigurationSource
   {
      private readonly ReloadableConfigurationProvider configProvider;
      private readonly IDictionary<string, string> source;

      public ReloadableConfigurationSource(IDictionary<string, string> source)
      {
         this.source = source;
         configProvider = new ReloadableConfigurationProvider(source);
      }

      public IConfigurationProvider Build(IConfigurationBuilder builder) => configProvider;

      public void Reload() => configProvider.Reload();

      public void Set(string key, string value) => source[key] = value;

      private class ReloadableConfigurationProvider : ConfigurationProvider
      {
         private readonly IDictionary<string, string> source;

         public ReloadableConfigurationProvider(IDictionary<string, string> source)
         {
            this.source = source;
         }

         public override void Load() => Data = source;

         public void Reload() => OnReload();
      }
   }
}
