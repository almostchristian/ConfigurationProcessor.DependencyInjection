// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.DependencyModel;

namespace ConfigurationProcessor.Core.Assemblies
{
   internal abstract class AssemblyFinder
   {
      public abstract IReadOnlyList<AssemblyName> FindAssembliesReferencingAssembly(Assembly[] markerAssemblies);

      protected static bool IsCaseInsensitiveMatch(string? text, string textToFind)
      {
         return text != null &&
#if NETSTANDARD2_0
            text.IndexOf(textToFind, StringComparison.OrdinalIgnoreCase) >= 0;
#else
            text.Contains(textToFind, StringComparison.OrdinalIgnoreCase);
#endif
      }

      public static AssemblyFinder Auto()
      {
         try
         {
            // Need to check `Assembly.GetEntryAssembly()` first because
            // `DependencyContext.Default` throws an exception when `Assembly.GetEntryAssembly()` returns null
            if (Assembly.GetEntryAssembly() != null && DependencyContext.Default != null)
            {
               return new DependencyContextAssemblyFinder(DependencyContext.Default);
            }
         }
         catch (NotSupportedException) when (string.IsNullOrEmpty(typeof(object).Assembly.Location))
         {
            // bundled mode detection
         }

         return new DllScanningAssemblyFinder();
      }

      public static AssemblyFinder ForSource(ConfigurationAssemblySource configurationAssemblySource)
      {
         return configurationAssemblySource switch
         {
            ConfigurationAssemblySource.UseLoadedAssemblies => Auto(),
            ConfigurationAssemblySource.AlwaysScanDllFiles => new DllScanningAssemblyFinder(),
            _ => throw new ArgumentOutOfRangeException(nameof(configurationAssemblySource), configurationAssemblySource, null),
         };
      }

      public static AssemblyFinder ForDependencyContext(DependencyContext dependencyContext)
      {
         return new DependencyContextAssemblyFinder(dependencyContext);
      }
   }
}
