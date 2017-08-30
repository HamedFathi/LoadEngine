using CoreExtensions;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoadEngine
{
    internal static class Extensions
    {
        internal static bool Contains(this string source, string target, bool ignoreCase)
        {
            return ignoreCase ? source.ContainsIgnoreCase(target) : source.Contains(target);
        }
    }
}
