// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System.Linq;
using System.Reflection;
using ConfigurationProcessor.Core.Assemblies;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.Core.Implementation
{
   internal class ConfigurationReader<TConfig> : ConfigurationReader, IConfigurationReader<TConfig>
        where TConfig : class
   {
      public ConfigurationReader(IConfigurationSection configSection, AssemblyFinder assemblyFinder, MethodInfo[] additionalMethods, IConfiguration configuration = null!)
          : base(new ResolutionContext(assemblyFinder, configuration!, configSection, typeof(TConfig)), configuration, assemblyFinder, configSection, additionalMethods)
      {
      }

      public void AddServices(TConfig builder, string? sectionName, bool getChildren, MethodFilterFactory? methodFilterFactory)
      {
         var builderDirective = string.IsNullOrEmpty(sectionName) ? ConfigurationSection : ConfigurationSection.GetSection(sectionName);
         if (!getChildren || builderDirective.GetChildren().Any())
         {
            var methodCalls = GetMethodCalls(builderDirective, getChildren);
            CallConfigurationMethods(ResolutionContext, typeof(TConfig), methodCalls, methodFilterFactory, (arguments, methodInfo) =>
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
