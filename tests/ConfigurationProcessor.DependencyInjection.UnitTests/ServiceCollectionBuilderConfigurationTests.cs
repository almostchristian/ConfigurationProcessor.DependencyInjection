// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using ConfigurationProcessor.DependencyInjection.UnitTests.Support;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TestDummies;
using static ConfigurationProcessor.DependencyInjection.UnitTests.Support.Extensions;

namespace ConfigurationProcessor.DependencyInjection.UnitTests
{
   public class ServiceCollectionBuilderConfigurationTests : ConfigurationBuilderTestsBase
   {
      protected override IServiceCollection ProcessJson(string json)
      {
         var fullJson = "{ 'ConnectionStrings' : { 'Conn1': 'abcd', 'Conn2': 'efgh' }, 'TestApp': { 'Services': "+ json + " } }";
         var serviceCollection = new ServiceCollection();
         var configuration = new ConfigurationBuilder().AddJsonString(fullJson).Build();
         serviceCollection.AddFromConfiguration(
             configuration,
             "TestApp",
             servicePaths: new[] { "Services" },
             candidateMethodNameSuffixes: new[] { "Handler", "Handlers" });

         return serviceCollection;
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

         IServiceCollection serviceCollection = new ServiceCollection();
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
