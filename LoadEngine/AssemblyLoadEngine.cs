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
using System.Threading.Tasks;

namespace LoadEngine
{
    public class LoadEngine
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

        public LoadEngine Build()
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

        public LoadEngine EnableHotPlug()
        {
            _ctx.EnableHotPlug = true;
            return this;
        }

        public LoadEngine ForceGarbageCollect()
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

        public LoadEngine SearchOption(SearchOption searchOption = System.IO.SearchOption.TopDirectoryOnly)
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

        public LoadEngine UseFileProvider(IFileProvider fileProvider)
        {
            _ctx.FileProvider = fileProvider;
            return this;
        }

        public LoadEngine UseFilter(string filter = "*.dll")
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
