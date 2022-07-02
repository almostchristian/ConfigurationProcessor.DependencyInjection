// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using ConfigurationProcessor.Core.Assemblies;
using System;
using System.IO;
using Xunit;

namespace ConfigurationProcessor.DependencyInjection.UnitTests
{
    public class DllScanningAssemblyFinderTests : IDisposable
    {
        private const string BinDir1 = "bin1";
        private const string BinDir2 = "bin2";
        private const string BinDir3 = "bin3";

        private readonly string privateBinPath;

        public DllScanningAssemblyFinderTests()
        {
            var d1 = GetOrCreateDirectory(BinDir1);
            var d2 = GetOrCreateDirectory(BinDir2);
            var d3 = GetOrCreateDirectory(BinDir3);

            privateBinPath = $"{d1.Name};{d2.FullName};{d3.Name}";

            DirectoryInfo GetOrCreateDirectory(string name)
                => Directory.Exists(name) ? new DirectoryInfo(name) : Directory.CreateDirectory(name);
        }

        public void Dispose()
        {
            Directory.Delete(BinDir1, true);
            Directory.Delete(BinDir2, true);
            Directory.Delete(BinDir3, true);
        }

        [Fact]
        public void ShouldProbeCurrentDirectory()
        {
            var assemblyNames = new DllScanningAssemblyFinder().FindAssembliesReferencingAssembly(new[] { typeof(DllScanningAssemblyFinderTests).Assembly });
            Assert.NotEmpty(assemblyNames);
        }
    }
}
