// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

namespace TestDummies
{
    public class DummyParameter<T>
    {
        public T DummyParam { get; set; }
    }

    public class DummyParameter : DummyParameter<string>
    {
    }
}
