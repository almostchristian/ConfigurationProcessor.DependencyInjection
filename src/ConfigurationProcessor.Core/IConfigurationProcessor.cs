// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text;

namespace ConfigurationProcessor
{
   /// <summary>
   /// Represents a configuration helper for dynamically executing methods.
   /// </summary>
   public interface IConfigurationProcessor
   {
      /// <summary>
      /// Invokes a specificed configuration method with specificed arguments. Method is chosen based on the argument types.
      /// </summary>
      /// <typeparam name="T"></typeparam>
      /// <param name="instance"></param>
      /// <param name="methodName"></param>
      /// <param name="arguments"></param>
      void Invoke<T>(T instance, string methodName, params object[] arguments)
         where T : class;
   }
}
