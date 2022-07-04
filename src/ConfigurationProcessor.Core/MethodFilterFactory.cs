// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System.Collections.Generic;
using System.Reflection;

namespace ConfigurationProcessor.Core
{
   /// <summary>
   /// Delegate for filtering a candidate method.
   /// </summary>
   /// <param name="methodInfo">The method to evaluate.</param>
   /// <param name="name">The configuration name from the method filter factory.</param>
   /// <returns>True if the method is acceptable.</returns>
   public delegate bool MethodFilter(MethodInfo methodInfo, string name);

   /// <summary>
   /// Creates method filters. See <see cref="MethodFilterFactories"/> for generating factories.
   /// </summary>
   /// <param name="name">The configuration name.</param>
   /// <returns>Returns the method filter and the candidate names.</returns>
   public delegate (MethodFilter Filter, IEnumerable<string> CandidateNames) MethodFilterFactory(string name);
}
