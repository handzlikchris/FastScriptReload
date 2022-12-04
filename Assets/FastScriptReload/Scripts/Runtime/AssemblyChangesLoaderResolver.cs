namespace FastScriptReload.Runtime
{
    public class AssemblyChangesLoaderResolver
    {
        private static AssemblyChangesLoaderResolver _instance;
        public static AssemblyChangesLoaderResolver Instance => _instance ?? (_instance = new AssemblyChangesLoaderResolver());

        public IAssemblyChangesLoader Resolve()
        {
#if FastScriptReload_LoadAssemblyOverNetwork_Enabled
            return LiveScriptReload.Runtime.NetworkedAssemblyChangesSender.Instance;
#else
            return AssemblyChangesLoader.Instance;
#endif

        }
    }
}