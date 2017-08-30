using Microsoft.Extensions.FileProviders;
using System;
using System.Collections.Generic;
using System.IO;

namespace LoadEngine
{
    internal class LoadEngineContext
    {
        internal bool EnableHotPlug { get; set; } = false;

        internal List<string> ExcludeCaseInsensitiveNamedTypes { get; set; } = null;

        internal List<string> ExcludeNamedTypes { get; set; } = null;

        internal List<string> ExcludeRegexNamedTypes { get; set; } = null;

        internal List<Type> ExcludeTypes { get; set; } = null;

        internal IFileProvider FileProvider { get; set; }

        internal string Filter { get; set; } = "*.*";

        internal SearchOption SearchOption { get; set; } = SearchOption.TopDirectoryOnly;

        public LoadEngineContext()
        {
            ExcludeNamedTypes = new List<string>();
            ExcludeCaseInsensitiveNamedTypes = new List<string>();
            ExcludeRegexNamedTypes = new List<string>();
            ExcludeTypes = new List<Type>();
        }
    }
}
