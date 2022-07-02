// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;

namespace ConfigurationProcessor.DependencyInjection.UnitTests.Support
{
    public class TestServiceCollection : List<ServiceDescriptor>, IServiceCollection
    {
    }
}
