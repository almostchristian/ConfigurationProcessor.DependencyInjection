// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;

namespace TestDummies
{
   public interface IChildValue
   {
      string Child { get; set; }
      Type ContextType { get; set; }
      Uri Location { get; set; }
      Delegate OnError { get; set; }
      TimeSpan? Time { get; set; }

      void Reset();
   }
}