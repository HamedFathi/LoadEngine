using DryIoc;
using DryIoc.Microsoft.DependencyInjection;
using Microsoft.Extensions.DependencyInjection;
using System;

namespace LoadEngine.DryIoc
{
    public static class DryIocExtensions
    {
        public static IServiceProvider AddDryIoc<TCompositionRoot>(this IServiceCollection services)
        {
            var container = new Container().WithDependencyInjectionAdapter(services);
            container.RegisterMany<TCompositionRoot>();
            container.Resolve<TCompositionRoot>();
            return container.Resolve<IServiceProvider>();
        }

        public static void RegisterLoadEngine<T>(this IRegistrator registrator, LoadEngine<T> loadEngine) where T : class
        {
            registrator.Register(typeof(T), loadEngine.GetInstance().GetType(), ifAlreadyRegistered: IfAlreadyRegistered.Replace);
            loadEngine.OnChange += () =>
                registrator.Register(typeof(T), loadEngine.GetInstance().GetType(), ifAlreadyRegistered: IfAlreadyRegistered.Replace);
        }
    }
}
