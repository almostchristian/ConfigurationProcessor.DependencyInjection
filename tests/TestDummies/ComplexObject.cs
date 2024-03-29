﻿// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;

namespace TestDummies
{
   public class ComplexObject : IComplexObject
   {
      public string Name { get; set; }

      public ChildValue Value { get; set; }

      public void SetName(string name)
      {
         Name = name;
      }

      public void Reset()
      {
         if (Value != null)
         {
            Value.Child = null;
            Value.Time = null;
         }
      }

      public class ChildValue : IChildValue
      {
         public TimeSpan? Time { get; set; }

         public TimeSpan? Time2 { set => Time = value; }

         public Uri Location { get; set; }

         public Type ContextType { get; set; }

         public Delegate OnError { get; set; }

         public string Child { get; set; }

         public void Reset()
         {
            Child = null;
         }
      }
   }
}
