using SimpleInjector;
using System.Collections.Concurrent;

namespace LoadEngine.SimpleInjector
{
    public static class SimpleInjectorExtensions
    {
        private static readonly ConcurrentDictionary<string, bool> _concurrentDictionaryChanges = new ConcurrentDictionary<string, bool>();

        public static void RegisterLoadEngine<T>(this Container container, LoadEngine<T> loadEngine,
                    params object[] ctorArgs) where T : class
        {
            object Lock = new object();
            if (loadEngine == null)
                return;
            lock (Lock)
            {
                _concurrentDictionaryChanges[loadEngine.UniqueId] = true;
                loadEngine.OnChange += () =>
                {
                    _concurrentDictionaryChanges[loadEngine.UniqueId] = true;
                };
                container.Register(() => loadEngine.GetInstance(ctorArgs), Lifestyle.CreateCustom(
                    "SimpleInjectorLoadEngine" + _concurrentDictionaryChanges[loadEngine.UniqueId],
                    instanceCreator =>
                    {
                        var syncRoot = new object();
                        object instance = null;
                        return () =>
                        {
                            lock (syncRoot)
                            {
                                if (_concurrentDictionaryChanges[loadEngine.UniqueId])
                                {
                                    _concurrentDictionaryChanges[loadEngine.UniqueId] = false;
                                    instance = instanceCreator.Invoke();
                                }
                                return instance;
                            }
                        };
                    }));
            }
        }
    }
}
