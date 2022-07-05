using Microsoft.AspNetCore.Mvc.Testing;

namespace ConfigurationProcessor.AspNetCore.IntegrationTests
{
   public partial class HttpIntegrationTestFixture<TStartup> : WebApplicationFactory<TStartup>
        where TStartup : class
   {
      private readonly string environmentName;

      public HttpIntegrationTestFixture(
          string environmentName,
          IConfigurationBuilder configurationBuilder)
      {
         this.environmentName = environmentName ?? Guid.NewGuid().ToString("n");
         this.WithWebHostBuilder(c => c.UseEnvironment(this.environmentName).UseConfiguration(configurationBuilder.Build()));
      }
   }
}
