// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace ConfigurationProcessor.Core
{
   /// <summary>
   /// Contains method filter factories.
   /// </summary>
   public static class MethodFilterFactories
   {
      private static bool DefaultMethodFilter(MethodInfo method, string name)
         => true;

      /// <summary>
      /// Method filter factory that accepts methods with names like '<paramref name="name"/>' or 'Add<paramref name="name"/>'.
      /// </summary>
      /// <param name="name">The configuration name to search for.</param>
      /// <returns>The method filter and candidate names.</returns>
      public static (MethodFilter Filter, IEnumerable<string> CandidateNames) DefaultMethodFilterFactory(string name)
      {
         var candidates = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { name, $"Add{name}", $"set_{name}" };
         return (DefaultMethodFilter, candidates);
      }

      /// <summary>
      /// Creates a method filter factory with suffixes.
      /// </summary>
      /// <param name="methodNameSuffixes">The method name suffixes to search for.</param>
      /// <returns>The method filter factory.</returns>
      public static MethodFilterFactory WithSuffixes(params string[] methodNameSuffixes)
         => WithSuffixes(DefaultMethodFilter, methodNameSuffixes);

      /// <summary>
      /// Creates a method filter factory with suffixes.
      /// </summary>
      /// <param name="methodFilter">The default method filter factory.</param>
      /// <param name="methodNameSuffixes">The method name suffixes to search for.</param>
      /// <returns>The method filter factory.</returns>
      public static MethodFilterFactory WithSuffixes(MethodFilter methodFilter, params string[] methodNameSuffixes)
         => WithPrefixAndSuffixes(methodFilter, new[] { "Add", "set_" }, methodNameSuffixes);

      /// <summary>
      /// Creates a method filter factory with prefixes and suffixes.
      /// </summary>
      /// <param name="methodFilter">The default method filter factory.</param>
      /// <param name="methodNamePrefixes">The method name suffixes to search for.</param>
      /// <param name="methodNameSuffixes">The method name suffixes to search for.</param>
      /// <returns>The method filter factory.</returns>
      public static MethodFilterFactory WithPrefixAndSuffixes(MethodFilter methodFilter, string[] methodNamePrefixes, string[] methodNameSuffixes)
      {
         return name =>
         {
            var candidates = GetCandidateNames(name, methodNamePrefixes, methodNameSuffixes);
            return (methodFilter, candidates);
         };

         static List<string> GetCandidateNames(string name, string[] methodNamePrefixes, string[] candidateSuffixes)
         {
            const char GenericTypeMarker = '`';
            var namesplit = name.Split(GenericTypeMarker);

            var result = new List<string> { name };

            if (candidateSuffixes.Length > 0)
            {
               if (namesplit.Length > 1)
               {
                  result.AddRange(candidateSuffixes.Select(x => $"{namesplit[0] + x}{GenericTypeMarker}{namesplit[1]}"));
               }
               else
               {
                  result.AddRange(candidateSuffixes.Select(x => name + x));
               }
            }

            var withPrefix = result.SelectMany(y => methodNamePrefixes.Select(x => x + y)).ToList();
            result.AddRange(withPrefix);

            return result;
         }
      }
   }
}
