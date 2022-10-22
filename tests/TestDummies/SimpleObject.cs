// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

namespace TestDummies
{
   public class SimpleObject
   {
      public int PropertyA { get; set; }

      public string PropertyB { get; set; }

      public string PropertyB2 { set => PropertyB = value; }

      public bool PropertyC { get; set; } = true;
   }
}
