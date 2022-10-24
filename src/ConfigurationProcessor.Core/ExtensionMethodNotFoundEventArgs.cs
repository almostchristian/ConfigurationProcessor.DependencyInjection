// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.Extensions.Configuration;

namespace ConfigurationProcessor.Core
{
   /// <summary>
   /// Represents event data when an extension method was not found.
   /// </summary>
   public class ExtensionMethodNotFoundEventArgs : EventArgs
   {
      internal ExtensionMethodNotFoundEventArgs(
         IEnumerable<MethodInfo> candidateMethods,
         IEnumerable<string> candidateNames,
         Type extensionMethodType,
         IReadOnlyDictionary<string, IConfigurationSection>? suppliedArguments)
      {
         CandidateMethods = candidateMethods;
         CandidateNames = candidateNames;
         ExtensionMethodType = extensionMethodType;
         SuppliedArguments = suppliedArguments;
      }

      /// <summary>
      /// Gets the candidate methods.
      /// </summary>
      public IEnumerable<MethodInfo> CandidateMethods { get; }

      /// <summary>
      /// Gets the candidate method names.
      /// </summary>
      public IEnumerable<string> CandidateNames { get; }

      /// <summary>
      /// Gets the extension method type that was searched.
      /// </summary>
      public Type ExtensionMethodType { get; }

      /// <summary>
      /// Gets the supplied argument names.
      /// </summary>
      public IReadOnlyDictionary<string, IConfigurationSection>? SuppliedArguments { get; }

      /// <summary>
      /// Gets or sets if the event is handled. If true, an exception will not be thrown.
      /// </summary>
      public bool Handled { get; set; }
   }
}
