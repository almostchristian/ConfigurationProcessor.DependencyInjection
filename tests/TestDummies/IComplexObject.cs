// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

namespace TestDummies
{
   public interface IComplexObject
   {
      string Name { get; set; }

      ComplexObject.ChildValue Value { get; set; }
   }
}
