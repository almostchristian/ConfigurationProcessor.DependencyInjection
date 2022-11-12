// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ConfigurationProcessor.Core.Implementation
{
   internal sealed class ChildContextMissingMethodTracker
   {
      private readonly Dictionary<string, ExtensionMethodNotFoundEventArgs> missingMethods = new(StringComparer.OrdinalIgnoreCase);
      private readonly HashSet<string> foundMethods = new(StringComparer.OrdinalIgnoreCase);
      private readonly ResolutionContext originalContext;

      public ChildContextMissingMethodTracker(ResolutionContext originalContext)
      {
         this.originalContext = originalContext;
      }

      public void OnExtensionMethodNotFound(ExtensionMethodNotFoundEventArgs args)
      {
         args.Handled = true;
         missingMethods[args.OriginalMethodName] = args;
      }

      public void OnExtensionMethodFound(string methodName)
      {
         foundMethods.Add(methodName);
      }

      public void Validate()
      {
         foreach (var item in foundMethods)
         {
            missingMethods.Remove(item);
         }

         foreach (var errorEventArgs in missingMethods.Values)
         {
            errorEventArgs.Handled = false;
            originalContext.OnExtensionMethodNotFound(errorEventArgs);
            if (!errorEventArgs.Handled)
            {
               Extensions.ThrowMissingMethodException(errorEventArgs);
            }
         }
      }
   }
}
