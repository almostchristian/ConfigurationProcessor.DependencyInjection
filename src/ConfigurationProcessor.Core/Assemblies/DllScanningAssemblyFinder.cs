// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace ConfigurationProcessor.Core.Assemblies
{
    internal sealed class DllScanningAssemblyFinder : AssemblyFinder
    {
        public override IReadOnlyList<AssemblyName> FindAssembliesReferencingAssembly(Assembly[] markerAssemblies)
        {
            var probeDirs = new List<string>();

            if (!string.IsNullOrEmpty(AppDomain.CurrentDomain.BaseDirectory))
            {
                probeDirs.Add(AppDomain.CurrentDomain.BaseDirectory);
            }
            else
            {
                probeDirs.Add(Path.GetDirectoryName(typeof(AssemblyFinder).Assembly.Location)!);
            }

            try
            {
                var query = from probeDir in probeDirs
                            where Directory.Exists(probeDir)
                            from outputAssemblyPath in Directory.GetFiles(probeDir, "*.dll").Union(Directory.GetFiles(probeDir, "*.exe"))
                            let assemblyFileName = Path.GetFileNameWithoutExtension(outputAssemblyPath)
                            where assemblyFileName.IndexOf("NUnit", StringComparison.OrdinalIgnoreCase) < 0
                            let assemblyName = TryGetAssemblyNameFrom(outputAssemblyPath)
                            where assemblyName != null
                            select assemblyName;

                return query.ToList().AsReadOnly();
            }
            catch (IOException ioex)
            {
                Console.WriteLine(ioex.Message);
                throw;
            }
        }

        private static AssemblyName? TryGetAssemblyNameFrom(string path)
        {
            try
            {
                return AssemblyName.GetAssemblyName(path);
            }
            catch (BadImageFormatException)
            {
                return null;
            }
        }
    }
}
