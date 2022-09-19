// -------------------------------------------------------------------------------------------------
// Copyright (c) almostchristian. All rights reserved.
// -------------------------------------------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using ConfigurationProcessor.Core.Implementation;

namespace ConfigurationProcessor.Core
{
   /// <summary>
   /// Configuration reader options.
   /// </summary>
   public class ConfigurationReaderOptions
   {
      /// <summary>
      /// Gets the config section.
      /// </summary>
      public string ConfigSection { get; set; } = "Services";

      /// <summary>
      /// Gets the context paths.
      /// </summary>
      public IEnumerable<string>? ContextPaths { get; set; }

      /// <summary>
      /// Gets ths method filter factory.
      /// </summary>
      public MethodFilterFactory? MethodFilterFactory { get; set; }

      /// <summary>
      /// Additional methods.
      /// </summary>
      public IEnumerable<MethodInfo> AdditionalMethods { get; set; } = Enumerable.Empty<MethodInfo>();

      /// <summary>
      /// Gets or sets the method to invoke when a method is not found. The default method throws a <see cref="MissingMethodException"/>.
      /// </summary>
      public Action<ExtensionMethodNotFoundEventArgs> OnExtensionMethodNotFound { get; set; } = x => { };
   }
}
