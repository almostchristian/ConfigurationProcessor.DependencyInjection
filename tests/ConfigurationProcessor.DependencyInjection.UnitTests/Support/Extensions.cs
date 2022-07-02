// -------------------------------------------------------------------------------------------------
// Copyright (c) Integrated Health Information Systems Pte Ltd. All rights reserved.
// -------------------------------------------------------------------------------------------------

namespace ConfigurationProcessor.DependencyInjection.UnitTests.Support
{
    public static class Extensions
    {
        public static string ToValidJson(this string str)
        {
            str = str.Replace('\'', '"');
            return str;
        }

        internal static string NameOf<T>() => typeof(T).FullName;
    }
}
