using SimpleInjector;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace LoadEngine.SimpleInjector
{
    public static class ExtendedLifestyle
    {
        public static Lifestyle Changeable(bool isChanged)
        {
            var changeable = Lifestyle.CreateCustom(
            "ChangeableLifestyle",
            instanceCreator =>
            {
                var syncRoot = new object();
                object instance = null;
                return () =>
                {
                    lock (syncRoot)
                    {
                        if (isChanged)
                        {
                            isChanged = false;
                            instance = instanceCreator.Invoke();
                        }
                        return instance;
                    }
                };
            });
            return changeable;
        }
    }
}
