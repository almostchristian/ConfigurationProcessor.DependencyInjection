// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;

namespace TestDummies
{
   public interface IChildValue : IRootChildValue
   {
      Type ContextType { get; set; }
      Uri Location { get; set; }
      Delegate OnError { get; set; }
      TimeSpan? Time { get; set; }
      TimeSpan? Time2 { set; }
   }
}