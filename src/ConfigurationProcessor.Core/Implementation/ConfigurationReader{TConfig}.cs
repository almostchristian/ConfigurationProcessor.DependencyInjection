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
    internal class ConfigurationReader<TConfig> : ConfigurationReader, IConfigurationReader<TConfig>
        where TConfig : class
    {
        public ConfigurationReader(IConfigurationSection configSection, AssemblyFinder assemblyFinder, MethodInfo[] surrogateMethods, IConfiguration configuration = null!)
            : base(new ResolutionContext(assemblyFinder, configuration!, configSection, typeof(TConfig)), configuration, assemblyFinder, configSection, surrogateMethods)
        {
        }

        public void AddServices(TConfig builder, string? sectionName, bool getChildren, params string[] candidateMethodNameSuffixes)
        {
            var builderDirective = string.IsNullOrEmpty(sectionName) ? ConfigurationSection : ConfigurationSection.GetSection(sectionName);
            if (!getChildren || builderDirective.GetChildren().Any())
            {
                var methodCalls = GetMethodCalls(builderDirective, getChildren);
                CallConfigurationMethods(ResolutionContext, typeof(TConfig), methodCalls, candidateMethodNameSuffixes, (arguments, methodInfo) =>
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
