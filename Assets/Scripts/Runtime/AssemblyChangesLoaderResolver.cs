using System;
using System.Linq;
using System.Reflection;
using ImmersiveVRTools.Runtime.Common.Utilities;
using UnityEngine;

#if UNITY_EDITOR || LiveScriptReload_Enabled

namespace FastScriptReload.Runtime
{
    public class AssemblyChangesLoaderResolver
    {
        private static AssemblyChangesLoaderResolver _instance;
        public static AssemblyChangesLoaderResolver Instance => _instance ?? (_instance = new AssemblyChangesLoaderResolver());

        private IAssemblyChangesLoader _cachedNetworkLoader;
        
        public IAssemblyChangesLoader Resolve()
        {
#if LiveScriptReload_Enabled
            //network loader is in add-on that's not referenced by this lib, use reflection to get instance
            if (!(Component) _cachedNetworkLoader) //needs to cast for Unity based null comparison (for that to work with disabled domain reload)
            {
                _cachedNetworkLoader = (IAssemblyChangesLoader)ReflectionHelper.GetAllTypes()
                    .First(t => t.FullName == "LiveScriptReload.Runtime.NetworkedAssemblyChangesSender")
                    .GetProperty("Instance", BindingFlags.Static | BindingFlags.Public | BindingFlags.FlattenHierarchy)
                    .GetValue(null);
            }

            if (_cachedNetworkLoader == null)
            {
                throw new Exception("Unable to resolve NetworkedAssemblyChangesSender, Live Script Reload will not work - please contact support");
            }

            return _cachedNetworkLoader;
#else
            return AssemblyChangesLoader.Instance;
#endif

        }
    }
}

#endif