// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

namespace ConfigurationProcessor.DependencyInjection.UnitTests.Support
{
   internal static class Some
   {
      private static int counter;

      public static int Int()
      {
         return Interlocked.Increment(ref counter);
      }

      public static decimal Decimal()
      {
         return Int() + 0.123m;
      }

      public static string String(string tag = null)
      {
         return (tag ?? string.Empty) + "__" + Int();
      }

      public static TimeSpan TimeSpan()
      {
         return System.TimeSpan.FromMinutes(Int());
      }

      public static DateTime Instant()
      {
         return new DateTime(2012, 10, 28) + TimeSpan();
      }

      public static DateTimeOffset OffsetInstant()
      {
         return new DateTimeOffset(Instant());
      }

      public static string NonexistentTempFilePath()
      {
         return Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".txt");
      }

      public static string TempFilePath()
      {
         return Path.GetTempFileName();
      }

      public static string TempFolderPath()
      {
         var dir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
         Directory.CreateDirectory(dir);
         return dir;
      }
   }
}
