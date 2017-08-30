using LightInject;
using System;

namespace LoadEngine.LightInject
{
    internal class LoadEngineLifeCycle<T> : ILifetime where T : class
    {
        private bool _isChanged = false;
        private readonly LoadEngine<T> _loadEngine;
        private object _lastInstance = null;

        public LoadEngineLifeCycle(LoadEngine<T> loadEngine)
        {
            loadEngine.OnChange += () => _isChanged = true;
            _loadEngine = loadEngine;
        }

        public object GetInstance(Func<object> instanceFactory, Scope currentScope)
        {
            if (_isChanged)
            {
                _isChanged = false;
                _lastInstance = _loadEngine.GetInstance();
                return _lastInstance;
            }
            if (_lastInstance == null)
            {
                return instanceFactory();
            }
            else
            {
                return _lastInstance;
            }
        }
    }
}
