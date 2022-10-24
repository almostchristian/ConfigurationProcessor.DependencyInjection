using ConfigurationProcessor;
using MassTransit;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace SampleOutboxApi;

public static class Extensions
{
   public static TracerProviderBuilder SetDefaultResourceBuilder(this TracerProviderBuilder builder, Action<ResourceBuilder> configure)
   {
      var resourceBuilder = ResourceBuilder.CreateDefault();
      configure?.Invoke(resourceBuilder);
      return builder.SetResourceBuilder(resourceBuilder);
   }

   public static IBusRegistrationConfigurator UsingActiveMq(this IBusRegistrationConfigurator configurator, Action<IActiveMqBusFactoryConfigurator> configure)
   {
      configurator.UsingActiveMq((ctx, cfg) =>
      {
         configure?.Invoke(cfg);
      });
      return configurator;
   }

   public static IActiveMqBusFactoryConfigurator ConnectionString(this IActiveMqBusFactoryConfigurator configurator, string value, IConfigurationProcessor configurationProcessor)
   {
      var uri = new Uri(configurationProcessor.RootConfiguration.GetConnectionString(value) ?? value);
      var userPair = uri.UserInfo?.Split(':');
      return configurator.HostSettings(uri.Host, uri.Port, userPair?.ElementAtOrDefault(0), userPair?.ElementAtOrDefault(1));
   }

   public static IActiveMqBusFactoryConfigurator HostSettings(this IActiveMqBusFactoryConfigurator configurator, string host, int port = default, string username = null, string password = null)
   {
      configurator.Host(host, port, c =>
      {
         if (username != null)
         {
            c.Username(username);
         }

         if (password != null)
         {
            c.Password(password);
         }

         c.UseSsl();
      });

      return configurator;
   }
}
