using StructureMap;
using System;
using System.Collections.Generic;
using System.Text;

namespace LoadEngine.StructureMap
{
    public static class StructureMapExtensions
    {
        public static void RegisterLoadEngine<T>(this Container container, LoadEngine<T> loadEngine)
               where T : class
        {
            loadEngine.OnChange += () =>
            {
                container.Model.EjectAndRemove(typeof(T));
                container.Configure(_ =>
                {
                    _.For(typeof(T)).Use(loadEngine.GetInstance());
                });
            };
            container.Configure(_ =>
            {
                _.For(typeof(T)).Use(loadEngine.GetInstance());
            });
        }
    }
}
