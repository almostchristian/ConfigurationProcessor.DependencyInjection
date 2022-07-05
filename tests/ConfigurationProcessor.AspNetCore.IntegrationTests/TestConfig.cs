namespace ConfigurationProcessor.AspNetCore.IntegrationTests
{
   public class TestConfig
   {
      [Fact]
      public async Task Otel()
      {
         var currentDir = Directory.GetCurrentDirectory();

         var fixture = new HttpIntegrationTestFixture<Program>("testing", new ConfigurationBuilder().AddJsonFile(Path.Combine(currentDir, "appsettings.otel.json")));
         var client = fixture.CreateClient();
         var response = await client.GetAsync("/");
         Assert.Equal("hello", await response.Content.ReadAsStringAsync());
      }
   }
}