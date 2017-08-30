using LightInject;

namespace LoadEngine.LightInject
{
    public static class LightInjectExtensions
    {
        public static void RegisterLoadEngine<T>(this ServiceContainer container, LoadEngine<T> loadEngine) where T : class
        {
            container.Register(typeof(T), loadEngine.GetInstance().GetType(), new LoadEngineLifeCycle<T>(loadEngine));
        }
    }
}
