// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

namespace ConfigurationProcessor.Core.Implementation
{
   internal interface IConfigurationReader<in TConfig>
        where TConfig : class
   {
      void AddServices(TConfig builder, string? sectionName, bool getChildren, ConfigurationReaderOptions options);
   }
}
