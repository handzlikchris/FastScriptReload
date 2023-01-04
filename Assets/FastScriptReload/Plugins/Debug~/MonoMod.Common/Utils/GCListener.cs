using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace MonoMod.Utils {
#if !MONOMOD_INTERNAL
    public
#endif
    static class GCListener {

        public static event Action OnCollect;
        private static bool Unloading;

        static GCListener() {
            new CollectionDummy();

#if NETSTANDARD
            Type t_AssemblyLoadContext = typeof(Assembly).GetTypeInfo().Assembly.GetType("System.Runtime.Loader.AssemblyLoadContext");
            if (t_AssemblyLoadContext != null) {
                object alc = t_AssemblyLoadContext.GetMethod("GetLoadContext").Invoke(null, new object[] { typeof(GCListener).Assembly });
                EventInfo e_Unloading = t_AssemblyLoadContext.GetEvent("Unloading");
                e_Unloading.AddEventHandler(alc, Delegate.CreateDelegate(
                    e_Unloading.EventHandlerType,
                    typeof(GCListener).GetMethod("UnloadingALC", BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(t_AssemblyLoadContext)
                ));
            }
#endif
        }

#if NETSTANDARD
#pragma warning disable IDE0051 // Remove unused private members
#pragma warning disable IDE0060 // Remove unused parameter
        private static void UnloadingALC<T>(T alc) {
#pragma warning restore IDE0051 // Remove unused private members
#pragma warning restore IDE0060 // Remove unused parameter
            Unloading = true;
        }
#endif

        private sealed class CollectionDummy {
            ~CollectionDummy() {
                Unloading |= AppDomain.CurrentDomain.IsFinalizingForUnload() || Environment.HasShutdownStarted;

                if (!Unloading)
                    GC.ReRegisterForFinalize(this);

                OnCollect?.Invoke();
            }
        }

    }
}
