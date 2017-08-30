using CoreExtensions;
using CoreUtilities;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace LoadEngine
{
    public class LoadEngine<T> where T : class
    {
        public event ChangedDelegate OnChange;
        public event ChangedDelegate OnLoad;

        public delegate void ChangedDelegate();

        private readonly LoadEngineContext _ctx = new LoadEngineContext();
        private readonly string _dir;
        private readonly ConcurrentDictionary<string, Assembly> _assemblies =
                    new ConcurrentDictionary<string, Assembly>();
        private IFileProvider _fileProvider;
        private ILogger _logger;
        private bool _forceGc;
        private bool isFirstLoad = true;

        public string UniqueId { get; } = "_" + Guid.NewGuid().ToString("N") + DateTime.Now.GetUtcTimeStamp();

        public LoadEngine(string dir)
        {
            _dir = dir;
        }

        public LoadEngine(string dir, ILogger logger)
        {
            _dir = dir;
            _logger = logger;
        }

        public LoadEngine<T> Build()
        {
            _fileProvider = _ctx.FileProvider ?? new PhysicalFileProvider(_dir);

            if (_ctx.EnableHotPlug)
            {
                Task.Run(() =>
                {
                    while (true)
                    {
                        WatcherAsync().GetAwaiter().GetResult();
                    }
                });
            }
            UpdateAssemblies();
            OnLoad?.Invoke();
            isFirstLoad = false;
            return this;
        }

        public LoadEngine<T> EnableHotPlug()
        {
            _ctx.EnableHotPlug = true;
            return this;
        }

        public LoadEngine<T> ExcludeTypes(params Type[] types)
        {
            if (types != null && types.Length > 0)
                _ctx.ExcludeTypes.AddRange(types);
            return this;
        }

        public LoadEngine<T> ExcludeTypes(ExcludeType excludeType = ExcludeType.CaseSensitive, params string[] types)
        {
            switch (excludeType)
            {
                case ExcludeType.CaseSensitive:
                    if (types != null && types.Length > 0)
                        _ctx.ExcludeNamedTypes.AddRange(types);
                    break;
                case ExcludeType.CaseInsensitive:
                    if (types != null && types.Length > 0)
                        _ctx.ExcludeCaseInsensitiveNamedTypes.AddRange(types);
                    break;
                case ExcludeType.Regex:
                    if (types != null && types.Length > 0)
                        _ctx.ExcludeRegexNamedTypes.AddRange(types);
                    break;
            }
            return this;
        }

        public LoadEngine<T> ForceGarbageCollect()
        {
            _forceGc = true;
            return this;
        }

        private List<string> GetAllFiles(string directory, string searchPattern, SearchOption searchOption)
        {
            return Directory.EnumerateFiles(directory, searchPattern, searchOption).ToList();
        }

        public IEnumerable<Assembly> GetAssemblies()
        {
            var assms = _assemblies.Values;
            return assms;
        }

        public IEnumerable<Assembly> GetAssemblies(string[] namespaces, bool ignoreCase = false)
        {
            var assems = new List<Assembly>();
            foreach (var ns in namespaces)
                assems.Add(GetAssembly(ns, ignoreCase));
            return assems;
        }

        public Assembly GetAssembly(string @namespace, bool ignoreCase = false)
        {
            var assems = new List<Assembly>();
            var types = _assemblies.Values.SelectMany(x => x.DefinedTypes);
            var names = types.Where(y => y.Namespace.Contains(@namespace, ignoreCase));
            assems.AddRange(names.Select(z => z.Assembly));
            return assems.Distinct().FirstOrDefault();
        }

        public T GetInstance(params object[] ctorArgs)
        {
            var types = new List<T>();
            var tInfo = GetAssemblies().SelectMany(x => x.DefinedTypes).Where(x => x.AsType().HasInterface<T>()).ToList();
            foreach (var typeInfo in tInfo)
            {
                if (!IsExcluded(typeInfo.AsType()))
                    types.Add((T)Activator.CreateInstance(typeInfo.AsType(), ctorArgs));
            }
            return types.FirstOrDefault();

        }

        public IEnumerable<T> GetInstances(params object[] ctorArgs)
        {

            var types = new List<T>();
            var tInfo = GetAssemblies().SelectMany(x => x.DefinedTypes).Where(x => x.HasInterface<T>()).ToList();
            foreach (var typeInfo in tInfo)
            {
                if (!IsExcluded(typeInfo.AsType()))
                    types.Add((T)Activator.CreateInstance(typeInfo.AsType(), ctorArgs));
            }
            return types;

        }

        public IEnumerable<Stream> GetResources()
        {
            var streams = new List<Stream>();
            foreach (var assm in GetAssemblies())
            {
                var embedded = new EmbeddedFileProvider(assm);
                var resources = embedded.GetDirectoryContents("/");
                foreach (var resource in resources)
                    streams.Add(resource.CreateReadStream());
            }
            return streams;
        }

        public IEnumerable<Stream> GetResources(string fileName, bool ignoreCase = false)
        {
            var streams = new List<Stream>();
            foreach (var assm in GetAssemblies())
            {
                var embedded = new EmbeddedFileProvider(assm);
                var resources = embedded.GetDirectoryContents("/");
                foreach (var resource in resources.Where(x => x.Name.Contains(fileName, ignoreCase)))
                    streams.Add(resource.CreateReadStream());
            }
            return streams;
        }

        public IEnumerable<Stream> GetResources(string[] fileNames, bool ignoreCase = false)
        {
            var streams = new List<Stream>();
            foreach (var assm in GetAssemblies())
            {
                var embedded = new EmbeddedFileProvider(assm);
                var resources = embedded.GetDirectoryContents("/");
                foreach (var fileName in fileNames)
                    foreach (var resource in resources.Where(x => x.Name.Contains(fileName, ignoreCase)))
                        streams.Add(resource.CreateReadStream());
            }
            return streams;
        }

        public T GetSpecificInstance(string typeName, bool ignoreCase = false, params object[] ctorArgs)
        {

            var tInfo = GetAssemblies().SelectMany(x => x.DefinedTypes).Where(x => x.AsType().HasInterface<T>()).ToList();
            var t = tInfo.FirstOrDefault(x => x.FullName.Contains(typeName, ignoreCase)).AsType();
            if (!IsExcluded(t))
                return (T)Activator.CreateInstance(t, ctorArgs);
            return null;

        }

        public IEnumerable<T> GetSpecificInstances(string[] typesName, bool ignoreCase = false, params object[] ctorArgs)
        {

            var types = new List<T>();
            var tInfo = GetAssemblies().SelectMany(x => x.DefinedTypes).Where(x => x.AsType().HasInterface<T>()).ToList();

            foreach (var tn in typesName)
            {
                var t = tInfo.Where(x => x.FullName.Contains(tn, ignoreCase)).Select(y => (T)Activator.CreateInstance(y.AsType(), ctorArgs));

                foreach (var item in t)
                {
                    if (!IsExcluded(item.GetType()))
                        types.Add(item);
                }
            }
            return types;

        }

        private bool IsExcluded(Type type)
        {
            var fullName = type.FullName;

            if (_ctx.ExcludeTypes.Contains(type))
                return true;

            foreach (var item in _ctx.ExcludeNamedTypes)
            {
                if (fullName.Contains(item))
                    return true;
            }

            foreach (var item in _ctx.ExcludeCaseInsensitiveNamedTypes)
            {
                if (fullName.Contains(item, true))
                    return true;
            }

            foreach (var item in _ctx.ExcludeRegexNamedTypes)
            {
                var regex = new Regex(item);
                if (regex.IsMatch(fullName))
                    return true;
            }
            return false;
        }

        public LoadEngine<T> SearchOption(SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly)
        {
            _ctx.SearchOption = searchOption;
            return this;
        }

        private void UpdateAssemblies()
        {
            LoadContextUtility loadContext = new LoadContextUtility();
            _assemblies.Clear();
            var files = GetAllFiles(_dir, _ctx.Filter, _ctx.SearchOption);
            foreach (var file in files)
            {
                Exception ex;
                var assembly = new FileInfo(file).TryLoadAssembly(out ex, loadContext: loadContext);
                if (ex == null && assembly != null)
                    _assemblies.AddOrUpdate(file.ToLowerInvariant().Trim(), assembly);
            }
            if (_forceGc)
            {
                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            if (!isFirstLoad)
                OnChange?.Invoke();
        }

        public LoadEngine<T> UseFileProvider(IFileProvider fileProvider)
        {
            _ctx.FileProvider = fileProvider;
            return this;
        }

        public LoadEngine<T> UseFilter(string filter = "*.dll")
        {
            _ctx.Filter = filter;
            return this;
        }

        private async Task WatcherAsync()
        {
            var pattern = (_ctx.SearchOption == System.IO.SearchOption.TopDirectoryOnly ? "" : "**/") + _ctx.Filter;
            IChangeToken token = _fileProvider.Watch(pattern);
            var tcs = new TaskCompletionSource<object>();
            token.RegisterChangeCallback(state =>
                ((TaskCompletionSource<object>)state).TrySetResult(null), tcs);
            await tcs.Task.ConfigureAwait(false);
            UpdateAssemblies();
        }
    }
}
