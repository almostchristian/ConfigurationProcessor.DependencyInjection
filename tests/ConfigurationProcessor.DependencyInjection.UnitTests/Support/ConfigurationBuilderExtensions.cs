// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.DependencyInjection.UnitTests.Support
{
   public static class ConfigurationBuilderExtensions
   {
      public static IConfigurationBuilder AddJsonString(this IConfigurationBuilder builder, string json)
      {
         return builder.Add(new JsonStringConfigSource(json));
      }
   }
}
