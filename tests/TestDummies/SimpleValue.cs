// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

namespace TestDummies
{
   public class SimpleValue<T>
   {
      public SimpleValue(T value)
      {
         Value = value;
      }

      public T Value { get; }
   }
}
