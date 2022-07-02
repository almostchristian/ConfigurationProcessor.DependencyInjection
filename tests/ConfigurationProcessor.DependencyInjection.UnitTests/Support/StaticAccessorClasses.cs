// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

namespace ConfigurationProcessor.DependencyInjection.UnitTests.Support
{
#pragma warning disable SA1649 // File name should match first type name
    public interface IAmAnInterface
    {
    }

    public abstract class AnAbstractClass
    {
    }

    internal class ConcreteImpl : AnAbstractClass, IAmAnInterface
    {
        private ConcreteImpl()
        {
        }

        public static ConcreteImpl Instance { get; } = new ConcreteImpl();
    }

    public class ClassWithStaticAccessors
    {
#pragma warning disable SA1401 // Fields should be private
        public static IAmAnInterface InterfaceField = ConcreteImpl.Instance;

        public static AnAbstractClass AbstractField = ConcreteImpl.Instance;

        public IAmAnInterface InstanceInterfaceField = ConcreteImpl.Instance;

#pragma warning disable 169
#pragma warning disable SA1306 // Field names should begin with lower-case letter
        private static IAmAnInterface PrivateInterfaceField = ConcreteImpl.Instance;
#pragma warning restore 169

        public static IAmAnInterface InterfaceProperty => ConcreteImpl.Instance;

        public static AnAbstractClass AbstractProperty => ConcreteImpl.Instance;

        // ReSharper disable once UnusedMember.Local
        private static IAmAnInterface PrivateInterfaceProperty => ConcreteImpl.Instance;

        public IAmAnInterface InstanceInterfaceProperty => ConcreteImpl.Instance;
    }
}
