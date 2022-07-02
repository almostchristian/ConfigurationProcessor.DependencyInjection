// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System.Linq;
using System.Reflection;
using ConfigurationProcessor.Core.Assemblies;
using ConfigurationProcessor.Core.Implementation;
using ConfigurationProcessor.DependencyInjection.UnitTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestDummies;
using Xunit;
using static ConfigurationProcessor.DependencyInjection.UnitTests.Support.Extensions;

namespace ConfigurationProcessor.DependencyInjection.UnitTests
{
    public class ConfigurationReaderTests
    {
        private readonly ConfigurationReader<IServiceCollection> configurationReader;

        public ConfigurationReaderTests()
        {
            configurationReader = new ConfigurationReader<IServiceCollection>(
                JsonStringConfigSource.LoadSection(@"{ 'FhirEngine': {  } }", "FhirEngine"),
                AssemblyFinder.ForSource(ConfigurationAssemblySource.UseLoadedAssemblies),
                Array.Empty<MethodInfo>());
        }

        [Fact]
        public void AddServicesSupportExpandedSyntaxWithoutArgs()
        {
            var json = @"
{
    'Services': [{
        'Name': 'HandlerMapping'
    }]
}";

            var result = configurationReader.GetMethodCalls(JsonStringConfigSource.LoadSection(json, "Services"));

            Assert.Collection(
                result,
                r => Assert.Equal("HandlerMapping", r.Key));
        }

        [Fact]
        public void AddServicesSupportAlternateSyntaxWithoutArgs()
        {
            var json = @"
{
    'Services': {
        'HandlerMapping': null
    }
}";

            var result = configurationReader.GetMethodCalls(JsonStringConfigSource.LoadSection(json, "Services"));

            Assert.Collection(
                result,
                r => Assert.Equal("HandlerMapping", r.Key));
        }

        [Fact]
        public void AddServicesSupportExpandedSyntaxWithArgs()
        {
            var json = @"
{
    'Services': [ {
        'Name': 'HandlerMapping',
        'Args': {
            'mappings': '{Message}'
        },
    }]
}";

            var result = configurationReader.GetMethodCalls(JsonStringConfigSource.LoadSection(json, "Services"));

            Assert.Collection(
                result,
                r => Assert.Equal("HandlerMapping", r.Key));

            Assert.Collection(
                result["HandlerMapping"],
                kvp => Assert.Empty(kvp.Item1));

            var args = result["HandlerMapping"].Single().Item3.ToArray();

            Assert.Collection(
                args,
                kvp =>
                {
                    Assert.Equal("mappings", kvp.Key);
                    Assert.Equal("{Message}", kvp.Value.ArgName.ConvertTo(default, typeof(string), new ResolutionContext(null, (IConfiguration)null, null, null)));
                });
        }

        [Fact]
        public void AddServicesSupportAlternateSyntaxWithArgs()
        {
            var json = @"
{
    'Services': {
        'HandlerMapping': {
            'mappings': '{Message}'
        },
    }
}";

            var result = configurationReader.GetMethodCalls(JsonStringConfigSource.LoadSection(json, "Services"));

            Assert.Collection(
                result,
                r => Assert.Equal("HandlerMapping", r.Key));

            Assert.Collection(
                result["HandlerMapping"],
                kvp => Assert.Empty(kvp.Item1));

            var args = result["HandlerMapping"].Single().Item3.ToArray();

            Assert.Collection(
                args,
                kvp =>
                {
                    Assert.Equal("mappings", kvp.Key);
                    Assert.Equal("{Message}", kvp.Value.ArgName.ConvertTo(default, typeof(string), new ResolutionContext(null, (IConfiguration)null, null, null)));
                });
        }
    }
}
