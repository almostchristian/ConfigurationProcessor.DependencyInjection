// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;

namespace ConfigurationProcessor.DependencyInjection.UnitTests.Support
{
   internal class JsonStringConfigSource : IConfigurationSource
   {
      private readonly string json;

      public JsonStringConfigSource(string json)
      {
         this.json = json;
      }

      public IConfigurationProvider Build(IConfigurationBuilder builder)
      {
         return new JsonStringConfigProvider(json);
      }

      public static IConfigurationSection LoadSection(string json, string section)
      {
         return new ConfigurationBuilder().Add(new JsonStringConfigSource(json)).Build().GetSection(section);
      }

      public static IDictionary<string, string> LoadData(string json)
      {
         var provider = new JsonStringConfigProvider(json);
         provider.Load();
         return provider.Data;
      }

      private class JsonStringConfigProvider : JsonConfigurationProvider
      {
         private readonly string json;

         public JsonStringConfigProvider(string json)
             : base(new JsonConfigurationSource { Optional = true })
         {
            this.json = json;
         }

         public new IDictionary<string, string> Data => base.Data;

         public override void Load()
         {
            Load(StringToStream(json.ToValidJson()));
         }

         private static Stream StringToStream(string str)
         {
            var memStream = new MemoryStream();
            var textWriter = new StreamWriter(memStream);
            textWriter.Write(str);
            textWriter.Flush();
            memStream.Seek(0, SeekOrigin.Begin);

            return memStream;
         }
      }
   }
}
