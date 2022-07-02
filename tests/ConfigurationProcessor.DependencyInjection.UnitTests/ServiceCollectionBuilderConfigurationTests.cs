// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using ConfigurationProcessor.DependencyInjection.UnitTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestDummies;
using Xunit;
using static ConfigurationProcessor.DependencyInjection.UnitTests.Support.Extensions;

namespace ConfigurationProcessor.DependencyInjection.UnitTests
{
    public class ServiceCollectionBuilderConfigurationTests : ConfigurationBuilderTestsBase
    {
        protected override void TestBuilder(string json, TestServiceCollection serviceCollection, string? fullJsonFormat = null)
        {
            var fullJson = string.Format(fullJsonFormat ?? "{{ 'TestApp': {0}}}", json);
            serviceCollection.AddFromConfiguration(
                new ConfigurationBuilder().AddJsonString(fullJson).Build(),
                "TestApp",
                servicePaths: new[] { "Services" },
                candidateMethodNameSuffixes: new[] { "Handler", "Handlers" } );
        }

        [Fact]
        public void FullJsonAddingGenericWithExpandedSyntaxServices()
        {
            var json = @$"
{{
    'TestApp': {{
        'DummyDelegate': '{NameOf<DelegateMembers>()}::{nameof(DelegateMembers.TestDelegate)}'
    }}
}}";

            IServiceCollection serviceCollection = new TestServiceCollection();
            var configuration = new ConfigurationBuilder().AddJsonString(json).Build();
            serviceCollection.AddFromConfiguration(
                configuration,
                "TestApp");

            Assert.Collection(
                serviceCollection,
                sd => Assert.Equal(typeof(DummyDelegate), sd.ServiceType));
        }
    }
}
