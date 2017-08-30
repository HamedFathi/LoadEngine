using Autofac;
using Autofac.Builder;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoadEngine.Autofac
{
    public static class AutofacExtensions
    {
        private static bool _isChanged = false;

        public static IRegistrationBuilder<object, ConcreteReflectionActivatorData, SingleRegistrationStyle> RegisterLoadEngine<T>(this ContainerBuilder builder, LoadEngine<T> loadEngine)
                    where T : class
        {
            if (loadEngine == null)
                return null;
            loadEngine.OnChange += () => _isChanged = true;
            object theInstance = null;
            var result = builder.RegisterType(loadEngine.GetInstance().GetType())
                 .As<T>()
                 .OnActivating(e =>
                 {
                     if (_isChanged)
                     {
                         e.ReplaceInstance(loadEngine.GetInstance());
                         theInstance = loadEngine.GetInstance();
                         _isChanged = false;
                     }
                     // Seems OnActivating() must pass ReplaceInstance everytimes so I must pass last instance everytimes 
                     // otherwise Autofac replace first instance again so I save last instance in 'theInstance' variable
                     // and pass when _isChanged = false
                     else if (theInstance != null)
                     {
                         e.ReplaceInstance(theInstance);
                     }
                 });

            return result;
        }
    }
}
