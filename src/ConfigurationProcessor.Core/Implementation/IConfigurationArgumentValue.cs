// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Reflection;

namespace ConfigurationProcessor.Core.Implementation
{
    internal interface IConfigurationArgumentValue
    {
        object? ConvertTo(MethodInfo method, Type toType, ResolutionContext resolutionContext);
    }
}
