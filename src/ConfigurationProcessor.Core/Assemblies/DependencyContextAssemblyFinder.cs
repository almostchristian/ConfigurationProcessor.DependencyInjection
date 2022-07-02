// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Microsoft.Extensions.DependencyModel;

namespace ConfigurationProcessor.Core.Assemblies
{
    internal sealed class DependencyContextAssemblyFinder : AssemblyFinder
    {
        private readonly DependencyContext dependencyContext;

        public DependencyContextAssemblyFinder(DependencyContext dependencyContext)
        {
            this.dependencyContext = dependencyContext ?? throw new ArgumentNullException(nameof(dependencyContext));
        }

        public override IReadOnlyList<AssemblyName> FindAssembliesReferencingAssembly(Assembly[] markerAssemblies)
        {
            var query = from assemblyName in this.dependencyContext.RuntimeLibraries
                            .SelectMany(l => l.GetDefaultAssemblyNames(this.dependencyContext)).Distinct()
                        select assemblyName;

            return query.ToList().AsReadOnly();
        }
    }
}
